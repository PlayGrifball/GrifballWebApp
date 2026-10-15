using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GrifballWebApp.Database.Conversion;

/// <summary>
/// Writes the copied rows into the target, inside its transaction. Prepare empties the tables (the
/// migrations' seed rows; the source has its own) and stops what would rewrite the rows' history as
/// they go in; Finish puts that back.
/// </summary>
internal abstract class TargetWriter(DbConnection connection, DbTransaction transaction) : IAsyncDisposable
{
    protected DbConnection Connection { get; } = connection;
    protected DbTransaction Transaction { get; } = transaction;

    public static TargetWriter For(DatabaseProvider provider, DbConnection connection, DbTransaction transaction, ILogger logger) => provider switch
    {
        DatabaseProvider.SqlServer => new SqlServerTargetWriter((SqlConnection)connection, (SqlTransaction)transaction),
        DatabaseProvider.Postgres => new PostgresTargetWriter((NpgsqlConnection)connection, (NpgsqlTransaction)transaction, logger),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };

    public abstract Task PrepareAsync(IReadOnlyList<TablePlan> tables, CancellationToken cancellationToken);

    public abstract Task WriteAsync(TablePlan table, IReadOnlyList<object?[]> rows, CancellationToken cancellationToken);

    /// <summary>Sets the deferred foreign keys (TablePlan.Deferred) of rows already written, by their key.</summary>
    public abstract Task SetDeferredAsync(TablePlan table, IReadOnlyList<object?[]> rows, CancellationToken cancellationToken);

    public abstract Task FinishAsync(CancellationToken cancellationToken);

    /// <summary>The key's and the deferred foreign keys' columns, with their index in the plan's rows.</summary>
    protected static List<(ColumnInfo Column, int Index)> KeyAndDeferred(TablePlan table)
        => table.Key.Concat(table.Deferred).Select(i => (table.Columns[i], i)).ToList();

