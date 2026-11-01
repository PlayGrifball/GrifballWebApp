using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;

namespace GrifballWebApp.Database.Conversion;

/// <summary>
/// A GrifballWebApp database, on any of the three servers, written out as one SQL script for SQL Server or
/// PostgreSQL that builds it again in a new, empty database there: the target's own migrations (its schema, exactly as
/// the app makes it on that server), then every table's rows, row history included, as DatabaseConverter copies them.
/// What depends on the restored server's catalog (SQL Server's temporal tables, identity seeds, Postgres's row
/// history triggers and identity sequences) the script works out there as it runs, the way the converter's target
/// writers do. It all runs in one transaction, and refuses a database that already has tables.
///
/// No target server is needed to write it: the target's tables and column types come from its model (the
/// migrations' snapshot), SQL Server's temporal history tables from the temporal tables they belong to. The source
/// is read as the converter reads it (one snapshot, where the server allows), and must have exactly this build's
/// migrations. Run it with psql -v ON_ERROR_STOP=1, or sqlcmd -b.
/// </summary>
public sealed class DatabaseScriptExporter(ILogger<DatabaseConverter> logger)
{
    public static bool Supports(DatabaseProvider target) => target is DatabaseProvider.SqlServer or DatabaseProvider.Postgres;

    public async Task<IReadOnlyList<ConvertedTable>> WriteAsync(DatabaseEndpoint source, DatabaseProvider target, TextWriter output, CancellationToken cancellationToken = default)
    {
        var script = ScriptDialect.For(target);
        await using (var sourceContext = DatabaseConverter.NewContext(source))
            await DatabaseConverter.CheckSourceAsync(sourceContext, cancellationToken);

        // A context for the target's server that never connects: its model and migrations only.
        await using var targetContext = DatabaseConverter.NewContext(new DatabaseEndpoint(target, script.ModelConnectionString));
        var tables = ConversionTables.Order(targetContext);
        var targetColumns = TargetColumns(targetContext, target);
        var schema = targetContext.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.NoTransactions);
        var lastMigration = targetContext.Database.GetMigrations().Last();

        var converter = new DatabaseConverter(logger);
        await using var connection = DatabaseConverter.Connect(source);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await converter.BeginSourceTransactionAsync(source.Provider, connection, cancellationToken);
        var dialect = SqlDialect.For(source.Provider);
        var plan = tables.Plan(await dialect.ReadColumnsAsync(connection, transaction, cancellationToken), targetColumns);

        await script.BeginAsync(output, source.Provider, lastMigration, schema, plan);
        var results = new List<ConvertedTable>();
        var deferred = new List<(TablePlan Table, List<object?[]> Rows)>();
        foreach (var table in plan)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = await DatabaseConverter.ReadRowsAsync(dialect, connection, transaction, table, cancellationToken);
            await script.InsertAsync(output, table, table.OrderForInsert(rows).Select(table.WithoutDeferred).ToList());
            if (table.Deferred.Length > 0)
                deferred.Add((table, table.WithDeferred(rows)));
            logger.LogInformation("Scripted {Rows} rows of {Table}", rows.Count, table.Name);
            results.Add(new ConvertedTable(table.Name.Schema, table.Name.Name, rows.Count));
        }
        foreach (var (table, rows) in deferred.Where(d => d.Rows.Count > 0))
            await script.SetDeferredAsync(output, table, rows);
        await script.EndAsync(output, plan);
        await output.FlushAsync(cancellationToken);
        logger.LogInformation("Scripted {Tables} tables, {Rows} rows, for {Target}", results.Count, results.Sum(r => r.Rows), target);
        return results;
    }

    /// <summary>
    /// The target's tables and columns as its catalog would list them once migrated (SqlDialect.ReadColumnsAsync): the
    /// model's tables, with each column's store type and whether the server generates it; on SQL Server, each temporal
    /// table's history table too, with the same columns.
    /// </summary>
    internal static Dictionary<TableName, List<ColumnInfo>> TargetColumns(GrifballContext target, DatabaseProvider provider)
    {
        var names = SqlDialect.For(provider);
        var model = target.GetService<IDesignTimeModel>().Model;
        var tables = new Dictionary<TableName, List<ColumnInfo>>();
        foreach (var table in model.GetRelationalModel().Tables)
            tables[new TableName(table.Schema ?? "", table.Name)] = table.Columns
                .Select(c => new ColumnInfo(c.Name, c.StoreType, Generated: c.ComputedColumnSql is not null, Identity: false))
                .ToList();
        if (provider == DatabaseProvider.SqlServer)
        {
            foreach (var entity in model.GetEntityTypes().Where(e => e.IsTemporal() && e.GetTableName() is not null))
            {
                // A schema the model leaves unset is the default one, as ConversionTables names it ("").
                var current = new TableName($"{entity.GetSchema()}", entity.GetTableName()!);
                var history = names.Table($"{entity.GetHistoryTableSchema()}", entity.GetHistoryTableName()!);
                tables.TryAdd(history, tables[current].Select(c => c with { Generated = false }).ToList());
            }
        }
        return tables;
    }
}

