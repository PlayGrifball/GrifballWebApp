using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GrifballWebApp.Database.Conversion;

/// <summary>
/// SQLite's columns as the app's model maps them there. SQLite keeps a value as one of a handful of storage
/// classes (an integer, a real, text), not as its column's type, so what a value is comes from the model:
/// each column's provider type (EF's text for a DateTime, Guid or TimeOnly, an integer for a bool or an enum)
/// and the conversions the model makes on SQLite alone (GrifballContext: a TimeSpan as its ticks, a decimal as
/// a double). The conversion reads and writes the other servers' values (a TimeSpan, a decimal), so those two
/// are undone as rows are read and done as they're written. SQLite has no schemas: a table is named by its
/// name alone, and the model's schema for it is looked up by that.
/// </summary>
internal sealed class SqliteColumns
{
    public static SqliteColumns Instance => Lazy.Value;

    private static readonly Lazy<SqliteColumns> Lazy = new(() =>
    {
        using var context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, "Data Source=:memory:").Options);
        return new SqliteColumns(context.GetService<IDesignTimeModel>().Model.GetRelationalModel());
    });

    private readonly Dictionary<string, string> _schemas = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Table, string Column), (Type Provider, ValueConverter? Undo)> _columns = [];

    internal SqliteColumns(IRelationalModel model)
    {
        foreach (var table in model.Tables)
        {
            _schemas[table.Name] = table.Schema ?? "";
            foreach (var column in table.Columns)
            {
                var mapping = column.PropertyMappings.First().TypeMapping;
                var converter = mapping.Converter;
                var provider = converter?.ProviderClrType ?? mapping.ClrType;
                _columns[(table.Name, column.Name)] = (Nullable.GetUnderlyingType(provider) ?? provider, SqliteOnly(converter) ? converter : null);
            }
        }
    }

    /// <summary>The conversions only SQLite has (GrifballContext.ConfigureConventions).</summary>
    private static bool SqliteOnly(ValueConverter? converter)
    {
        var model = converter is null ? null : Nullable.GetUnderlyingType(converter.ModelClrType) ?? converter.ModelClrType;
        return model == typeof(TimeSpan) || model == typeof(decimal);
    }

    /// <summary>The model's schema of the table SQLite calls <paramref name="name"/>.</summary>
    public string SchemaOf(string name) => _schemas.GetValueOrDefault(name, "");

    /// <summary>A value as SQLite stores it, read as the other servers read theirs.</summary>
    public object Read(DbDataReader reader, int ordinal, TableName table, string column)
    {
        if (!_columns.TryGetValue((table.Name, column), out var mapped))
            return reader.GetValue(ordinal);
        var value = ReadAs(reader, ordinal, mapped.Provider);
        return mapped.Undo is { } undo ? undo.ConvertFromProvider(value)! : value;
    }

    internal static object ReadAs(DbDataReader reader, int ordinal, Type type)
    {
        if (type == typeof(DateTime)) return reader.GetFieldValue<DateTime>(ordinal);
        if (type == typeof(DateTimeOffset)) return reader.GetFieldValue<DateTimeOffset>(ordinal);
        if (type == typeof(Guid)) return reader.GetFieldValue<Guid>(ordinal);
        if (type == typeof(bool)) return reader.GetFieldValue<bool>(ordinal);
        if (type == typeof(byte)) return reader.GetFieldValue<byte>(ordinal);
        if (type == typeof(short)) return reader.GetFieldValue<short>(ordinal);
        if (type == typeof(int)) return reader.GetFieldValue<int>(ordinal);
        if (type == typeof(long)) return reader.GetFieldValue<long>(ordinal);
        // As SQL Server and Postgres read theirs (decimal(20,0), numeric(20,0)), so it writes to them as is
        if (type == typeof(ulong)) return (decimal)reader.GetFieldValue<ulong>(ordinal);
        if (type == typeof(double)) return reader.GetFieldValue<double>(ordinal);
        if (type == typeof(float)) return reader.GetFieldValue<float>(ordinal);
        if (type == typeof(decimal)) return reader.GetFieldValue<decimal>(ordinal);
        if (type == typeof(TimeSpan)) return reader.GetFieldValue<TimeSpan>(ordinal);
        if (type == typeof(TimeOnly)) return reader.GetFieldValue<TimeOnly>(ordinal);
        if (type == typeof(DateOnly)) return reader.GetFieldValue<DateOnly>(ordinal);
        if (type == typeof(string)) return reader.GetFieldValue<string>(ordinal);
        if (type == typeof(byte[])) return reader.GetFieldValue<byte[]>(ordinal);
        return reader.GetValue(ordinal);
    }

    /// <summary>
    /// A value read from any server, as EF stores it on SQLite: a DateTime as its text (UTC, the app's:
    /// UtcDateTimeConverter), a Guid as upper-case text, a bool as 0 or 1, and SQLite's own conversions done.
    /// </summary>
    // EF's, on SQLite.
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss.FFFFFFF";
    private const string OffsetFormat = "yyyy-MM-dd HH:mm:ss.FFFFFFFzzz";
    private const string TimeFormat = "HH:mm:ss.FFFFFFF";

    public object? Write(object? value, TableName table, string column)
    {
        if (value is null)
            return null;
        _columns.TryGetValue((table.Name, column), out var mapped);
        if (mapped.Undo is { } convert
            && (Nullable.GetUnderlyingType(convert.ModelClrType) ?? convert.ModelClrType).IsInstanceOfType(value))
            value = convert.ConvertToProvider(value);
        var provider = mapped.Provider;
        return value switch
        {
            // Postgres reads a timestamptz as a UTC DateTime, and SQL Server's datetime2 has no offset.
            DateTime d when provider == typeof(DateTimeOffset) => new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)).ToString(OffsetFormat, CultureInfo.InvariantCulture),
            DateTime d => d.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
            DateTimeOffset o when provider == typeof(DateTimeOffset) => o.ToString(OffsetFormat, CultureInfo.InvariantCulture),
            DateTimeOffset o => o.UtcDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
            // SQL Server and Postgres keep a ulong as a decimal number (decimal(20,0), numeric(20,0)); EF keeps it on
            // SQLite as the integer of its bits (Microsoft.Data.Sqlite's), which a decimal would make a lossy real.
            decimal m when provider == typeof(ulong) => (ulong)m,
            Guid g => g.ToString().ToUpperInvariant(),
            bool b => b ? 1L : 0L,
            // A time of day as text, as EF writes a TimeOnly; SQL Server reads its time columns as TimeSpans.
            TimeSpan t when provider == typeof(TimeOnly) => TimeOnly.FromTimeSpan(t).ToString(TimeFormat, CultureInfo.InvariantCulture),
            TimeOnly t => t.ToString(TimeFormat, CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => value,
        };
    }
}