    public virtual Task AfterCommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected async Task ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        await using var command = Connection.CreateCommand();
        command.Transaction = Transaction;
        command.CommandTimeout = 0;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    protected async Task<List<object?[]>> QueryAsync(string sql, CancellationToken cancellationToken)
    {
        await using var command = Connection.CreateCommand();
        command.Transaction = Transaction;
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
                row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>Emptied children first, as their foreign keys need.</summary>
    protected async Task DeleteAllAsync(SqlDialect dialect, IReadOnlyList<TablePlan> tables, CancellationToken cancellationToken)
    {
        foreach (var table in tables.Reverse())
            await ExecuteAsync($"DELETE FROM {dialect.Quote(table.Name)}", cancellationToken);
    }
}

/// <summary>
/// SQL Server. A temporal table's period columns are GENERATED ALWAYS, which takes no values even with
/// system versioning off and the period dropped. So for the load, each temporal table's period columns
/// (and its history table's, to keep the two tables' columns in the same order) are dropped and added
/// again as plain datetime2 columns; once the rows are in, ADD PERIOD FOR SYSTEM_TIME makes them its
/// period again, keeping their values, and system versioning goes back on with the same history table,
/// checking its rows against the current ones (DATA_CONSISTENCY_CHECK). Indexes on the period columns
/// (the history table's clustered one) are recreated as they were. The columns end up last in their
/// tables, which nothing refers to: EF goes by name. Rows go in by SqlBulkCopy, identity values kept and
/// constraints checked (so they stay trusted).
/// </summary>
internal sealed class SqlServerTargetWriter(SqlConnection connection, SqlTransaction transaction)
    : TargetWriter(connection, transaction)
{
    private static readonly SqlServerDialect Dialect = SqlServerDialect.Instance;

    private sealed record Temporal(TableName Table, TableName History, string Start, string End, byte Scale, bool StartHidden, bool EndHidden)
    {
        public List<string> Indexes { get; } = [];
    }

    private List<Temporal> _temporal = [];

    public override async Task PrepareAsync(IReadOnlyList<TablePlan> tables, CancellationToken cancellationToken)
    {
        var rows = await QueryAsync("""
            SELECT s.name, t.name, hs.name, h.name, ps.name, pe.name, ps.scale, ps.is_hidden, pe.is_hidden
            FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            JOIN sys.tables h ON h.object_id = t.history_table_id
            JOIN sys.schemas hs ON hs.schema_id = h.schema_id
            JOIN sys.periods p ON p.object_id = t.object_id
            JOIN sys.columns ps ON ps.object_id = t.object_id AND ps.column_id = p.start_column_id
            JOIN sys.columns pe ON pe.object_id = t.object_id AND pe.column_id = p.end_column_id
            WHERE t.temporal_type = 2
            ORDER BY s.name, t.name
            """, cancellationToken);
        _temporal = rows.Select(r => new Temporal(
            Dialect.Table((string)r[0]!, (string)r[1]!), Dialect.Table((string)r[2]!, (string)r[3]!),
            (string)r[4]!, (string)r[5]!, (byte)r[6]!, (bool)r[7]!, (bool)r[8]!)).ToList();

        foreach (var temporal in _temporal)
        {
            await ExecuteAsync($"ALTER TABLE {Dialect.Quote(temporal.Table)} SET (SYSTEM_VERSIONING = OFF)", cancellationToken);
            await ExecuteAsync($"ALTER TABLE {Dialect.Quote(temporal.Table)} DROP PERIOD FOR SYSTEM_TIME", cancellationToken);
        }

        await DeleteAllAsync(Dialect, tables, cancellationToken);

        foreach (var temporal in _temporal)
        {
            foreach (var table in new[] { temporal.Table, temporal.History })
            {
                foreach (var (name, create) in await PeriodIndexesAsync(table, temporal, cancellationToken))
                {
                    temporal.Indexes.Add(create);
                    await ExecuteAsync($"DROP INDEX {Dialect.Quote(name)} ON {Dialect.Quote(table)}", cancellationToken);
                }
                await ExecuteAsync($"ALTER TABLE {Dialect.Quote(table)} DROP COLUMN {Dialect.Quote(temporal.Start)}, {Dialect.Quote(temporal.End)}", cancellationToken);
                await ExecuteAsync($"ALTER TABLE {Dialect.Quote(table)} ADD {Dialect.Quote(temporal.Start)} datetime2({temporal.Scale}) NOT NULL, {Dialect.Quote(temporal.End)} datetime2({temporal.Scale}) NOT NULL", cancellationToken);
            }
        }
    }

    /// <summary>The indexes on the table that use a period column, each with the statement that makes it.</summary>
    private async Task<List<(string Name, string Create)>> PeriodIndexesAsync(TableName table, Temporal temporal, CancellationToken cancellationToken)
    {
        var rows = await QueryAsync($"""
            SELECT i.name, i.is_unique, i.type_desc, i.filter_definition, c.name, ic.is_descending_key, ic.is_included_column
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID(N'{Dialect.Quote(table).Replace("'", "''")}')
              AND i.is_primary_key = 0 AND i.is_unique_constraint = 0
              AND EXISTS (
                SELECT 1 FROM sys.index_columns pic JOIN sys.columns pc ON pc.object_id = pic.object_id AND pc.column_id = pic.column_id
                WHERE pic.object_id = i.object_id AND pic.index_id = i.index_id AND pc.name IN (N'{temporal.Start.Replace("'", "''")}', N'{temporal.End.Replace("'", "''")}'))
            ORDER BY i.name, ic.is_included_column, ic.key_ordinal, ic.index_column_id
            """, cancellationToken);
        return rows.GroupBy(r => (string)r[0]!).Select(index =>
        {
            var first = index.First();
            var sql = new StringBuilder("CREATE ");
            if ((bool)first[1]!)
                sql.Append("UNIQUE ");
            sql.Append((string)first[2]! == "CLUSTERED" ? "CLUSTERED " : "NONCLUSTERED ");
            sql.Append($"INDEX {Dialect.Quote(index.Key)} ON {Dialect.Quote(table)} (");
            sql.Append(string.Join(", ", index.Where(c => !(bool)c[6]!).Select(c => Dialect.Quote((string)c[4]!) + ((bool)c[5]! ? " DESC" : " ASC"))));
            sql.Append(')');
            var included = index.Where(c => (bool)c[6]!).Select(c => Dialect.Quote((string)c[4]!)).ToList();
            if (included.Count > 0)
                sql.Append($" INCLUDE ({string.Join(", ", included)})");
            if (first[3] is string filter)
                sql.Append($" WHERE {filter}");
            return (index.Key, sql.ToString());
        }).ToList();
    }

    public override async Task WriteAsync(TablePlan table, IReadOnlyList<object?[]> rows, CancellationToken cancellationToken)
    {
        var columns = table.Columns.Select((c, i) => (Column: c, Index: i)).Where(c => !c.Column.Generated).ToList();
        if (rows.Count == 0)
            return;
        await BulkCopyAsync(Dialect.Quote(table.Name), columns, rows, cancellationToken);

        // The identity carries on after the highest value written.
        if (columns.Any(c => c.Column.Identity))
            await ExecuteAsync($"DBCC CHECKIDENT (N'{Dialect.Quote(table.Name).Replace("'", "''")}', RESEED) WITH NO_INFOMSGS", cancellationToken);
    }

    private async Task BulkCopyAsync(string destination, List<(ColumnInfo Column, int Index)> columns, IReadOnlyList<object?[]> rows, CancellationToken cancellationToken)
    {
        using var data = new DataTable();
        foreach (var (column, _) in columns)
            data.Columns.Add(column.Name, typeof(object));
        foreach (var row in rows)
            data.Rows.Add(columns.Select(c => ToSqlServer(row[c.Index], c.Column) ?? DBNull.Value).ToArray());

        using var bulkCopy = new SqlBulkCopy((SqlConnection)Connection,
            SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.KeepNulls | SqlBulkCopyOptions.CheckConstraints,
            (SqlTransaction)Transaction)
        {
            DestinationTableName = destination,
            BulkCopyTimeout = 0,
        };
        foreach (var (column, _) in columns)
            bulkCopy.ColumnMappings.Add(column.Name, column.Name);
        await bulkCopy.WriteToServerAsync(data, cancellationToken);
    }

    public override async Task SetDeferredAsync(TablePlan table, IReadOnlyList<object?[]> rows, CancellationToken cancellationToken)
    {
        const string staging = "#grif_deferred";
        var columns = KeyAndDeferred(table);
        await ExecuteAsync($"SELECT TOP 0 {string.Join(", ", columns.Select(c => Dialect.Quote(c.Column.Name)))} INTO {staging} FROM {Dialect.Quote(table.Name)}", cancellationToken);
        await BulkCopyAsync(staging, columns, rows, cancellationToken);
        var set = string.Join(", ", table.Deferred.Select(i => $"t.{Dialect.Quote(table.Columns[i].Name)} = d.{Dialect.Quote(table.Columns[i].Name)}"));
        var on = string.Join(" AND ", table.Key.Select(i => $"t.{Dialect.Quote(table.Columns[i].Name)} = d.{Dialect.Quote(table.Columns[i].Name)}"));
        await ExecuteAsync($"UPDATE t SET {set} FROM {Dialect.Quote(table.Name)} AS t JOIN {staging} AS d ON {on}", cancellationToken);
        await ExecuteAsync($"DROP TABLE {staging}", cancellationToken);
    }

    /// <summary>A value read from either server, as SQL Server's column takes it.</summary>
    internal static object? ToSqlServer(object? value, ColumnInfo column) => (value, column.DataType) switch
    {
        (DateTime d, "datetimeoffset") => d == DateTime.MaxValue ? DateTimeOffset.MaxValue
            : d == DateTime.MinValue ? DateTimeOffset.MinValue
            : new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)),
        (DateTimeOffset o, "datetime2" or "datetime") => o.UtcDateTime,
        (DateTime d, _) => DateTime.SpecifyKind(d, DateTimeKind.Unspecified),
        (TimeOnly t, _) => t.ToTimeSpan(),
        _ => value,
    };

    public override async Task FinishAsync(CancellationToken cancellationToken)
    {
        foreach (var temporal in _temporal)
        {
            var table = Dialect.Quote(temporal.Table);
            await ExecuteAsync($"ALTER TABLE {table} ADD PERIOD FOR SYSTEM_TIME ({Dialect.Quote(temporal.Start)}, {Dialect.Quote(temporal.End)})", cancellationToken);
            if (temporal.StartHidden)
                await ExecuteAsync($"ALTER TABLE {table} ALTER COLUMN {Dialect.Quote(temporal.Start)} ADD HIDDEN", cancellationToken);
            if (temporal.EndHidden)
                await ExecuteAsync($"ALTER TABLE {table} ALTER COLUMN {Dialect.Quote(temporal.End)} ADD HIDDEN", cancellationToken);
            foreach (var index in temporal.Indexes)
                await ExecuteAsync(index, cancellationToken);
            await ExecuteAsync($"ALTER TABLE {table} SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = {Dialect.Quote(temporal.History)}, DATA_CONSISTENCY_CHECK = ON))", cancellationToken);
        }
    }
}