/// <summary>How each target server's script is written: its statements around the rows, and its literals.</summary>
internal abstract class ScriptDialect
{
    public static ScriptDialect For(DatabaseProvider target) => target switch
    {
        DatabaseProvider.SqlServer => new SqlServerScript(),
        DatabaseProvider.Postgres => new PostgresScript(),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Scripts are for SQL Server and PostgreSQL; SQLite's backup is a file."),
    };

    /// <summary>Rows per INSERT (SQL Server takes at most 1000 in one VALUES).</summary>
    protected const int RowsPerInsert = 500;

    protected abstract SqlDialect Names { get; }

    /// <summary>For the model-only context the target's migrations are scripted from; never opened.</summary>
    public abstract string ModelConnectionString { get; }

    public abstract Task BeginAsync(TextWriter output, DatabaseProvider source, string lastMigration, string schema, IReadOnlyList<TablePlan> plan);

    public abstract Task InsertAsync(TextWriter output, TablePlan table, IReadOnlyList<object?[]> rows);

    public abstract Task EndAsync(TextWriter output, IReadOnlyList<TablePlan> plan);

    /// <summary>A value read from any server, as a literal of this one's, for a column of the given store type.</summary>
    public abstract string Literal(object? value, ColumnInfo column);

    protected static string Header(string comment, DatabaseProvider source, string lastMigration, string restore) => $"""
        {comment} GrifballWebApp database export: a {source} database, as this server's script.
        {comment} Migrations up to {lastMigration}. Written {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC.
        {comment} Restore into a new, empty database (it refuses one with tables), all in one transaction:
        {comment}   {restore}
        """;

    /// <summary>INSERTs of the rows, the columns the target generates left out; each statement followed by <paramref name="separator"/>.</summary>
    protected async Task WriteInsertsAsync(TextWriter output, TablePlan table, IReadOnlyList<object?[]> rows, string separator)
    {
        var columns = table.Columns.Select((c, i) => (Column: c, Index: i)).Where(c => !c.Column.Generated).ToList();
        var insert = $"INSERT INTO {Names.Quote(table.Name)} ({string.Join(", ", columns.Select(c => Names.Quote(c.Column.Name)))}) VALUES";
        foreach (var chunk in rows.Chunk(RowsPerInsert))
        {
            await output.WriteLineAsync(insert);
            for (var r = 0; r < chunk.Length; r++)
            {
                var row = chunk[r];
                await output.WriteLineAsync($"  ({string.Join(", ", columns.Select(c => Literal(row[c.Index], c.Column)))}){(r == chunk.Length - 1 ? ";" : ",")}");
            }
            await output.WriteLineAsync(separator);
        }
    }

    /// <summary>Sets the deferred foreign keys (TablePlan.Deferred) of rows already in, one UPDATE each, by key.</summary>
    public virtual async Task SetDeferredAsync(TextWriter output, TablePlan table, IReadOnlyList<object?[]> rows)
    {
        foreach (var row in rows)
        {
            var set = string.Join(", ", table.Deferred.Select(i => $"{Names.Quote(table.Columns[i].Name)} = {Literal(row[i], table.Columns[i])}"));
            var where = string.Join(" AND ", table.Key.Select(i => $"{Names.Quote(table.Columns[i].Name)} = {Literal(row[i], table.Columns[i])}"));
            await output.WriteLineAsync($"UPDATE {Names.Quote(table.Name)} SET {set} WHERE {where};");
        }
    }

    protected static string Number(object value) => Convert.ToString(value, CultureInfo.InvariantCulture)!;