/// <summary>SQLite: no schemas, names in double quotes, columns from pragma_table_xinfo.</summary>
internal sealed class SqliteDialect : SqlDialect
{
    public static readonly SqliteDialect Instance = new();

    public override string DefaultSchema => "";

    public override string Quote(string name) => $"\"{name.Replace("\"", "\"\"")}\"";

    public override string Quote(TableName table) => Quote(table.Name);

    public override TableName Table(string schema, string name) => new(SqliteColumns.Instance.SchemaOf(name), name);

    // Generated: hidden 2 (virtual) or 3 (stored), as PeriodEnd is. SQLite's own tables (sqlite_sequence) left out.
    protected override string ColumnsQuery => """
        SELECT '', m.name, p.name, p.type, p.hidden IN (2, 3), 0
        FROM sqlite_master AS m
        JOIN pragma_table_xinfo(m.name) AS p
        WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite\_%' ESCAPE '\'
        ORDER BY m.name, p.cid
        """;

    public override object ReadValue(DbDataReader reader, int ordinal, TableName table, ColumnInfo column)
        => SqliteColumns.Instance.Read(reader, ordinal, table, column.Name);
}

/// <summary>
/// SQLite. Row history's triggers (SqliteHistory) are dropped for the load, so the copied rows keep their
/// PeriodStart and add no history of their own, and the history tables take the source's; they're created
/// again as they were once the rows are in. Rows go in by one prepared INSERT a table, each value as EF
/// stores it there (SqliteColumns). PeriodEnd, which the table generates, is left to it. An AUTOINCREMENT
/// key carries on after the highest written by itself.
/// </summary>
internal sealed class SqliteTargetWriter(SqliteConnection connection, SqliteTransaction transaction)
    : TargetWriter(connection, transaction)
{
    private static readonly SqliteDialect Dialect = SqliteDialect.Instance;

    private List<string> _triggers = [];

    public override async Task PrepareAsync(IReadOnlyList<TablePlan> tables, CancellationToken cancellationToken)
    {
        var triggers = await QueryAsync("SELECT name, sql FROM sqlite_master WHERE type = 'trigger' ORDER BY name", cancellationToken);
        _triggers = triggers.Select(t => (string)t[1]!).ToList();
        foreach (var trigger in triggers)
            await ExecuteAsync($"DROP TRIGGER {Dialect.Quote((string)trigger[0]!)}", cancellationToken);
        await DeleteAllAsync(Dialect, tables, cancellationToken);
    }

    public override async Task WriteAsync(TablePlan table, IReadOnlyList<object?[]> rows, CancellationToken cancellationToken)
    {
        var columns = table.Columns.Select((c, i) => (Column: c, Index: i)).Where(c => !c.Column.Generated).ToList();
        if (rows.Count == 0)
            return;
        var names = string.Join(", ", columns.Select(c => Dialect.Quote(c.Column.Name)));
        var values = string.Join(", ", columns.Select((_, i) => $"$p{i}"));
        await RunForEachAsync($"INSERT INTO {Dialect.Quote(table.Name)} ({names}) VALUES ({values})", table, columns, rows, cancellationToken);
    }

    public override async Task SetDeferredAsync(TablePlan table, IReadOnlyList<object?[]> rows, CancellationToken cancellationToken)
    {
        var columns = table.Deferred.Concat(table.Key).Select(i => (Column: table.Columns[i], Index: i)).ToList();
        var set = string.Join(", ", table.Deferred.Select((i, n) => $"{Dialect.Quote(table.Columns[i].Name)} = $p{n}"));
        var where = string.Join(" AND ", table.Key.Select((i, n) => $"{Dialect.Quote(table.Columns[i].Name)} = $p{table.Deferred.Length + n}"));
        await RunForEachAsync($"UPDATE {Dialect.Quote(table.Name)} SET {set} WHERE {where}", table, columns, rows, cancellationToken);
    }

    /// <summary>Runs <paramref name="sql"/> once a row, its $p0, $p1... the row's values of <paramref name="columns"/>.</summary>
    private async Task RunForEachAsync(string sql, TablePlan table, List<(ColumnInfo Column, int Index)> columns, IReadOnlyList<object?[]> rows, CancellationToken cancellationToken)
    {
        await using var command = (SqliteCommand)Connection.CreateCommand();
        command.Transaction = (SqliteTransaction)Transaction;
        command.CommandTimeout = 0;
        command.CommandText = sql;
        var parameters = columns.Select((_, i) => command.Parameters.Add(new SqliteParameter($"$p{i}", null))).ToList();
        foreach (var row in rows)
        {
            for (var i = 0; i < columns.Count; i++)
                parameters[i].Value = SqliteColumns.Instance.Write(row[columns[i].Index], table.Name, columns[i].Column.Name) ?? DBNull.Value;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public override async Task FinishAsync(CancellationToken cancellationToken)
    {
        foreach (var trigger in _triggers)
            await ExecuteAsync(trigger, cancellationToken);
    }
}
