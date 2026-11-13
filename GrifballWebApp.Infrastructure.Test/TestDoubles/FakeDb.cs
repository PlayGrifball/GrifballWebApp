using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>A command as the code sent it: its SQL, its parameters by name, and the transaction it ran in.</summary>
internal sealed record SentCommand(string Sql, IReadOnlyDictionary<string, object?> Parameters, DbTransaction? Transaction, int Timeout);

/// <summary>
/// A database connection that never reaches a server: every command the code sends is recorded (<see cref="Sent"/>)
/// and answered by <see cref="Answer"/>, which returns what the command reads (a <see cref="DbDataReader"/> for a
/// query, a value for a scalar, a row count for the rest) or throws, as a server would. Opening and transactions are
/// recorded too. For the code that talks ADO.NET itself (raw commands, an EF context given this connection).
/// </summary>
internal sealed class FakeDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;
    private readonly List<SentCommand> _sent = [];

    /// <summary>What each command gets back; by default nothing (0 rows, a null scalar, an empty reader).</summary>
    public Func<SentCommand, object?> Answer { get; set; } = _ => null;

    public IReadOnlyList<SentCommand> Sent
    {
        get
        {
            lock (_sent)
                return [.. _sent];
        }
    }

    public List<IsolationLevel> TransactionsBegun { get; } = [];
    public int Opened { get; private set; }
    public int Closed { get; private set; }

    [AllowNull]
    public override string ConnectionString { get; set; } = "Server=fake;Database=grif";
    public override string Database => "grif";
    public override string DataSource => "fake";
    public override string ServerVersion => "16.0";
    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

    public override void Open()
    {
        Opened++;
        _state = ConnectionState.Open;
    }

    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        Open();
        return Task.CompletedTask;
    }

    public override void Close()
    {
        if (_state == ConnectionState.Open)
            Closed++;
        _state = ConnectionState.Closed;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        TransactionsBegun.Add(isolationLevel);
        return new FakeDbTransaction(this, isolationLevel);
    }

    protected override DbCommand CreateDbCommand() => new FakeDbCommand(this);

    internal object? Run(FakeDbCommand command)
    {
        var sent = new SentCommand(command.CommandText,
            command.Parameters.Cast<DbParameter>().ToDictionary(p => p.ParameterName, p => p.Value == DBNull.Value ? null : p.Value),
            command.Transaction, command.CommandTimeout);
        lock (_sent)
            _sent.Add(sent);
        return Answer(sent);
    }

    /// <summary>A reader of these rows, the columns named as given.</summary>
    public static DbDataReader Rows(string[] columns, params object?[][] rows)
    {
        var table = new DataTable();
        for (var i = 0; i < columns.Length; i++)
        {
            var type = rows.Select(r => r[i]).FirstOrDefault(v => v is not null)?.GetType() ?? typeof(object);
            table.Columns.Add(columns[i], type);
        }
        foreach (var row in rows)
            table.Rows.Add(row.Select(v => v ?? DBNull.Value).ToArray());
        return table.CreateDataReader();
    }
}

internal sealed class FakeDbTransaction(FakeDbConnection connection, IsolationLevel isolationLevel) : DbTransaction
{
    public bool Committed { get; private set; }
    public bool RolledBack { get; private set; }
    public override IsolationLevel IsolationLevel { get; } = isolationLevel;
    protected override DbConnection DbConnection { get; } = connection;
    public override void Commit() => Committed = true;
    public override void Rollback() => RolledBack = true;
}

internal sealed class FakeDbCommand(FakeDbConnection connection) : DbCommand
{
    private readonly FakeParameters _parameters = new();

    [AllowNull]
    public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; } = 30;
    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; } = connection;
    protected override DbParameterCollection DbParameterCollection => _parameters;
    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel()
    {
        // Nothing runs long enough to cancel
    }

    public override void Prepare()
    {
        // Nothing to prepare: the answer is canned
    }

    protected override DbParameter CreateDbParameter() => new FakeParameter();

    public override int ExecuteNonQuery() => connection.Run(this) is int rows ? rows : 0;

    public override object? ExecuteScalar() => connection.Run(this);

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        => connection.Run(this) as DbDataReader ?? new DataTable().CreateDataReader();
}

internal sealed class FakeParameter : DbParameter
{
    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public override bool IsNullable { get; set; }
    [AllowNull]
    public override string ParameterName { get; set; } = "";
    [AllowNull]
    public override string SourceColumn { get; set; } = "";
    public override object? Value { get; set; }
    public override bool SourceColumnNullMapping { get; set; }
    public override int Size { get; set; }
    public override void ResetDbType() => DbType = DbType.String;
}

internal sealed class FakeParameters : DbParameterCollection
{
    private readonly List<DbParameter> _items = [];

    public override int Count => _items.Count;
    public override object SyncRoot => _items;
    public override int Add(object value)
    {
        _items.Add((DbParameter)value);
        return _items.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values)
            Add(value);
    }

    public override void Clear() => _items.Clear();
    public override bool Contains(object value) => _items.Contains((DbParameter)value);
    public override bool Contains(string value) => IndexOf(value) >= 0;
    public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)_items).CopyTo(array, index);
    public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
    public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
    public override int IndexOf(string parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);
    public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
    public override void Remove(object value) => _items.Remove((DbParameter)value);
    public override void RemoveAt(int index) => _items.RemoveAt(index);
    public override void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));
    protected override DbParameter GetParameter(int index) => _items[index];
    protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
    protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
    protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
}