    protected static string Real(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}

/// <summary>
/// SQL Server: sqlcmd batches (GO), each starting with a check that stops the rest (SET NOEXEC ON) once the
/// transaction is gone, so a failure part way, which rolls everything back (XACT_ABORT), leaves nothing half done even
/// without sqlcmd -b. Temporal tables are loaded as SqlServerTargetWriter loads them, from the restored server's
/// catalog: system versioning off, the period dropped, the period columns (and the indexes on them) dropped and added
/// back as plain datetime2, the rows in, then the period, its hidden columns, the indexes and system versioning back.
/// Identity columns take their values (IDENTITY_INSERT) and carry on after the highest.
/// </summary>
internal sealed class SqlServerScript : ScriptDialect
{
    protected override SqlDialect Names => SqlServerDialect.Instance;

    public override string ModelConnectionString => "Server=localhost;Database=GrifballWebApp";

    private const string StopUnlessInTransaction = "IF @@TRANCOUNT = 0 SET NOEXEC ON;";
    private const string Go = "GO\n" + StopUnlessInTransaction;

    /// <summary>T-SQL that runs each statement the query gives (columns Ord, Statement), one at a time, in order.</summary>
    private static string RunEach(string query) => $"""
        DECLARE @statement nvarchar(max);
        DECLARE statements CURSOR LOCAL FAST_FORWARD FOR SELECT Statement FROM ({query}) q ORDER BY Ord;
        OPEN statements;
        FETCH NEXT FROM statements INTO @statement;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            EXEC sys.sp_executesql @statement;
            FETCH NEXT FROM statements INTO @statement;
        END;
        CLOSE statements;
        DEALLOCATE statements;
        """;

    private const string Temporal = """
        CREATE TABLE #grif_temporal (Id int IDENTITY PRIMARY KEY, TableName nvarchar(300) NOT NULL, HistoryName nvarchar(300) NOT NULL,
            StartColumn sysname NOT NULL, EndColumn sysname NOT NULL, Scale tinyint NOT NULL, StartHidden bit NOT NULL, EndHidden bit NOT NULL);
        INSERT INTO #grif_temporal (TableName, HistoryName, StartColumn, EndColumn, Scale, StartHidden, EndHidden)
        SELECT QUOTENAME(s.name) + N'.' + QUOTENAME(t.name), QUOTENAME(hs.name) + N'.' + QUOTENAME(h.name), ps.name, pe.name, ps.scale, ps.is_hidden, pe.is_hidden
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.tables h ON h.object_id = t.history_table_id
        JOIN sys.schemas hs ON hs.schema_id = h.schema_id
        JOIN sys.periods p ON p.object_id = t.object_id
        JOIN sys.columns ps ON ps.object_id = t.object_id AND ps.column_id = p.start_column_id
        JOIN sys.columns pe ON pe.object_id = t.object_id AND pe.column_id = p.end_column_id
        WHERE t.temporal_type = 2
        ORDER BY s.name, t.name;
        -- The indexes on the period columns, of both tables, each with the statements that drop and make it.
        CREATE TABLE #grif_indexes (Id int IDENTITY PRIMARY KEY, DropIndex nvarchar(max) NOT NULL, CreateIndex nvarchar(max) NOT NULL);
        INSERT INTO #grif_indexes (DropIndex, CreateIndex)
        SELECT N'DROP INDEX ' + QUOTENAME(i.name) + N' ON ' + x.TableName,
            N'CREATE ' + CASE WHEN i.is_unique = 1 THEN N'UNIQUE ' ELSE N'' END
            + CASE WHEN i.type_desc = N'CLUSTERED' THEN N'CLUSTERED ' ELSE N'NONCLUSTERED ' END
            + N'INDEX ' + QUOTENAME(i.name) + N' ON ' + x.TableName + N' ('
            + (SELECT STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(c.name) + CASE WHEN ic.is_descending_key = 1 THEN N' DESC' ELSE N' ASC' END), N', ')
                   WITHIN GROUP (ORDER BY ic.key_ordinal)
               FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
               WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0) + N')'
            + ISNULL(N' INCLUDE (' + (SELECT STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(c.name)), N', ') WITHIN GROUP (ORDER BY ic.index_column_id)
               FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
               WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1) + N')', N'')
            + ISNULL(N' WHERE ' + i.filter_definition, N'')
        FROM #grif_temporal g
        CROSS APPLY (VALUES (g.TableName), (g.HistoryName)) x(TableName)
        JOIN sys.indexes i ON i.object_id = OBJECT_ID(x.TableName)
        WHERE i.type > 0 AND i.is_primary_key = 0 AND i.is_unique_constraint = 0
            AND EXISTS (SELECT 1 FROM sys.index_columns pic JOIN sys.columns pc ON pc.object_id = pic.object_id AND pc.column_id = pic.column_id
                        WHERE pic.object_id = i.object_id AND pic.index_id = i.index_id AND pc.name IN (g.StartColumn, g.EndColumn));
        """;