/// <summary>
/// PostgreSQL. Row history's triggers (PostgresHistory) are switched off for the load, so the copied
/// rows keep their PeriodStart and add no history of their own, and the history tables take the
/// source's; DISABLE TRIGGER USER leaves the foreign keys' triggers on, and needs only the tables'
/// owner. Rows go in by binary COPY, each value as its column's type; PeriodEnd, which the table
/// generates ('infinity'), is left to it. Identity columns' sequences then carry on after the highest
/// value, and after the commit ANALYZE gives the planner the new tables' statistics.
/// </summary>
internal sealed class PostgresTargetWriter(NpgsqlConnection connection, NpgsqlTransaction transaction, ILogger logger)
    : TargetWriter(connection, transaction)
{
    private static readonly PostgresDialect Dialect = PostgresDialect.Instance;

    private IReadOnlyList<TablePlan> _tables = [];

    public override async Task PrepareAsync(IReadOnlyList<TablePlan> tables, CancellationToken cancellationToken)
    {
        _tables = tables;
        foreach (var table in tables)
            await ExecuteAsync($"ALTER TABLE {Dialect.Quote(table.Name)} DISABLE TRIGGER USER", cancellationToken);
        await DeleteAllAsync(Dialect, tables, cancellationToken);
    }

    public override async Task WriteAsync(TablePlan table, IReadOnlyList<object?[]> rows, CancellationToken cancellationToken)
    {
        var columns = table.Columns.Select((c, i) => (Column: c, Index: i)).Where(c => !c.Column.Generated).ToList();
        if (rows.Count == 0)
            return;
        await CopyAsync(Dialect.Quote(table.Name), columns, rows, cancellationToken);

        foreach (var (column, _) in columns.Where(c => c.Column.Identity))
        {
            var name = Dialect.Quote(table.Name).Replace("'", "''");
            await ExecuteAsync($"SELECT setval(pg_get_serial_sequence('{name}', '{column.Name.Replace("'", "''")}'), (SELECT max({Dialect.Quote(column.Name)}) FROM {Dialect.Quote(table.Name)}))", cancellationToken);
        }
    }

    private async Task CopyAsync(string destination, List<(ColumnInfo Column, int Index)> columns, IReadOnlyList<object?[]> rows, CancellationToken cancellationToken)
    {
        var copy = $"COPY {destination} ({string.Join(", ", columns.Select(c => Dialect.Quote(c.Column.Name)))}) FROM STDIN (FORMAT BINARY)";
        await using var importer = await ((NpgsqlConnection)Connection).BeginBinaryImportAsync(copy, cancellationToken);
        foreach (var row in rows)
        {
            await importer.StartRowAsync(cancellationToken);
            foreach (var (column, index) in columns)
            {
                if (ToPostgres(row[index], column) is { } value)
                    await importer.WriteAsync(value, column.DataType, cancellationToken);
                else
                    await importer.WriteNullAsync(cancellationToken);
            }
        }
        await importer.CompleteAsync(cancellationToken);
    }

    public override async Task SetDeferredAsync(TablePlan table, IReadOnlyList<object?[]> rows, CancellationToken cancellationToken)
    {
        const string staging = "grif_deferred";
        var columns = KeyAndDeferred(table);
        await ExecuteAsync($"CREATE TEMPORARY TABLE {staging} ON COMMIT DROP AS SELECT {string.Join(", ", columns.Select(c => Dialect.Quote(c.Column.Name)))} FROM {Dialect.Quote(table.Name)} WITH NO DATA", cancellationToken);
        await CopyAsync(staging, columns, rows, cancellationToken);
        var set = string.Join(", ", table.Deferred.Select(i => $"{Dialect.Quote(table.Columns[i].Name)} = d.{Dialect.Quote(table.Columns[i].Name)}"));
        var where = string.Join(" AND ", table.Key.Select(i => $"t.{Dialect.Quote(table.Columns[i].Name)} = d.{Dialect.Quote(table.Columns[i].Name)}"));
        await ExecuteAsync($"UPDATE {Dialect.Quote(table.Name)} AS t SET {set} FROM {staging} AS d WHERE {where}", cancellationToken);
        await ExecuteAsync($"DROP TABLE {staging}", cancellationToken);
    }

    private static DateTime Microseconds(DateTime d) => new(d.Ticks - d.Ticks % TimeSpan.TicksPerMicrosecond, DateTimeKind.Utc);

    /// <summary>
    /// A value read from either server, as Postgres's column takes it: instants UTC (the app's
    /// DateTimes are UTC on both: UtcDateTimeConverter) and to the microsecond, Postgres's precision,
    /// rounded down as the check compares them; SQL Server's end and beginning of time as Postgres's
    /// 'infinity' and '-infinity', which Npgsql writes for DateTime.MaxValue and MinValue.
    /// </summary>
    internal static object? ToPostgres(object? value, ColumnInfo column) => value switch
    {
        DateTime d when d == DateTime.MaxValue || d == DateTime.MinValue => DateTime.SpecifyKind(d, DateTimeKind.Utc),
        DateTime d => Microseconds(d),
        DateTimeOffset o when o == DateTimeOffset.MaxValue => DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc),
        DateTimeOffset o when o == DateTimeOffset.MinValue => DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
        DateTimeOffset o => Microseconds(o.UtcDateTime),
        TimeSpan t => TimeSpan.FromTicks(t.Ticks - t.Ticks % TimeSpan.TicksPerMicrosecond),
        _ => value,
    };

    public override async Task FinishAsync(CancellationToken cancellationToken)
    {
        foreach (var table in _tables)
            await ExecuteAsync($"ALTER TABLE {Dialect.Quote(table.Name)} ENABLE TRIGGER USER", cancellationToken);
    }

    public override async Task AfterCommitAsync(CancellationToken cancellationToken)
    {
        await using var command = Connection.CreateCommand();
        command.CommandTimeout = 0;
        command.CommandText = "ANALYZE";
        await command.ExecuteNonQueryAsync(cancellationToken);
        logger.LogInformation("Analyzed the target's tables");
    }
}