    public override async Task BeginAsync(TextWriter output, DatabaseProvider source, string lastMigration, string schema, IReadOnlyList<TablePlan> plan)
    {
        await output.WriteLineAsync(Header("--", source, lastMigration, "sqlcmd -S <server> -d <new database> -b -i <this file>"));
        await output.WriteLineAsync("""
            SET NOCOUNT ON;
            SET XACT_ABORT ON;
            GO
            IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0)
            BEGIN
                RAISERROR(N'This database already has tables: restore into a new, empty database.', 16, 1);
                SET NOEXEC ON;
            END;
            GO
            BEGIN TRANSACTION;
            GO
            """);
        await output.WriteLineAsync(StopUnlessInTransaction);
        // The target's migrations: its schema. Its own batches, each checked as the others.
        foreach (var line in schema.ReplaceLineEndings("\n").Split('\n'))
            await output.WriteLineAsync(line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase) ? Go : line);
        await output.WriteLineAsync(Go);

        await output.WriteLineAsync("-- Temporal tables: system versioning off and the period dropped while the rows go in.");
        await output.WriteLineAsync(Temporal);
        await output.WriteLineAsync(RunEach("""
            SELECT Id * 2 AS Ord, N'ALTER TABLE ' + TableName + N' SET (SYSTEM_VERSIONING = OFF)' AS Statement FROM #grif_temporal
            UNION ALL SELECT Id * 2 + 1, N'ALTER TABLE ' + TableName + N' DROP PERIOD FOR SYSTEM_TIME' FROM #grif_temporal
            """));
        await output.WriteLineAsync(Go);

        // The migrations' seed rows: the source has its own. Children first.
        foreach (var table in plan.Reverse())
            await output.WriteLineAsync($"DELETE FROM {Names.Quote(table.Name)};");
        await output.WriteLineAsync(Go);

        await output.WriteLineAsync("-- The period columns as plain columns, which take values, and the indexes on them dropped meanwhile.");
        await output.WriteLineAsync(RunEach("""
            SELECT Id AS Ord, DropIndex AS Statement FROM #grif_indexes
            UNION ALL SELECT 1000000 + g.Id * 4 + x.n, N'ALTER TABLE ' + x.TableName + N' DROP COLUMN ' + QUOTENAME(g.StartColumn) + N', ' + QUOTENAME(g.EndColumn)
                FROM #grif_temporal g CROSS APPLY (VALUES (0, g.TableName), (2, g.HistoryName)) x(n, TableName)
            UNION ALL SELECT 1000000 + g.Id * 4 + x.n + 1, N'ALTER TABLE ' + x.TableName + N' ADD ' + QUOTENAME(g.StartColumn) + N' datetime2(' + CONVERT(nvarchar(2), g.Scale) + N') NOT NULL, '
                    + QUOTENAME(g.EndColumn) + N' datetime2(' + CONVERT(nvarchar(2), g.Scale) + N') NOT NULL'
                FROM #grif_temporal g CROSS APPLY (VALUES (0, g.TableName), (2, g.HistoryName)) x(n, TableName)
            """));
        await output.WriteLineAsync(Go);
    }

    public override async Task InsertAsync(TextWriter output, TablePlan table, IReadOnlyList<object?[]> rows)
    {
        if (rows.Count == 0)
            return;
        var name = Names.Quote(table.Name);
        var hasIdentity = $"OBJECTPROPERTY(OBJECT_ID(N'{name.Replace("'", "''")}'), 'TableHasIdentity') = 1";
        await output.WriteLineAsync($"IF {hasIdentity} SET IDENTITY_INSERT {name} ON;");
        await WriteInsertsAsync(output, table, rows, Go);
        await output.WriteLineAsync($"IF {hasIdentity} SET IDENTITY_INSERT {name} OFF;");
        // The identity carries on after the highest value written.
        await output.WriteLineAsync($"IF {hasIdentity} DBCC CHECKIDENT (N'{name.Replace("'", "''")}', RESEED) WITH NO_INFOMSGS;");
        await output.WriteLineAsync(Go);
    }

    public override async Task SetDeferredAsync(TextWriter output, TablePlan table, IReadOnlyList<object?[]> rows)
    {
        await base.SetDeferredAsync(output, table, rows);
        await output.WriteLineAsync(Go);
    }

    public override async Task EndAsync(TextWriter output, IReadOnlyList<TablePlan> plan)
    {
        await output.WriteLineAsync("-- Temporal tables back: the period, its hidden columns, the indexes, then system versioning, checking the history.");
        await output.WriteLineAsync(RunEach("""
            SELECT Id * 4 AS Ord, N'ALTER TABLE ' + TableName + N' ADD PERIOD FOR SYSTEM_TIME (' + QUOTENAME(StartColumn) + N', ' + QUOTENAME(EndColumn) + N')' AS Statement FROM #grif_temporal
            UNION ALL SELECT Id * 4 + 1, N'ALTER TABLE ' + TableName + N' ALTER COLUMN ' + QUOTENAME(StartColumn) + N' ADD HIDDEN' FROM #grif_temporal WHERE StartHidden = 1
            UNION ALL SELECT Id * 4 + 2, N'ALTER TABLE ' + TableName + N' ALTER COLUMN ' + QUOTENAME(EndColumn) + N' ADD HIDDEN' FROM #grif_temporal WHERE EndHidden = 1
            UNION ALL SELECT 1000000 + Id, CreateIndex FROM #grif_indexes
            UNION ALL SELECT 2000000 + Id, N'ALTER TABLE ' + TableName + N' SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + HistoryName + N', DATA_CONSISTENCY_CHECK = ON))' FROM #grif_temporal
            """));
        await output.WriteLineAsync("""
            DROP TABLE #grif_indexes;
            DROP TABLE #grif_temporal;
            COMMIT TRANSACTION;
            GO
            SET NOEXEC OFF;
            GO
            """);
    }

    public override string Literal(object? value, ColumnInfo column) => SqlServerTargetWriter.ToSqlServer(value, column) switch
    {
        null => "NULL",
        bool b => b ? "1" : "0",
        string s => Unicode(s),
        Guid g => $"'{g}'",
        byte[] bytes => "0x" + Convert.ToHexString(bytes),
        DateTime d when column.DataType.StartsWith("datetime2", StringComparison.OrdinalIgnoreCase) || column.DataType.StartsWith("datetimeoffset", StringComparison.OrdinalIgnoreCase)
            => $"'{d.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture)}'",
        // datetime and smalldatetime read three fractional digits at most
        DateTime d => $"'{d.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)}'",
        DateTimeOffset o => $"'{o.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture)}'",
        DateOnly d => $"'{d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}'",
        // A TimeOnly too: ToSqlServer makes it a TimeSpan.
        TimeSpan t => $"'{t.ToString(@"hh\:mm\:ss\.fffffff", CultureInfo.InvariantCulture)}'",
        double d => Real(d),
        float f => Real(f),
        // The other numbers (decimal, the integers); every other IFormattable the drivers give is matched above.
        IFormattable number => Number(number),
        var other => Unicode(Number(other)),
    };

    /// <summary>
    /// An nvarchar literal, kept on one line: a line break is NCHAR(13) or NCHAR(10), so no text can end a batch (a
    /// line of its own reading GO). Joined as nvarchar(max), so no part cuts the text at 4000 characters.
    /// </summary>
    internal static string Unicode(string s)
    {
        if (s.IndexOfAny(['\r', '\n']) < 0)
            return $"N'{s.Replace("'", "''")}'";
        var parts = new List<string>();
        var start = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] is not ('\r' or '\n'))
                continue;
            parts.Add($"N'{s[start..i].Replace("'", "''")}'");
            parts.Add(s[i] == '\r' ? "NCHAR(13)" : "NCHAR(10)");
            start = i + 1;
        }
        parts.Add($"N'{s[start..].Replace("'", "''")}'");
        return $"CONVERT(nvarchar(max), {string.Join(" + ", parts)})";
    }
}

/// <summary>
/// PostgreSQL: one transaction. Row history's triggers off while the rows go in, as PostgresTargetWriter does, so the
/// rows keep their PeriodStart and the history tables take the source's; PeriodEnd, which the tables generate, left to
/// them. Identity columns' sequences then carry on after the highest value, and ANALYZE gives the planner the tables'
/// statistics.
/// </summary>
internal sealed class PostgresScript : ScriptDialect
{
    protected override SqlDialect Names => PostgresDialect.Instance;

    public override string ModelConnectionString => "Host=localhost;Database=GrifballWebApp";

    public override async Task BeginAsync(TextWriter output, DatabaseProvider source, string lastMigration, string schema, IReadOnlyList<TablePlan> plan)
    {
        await output.WriteLineAsync(Header("--", source, lastMigration, "psql -v ON_ERROR_STOP=1 -d <new database> -f <this file>"));
        await output.WriteLineAsync("""
            SET client_encoding = 'UTF8';
            SET standard_conforming_strings = on;
            BEGIN;
            DO $grif$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog', 'information_schema')) THEN
                    RAISE EXCEPTION 'This database already has tables: restore into a new, empty database.';
                END IF;
            END $grif$;
            """);
        // The target's migrations: its schema.
        await output.WriteLineAsync(schema);
        foreach (var table in plan)
            await output.WriteLineAsync($"ALTER TABLE {Names.Quote(table.Name)} DISABLE TRIGGER USER;");
        // The migrations' seed rows: the source has its own. Children first.
        foreach (var table in plan.Reverse())
            await output.WriteLineAsync($"DELETE FROM {Names.Quote(table.Name)};");
    }

    public override async Task InsertAsync(TextWriter output, TablePlan table, IReadOnlyList<object?[]> rows)
    {
        if (rows.Count > 0)
            await WriteInsertsAsync(output, table, rows, "");
    }

    public override async Task EndAsync(TextWriter output, IReadOnlyList<TablePlan> plan)
    {
        foreach (var table in plan)
            await output.WriteLineAsync($"ALTER TABLE {Names.Quote(table.Name)} ENABLE TRIGGER USER;");
        await output.WriteLineAsync("""
            -- Each identity column's sequence carries on after the highest value (an empty table's is left as it is).
            DO $grif$
            DECLARE c record;
            BEGIN
                FOR c IN SELECT table_schema, table_name, column_name FROM information_schema.columns
                         WHERE is_identity = 'YES' AND table_schema NOT IN ('pg_catalog', 'information_schema')
                LOOP
                    EXECUTE format('SELECT setval(pg_get_serial_sequence(%L, %L), max(%I)) FROM %I.%I',
                        format('%I.%I', c.table_schema, c.table_name), c.column_name, c.column_name, c.table_schema, c.table_name);
                END LOOP;
            END $grif$;
            COMMIT;
            ANALYZE;
            """);
    }

    public override string Literal(object? value, ColumnInfo column) => PostgresTargetWriter.ToPostgres(value, column) switch
    {
        null => "NULL",
        bool b => b ? "TRUE" : "FALSE",
        string s => Text(s),
        Guid g => $"'{g}'",
        byte[] bytes => $"'\\x{Convert.ToHexString(bytes)}'",
        DateTime d when d == DateTime.MaxValue => "'infinity'",
        DateTime d when d == DateTime.MinValue => "'-infinity'",
        DateTime d => $"'{d.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture)}+00'",
        DateOnly d => $"'{d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}'",
        TimeSpan t when column.DataType == "interval" => Interval(t),
        TimeSpan t => $"'{t.ToString(@"hh\:mm\:ss\.ffffff", CultureInfo.InvariantCulture)}'",
        TimeOnly t => $"'{t.ToString("HH:mm:ss.ffffff", CultureInfo.InvariantCulture)}'",
        double d when double.IsNaN(d) => "'NaN'",
        double d when double.IsInfinity(d) => d > 0 ? "'Infinity'" : "'-Infinity'",
        float f when float.IsNaN(f) => "'NaN'",
        float f when float.IsInfinity(f) => f > 0 ? "'Infinity'" : "'-Infinity'",
        double d => Real(d),
        float f => Real(f),
        // The other numbers (decimal, the integers); every other IFormattable the drivers give is matched above.
        IFormattable number => Number(number),
        var other => Text(Number(other)),
    };

    internal static string Text(string s) => $"'{s.Replace("'", "''")}'";

    /// <summary>An interval as ISO 8601 (P1DT2H3M4.5S), which keeps days apart from hours as TimeSpan does.</summary>
    internal static string Interval(TimeSpan t)
    {
        var sign = t < TimeSpan.Zero ? "-" : "";
        var a = t.Duration();
        var seconds = (a.Ticks % TimeSpan.TicksPerMinute) / (decimal)TimeSpan.TicksPerSecond;
        return $"'{sign}P{a.Days}DT{a.Hours}H{a.Minutes}M{seconds.ToString("0.######", CultureInfo.InvariantCulture)}S'";
    }
}
