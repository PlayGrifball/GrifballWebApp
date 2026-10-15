using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GrifballWebApp.Database.Conversion;

/// <summary>A database: its server's kind and the connection string to it.</summary>
public sealed record DatabaseEndpoint(DatabaseProvider Provider, string ConnectionString);

/// <summary>A table the conversion copied, and how many rows.</summary>
public sealed record ConvertedTable(string Schema, string Name, int Rows);

/// <summary>Why a conversion refused to run or stopped; the message says what to do.</summary>
public sealed class ConversionException(string message) : Exception(message);

/// <summary>
/// Copies a GrifballWebApp database into a new one, on SQL Server or PostgreSQL either way round (or
/// the same kind). The target is made by the app's own migrations, so it has exactly the schema the app
/// expects on its server; then every table's rows are copied into it, row history included, with the
/// times each version took effect and ended (SQL Server's temporal history tables, Postgres's row
/// history: PostgresHistory). Keys keep their values, and identity columns carry on after the highest.
///
/// It refuses unless the source has every migration of this build and no others (so both have the same
/// tables and columns), and unless the target has no tables yet. All of the copy is one transaction on
/// the target, checked before it commits: every table's rows, read back from the target, must be the
/// rows read from the source (Postgres keeps microseconds, SQL Server 100ns: times are compared to the
/// microsecond). If anything fails, the target is put back as it was: dropped when the conversion created
/// it, or, when it was there already (an empty database made beforehand), its migrations undone, so it
/// has no tables again and the next conversion takes it.
///
/// The source is only read. On Postgres, and on SQL Server with snapshot isolation allowed, it's read in
/// one snapshot; otherwise nothing should write to it while this runs (a change part way through can
/// fail the copy or the check, never pass a wrong one).
/// </summary>
public sealed class DatabaseConverter(ILogger<DatabaseConverter> logger)
{
    public async Task<IReadOnlyList<ConvertedTable>> ConvertAsync(DatabaseEndpoint source, DatabaseEndpoint target, CancellationToken cancellationToken = default)
    {
        await using (var sourceContext = NewContext(source))
            await CheckSourceAsync(sourceContext, cancellationToken);

        await using var targetContext = NewContext(target);
        var created = await PrepareTargetAsync(targetContext, cancellationToken);
        try
        {
            var tables = ConversionTables.Order(targetContext);
            return await CopyAsync(source, target, tables, cancellationToken);
        }
        catch (Exception e) when (created)
        {
            logger.LogError("The conversion failed ({Message}); dropping the target database it created", e.Message);
            ClearPools(target);
            await targetContext.Database.EnsureDeletedAsync(CancellationToken.None);
            throw;
        }
        catch (Exception e)
        {
            logger.LogError("The conversion failed ({Message}); the target database was there before it: undoing its migrations", e.Message);
            await UndoMigrationsAsync(targetContext);
            throw;
        }
    }

    internal static GrifballContext NewContext(DatabaseEndpoint endpoint)
    {
        return new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(endpoint.Provider, endpoint.ConnectionString)
            .Options);
    }

    private static async Task CheckSourceAsync(GrifballContext source, CancellationToken cancellationToken)
    {
        if (!await source.GetService<IRelationalDatabaseCreator>().ExistsAsync(cancellationToken))
            throw new ConversionException("The source database doesn't exist.");

        var applied = (await source.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        var known = source.Database.GetMigrations().ToList();
        var pending = known.Except(applied).ToList();
        var unknown = applied.Except(known).ToList();
        if (unknown.Count > 0)
            throw new ConversionException($"The source has migrations this build doesn't ({string.Join(", ", unknown)}): convert it with the build that made them.");
        if (pending.Count > 0)
            throw new ConversionException($"The source lacks migrations of this build ({string.Join(", ", pending)}): migrate it first, or convert it with its own build.");
    }

    /// <summary>
    /// Migrates the target, which must have no tables yet (missing, or empty). True if the conversion
    /// created the database.
    /// </summary>
    private async Task<bool> PrepareTargetAsync(GrifballContext target, CancellationToken cancellationToken)
    {
        var exists = await target.GetService<IRelationalDatabaseCreator>().ExistsAsync(cancellationToken);
        if (exists && await target.GetService<IRelationalDatabaseCreator>().HasTablesAsync(cancellationToken))
            throw new ConversionException("The target database already has tables: the conversion only fills a new database. Drop it, or give it another.");

        logger.LogInformation("Applying the migrations to the target ({Count})", target.Database.GetMigrations().Count());
        try
        {
            await target.Database.MigrateAsync(cancellationToken);
        }
        catch when (!exists)
        {
            await target.Database.EnsureDeletedAsync(CancellationToken.None);
            throw;
        }
        catch
        {
            await UndoMigrationsAsync(target);
            throw;
        }
        return !exists;
    }

    /// <summary>
    /// Back to no tables: a database that was there before the conversion, empty, gets its migrations undone
    /// (their Down, to migration 0) rather than left with the migrations' tables and no data, which would
    /// look like a converted database.
    /// </summary>
    private async Task UndoMigrationsAsync(GrifballContext target)
    {
        try
        {
            await target.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase, CancellationToken.None);
            // EF keeps its history table, empty, after the last Down.
            await target.Database.ExecuteSqlRawAsync(target.Database.IsNpgsql()
                ? "DROP TABLE IF EXISTS public.\"__EFMigrationsHistory\""
                : "DROP TABLE IF EXISTS dbo.__EFMigrationsHistory", CancellationToken.None);
            logger.LogInformation("Undid the target's migrations: it has no tables again");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Undoing the target's migrations failed: it has tables and no data, so drop it, or empty it, before trying again");
        }
    }

    private async Task<IReadOnlyList<ConvertedTable>> CopyAsync(DatabaseEndpoint source, DatabaseEndpoint target, ConversionTables tables, CancellationToken cancellationToken)
    {
        await using var sourceConnection = Open(source);
        await sourceConnection.OpenAsync(cancellationToken);
        await using var sourceTransaction = await BeginSourceTransactionAsync(source.Provider, sourceConnection, cancellationToken);
        var sourceDialect = SqlDialect.For(source.Provider);

        await using var targetConnection = Open(target);
        await targetConnection.OpenAsync(cancellationToken);
        await using var targetTransaction = await targetConnection.BeginTransactionAsync(cancellationToken);
        var targetDialect = SqlDialect.For(target.Provider);

        var sourceColumns = await sourceDialect.ReadColumnsAsync(sourceConnection, sourceTransaction, cancellationToken);
        var targetColumns = await targetDialect.ReadColumnsAsync(targetConnection, targetTransaction, cancellationToken);
        var plan = tables.Plan(sourceColumns, targetColumns);

        await using var writer = TargetWriter.For(target.Provider, targetConnection, targetTransaction, logger);
        await writer.PrepareAsync(plan, cancellationToken);

        var results = new List<ConvertedTable>();
        var fingerprints = new Dictionary<TableName, TableFingerprint>();
        var deferred = new List<(TablePlan Table, List<object?[]> Rows)>();
        foreach (var table in plan)
        {
            var rows = await ReadRowsAsync(sourceDialect, sourceConnection, sourceTransaction, table, cancellationToken);
            fingerprints[table.Name] = TableFingerprint.Of(rows);
            var ordered = table.OrderForInsert(rows);
            await writer.WriteAsync(table, ordered.Select(table.WithoutDeferred).ToList(), cancellationToken);
            if (table.Deferred.Length > 0)
                deferred.Add((table, table.WithDeferred(rows)));
            logger.LogInformation("Copied {Rows} rows of {Table}", rows.Count, table.Name);
            results.Add(new ConvertedTable(table.Name.Schema, table.Name.Name, rows.Count));
        }

        // The foreign keys that close a cycle between tables, now that every row they point at is in.
        foreach (var (table, rows) in deferred.Where(d => d.Rows.Count > 0))
        {
            await writer.SetDeferredAsync(table, rows, cancellationToken);
            logger.LogInformation("Set the deferred foreign keys of {Rows} rows of {Table}", rows.Count, table.Name);
        }

        await writer.FinishAsync(cancellationToken);

        // Everything the target now holds, read back the way the app reads it, against what was read
        // from the source.
        var mismatches = new List<string>();
        foreach (var table in plan)
        {
            var rows = await ReadRowsAsync(targetDialect, targetConnection, targetTransaction, table, cancellationToken);
            var copied = TableFingerprint.Of(rows);
            var expected = fingerprints[table.Name];
            if (copied != expected)
                mismatches.Add($"{table.Name} ({expected.Rows} rows in the source, {copied.Rows} in the target{(copied.Rows == expected.Rows ? ", with different values" : "")})");
        }
        if (mismatches.Count > 0)
            throw new ConversionException($"The target's rows aren't the source's in {string.Join("; ", mismatches)}. Nothing was committed.");

        await targetTransaction.CommitAsync(cancellationToken);
        await writer.AfterCommitAsync(cancellationToken);
        logger.LogInformation("Converted {Tables} tables, {Rows} rows, and checked them", results.Count, results.Sum(r => r.Rows));
        return results;
    }

    private static DbConnection Open(DatabaseEndpoint endpoint) => endpoint.Provider switch
    {
        DatabaseProvider.SqlServer => new SqlConnection(endpoint.ConnectionString),
        DatabaseProvider.Postgres => new NpgsqlConnection(endpoint.ConnectionString),
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint.Provider, null),
    };

    /// <summary>Closes the pooled connections to the target, which dropping it would otherwise wait on.</summary>
    private static void ClearPools(DatabaseEndpoint endpoint)
    {
        if (endpoint.Provider == DatabaseProvider.Postgres)
            NpgsqlConnection.ClearPool(new NpgsqlConnection(endpoint.ConnectionString));
        else
            SqlConnection.ClearPool(new SqlConnection(endpoint.ConnectionString));
    }

    /// <summary>
    /// One snapshot of the source for the whole copy where the server offers one without blocking its
    /// writers: Postgres always (repeatable read, read only), SQL Server when snapshot isolation is
    /// allowed on the database.
    /// </summary>
    private async Task<DbTransaction?> BeginSourceTransactionAsync(DatabaseProvider provider, DbConnection connection, CancellationToken cancellationToken)
    {
        if (provider == DatabaseProvider.Postgres)
        {
            var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SET TRANSACTION READ ONLY";
            await command.ExecuteNonQueryAsync(cancellationToken);
            return transaction;
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT snapshot_isolation_state FROM sys.databases WHERE name = DB_NAME()";
            if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1)
                return await connection.BeginTransactionAsync(IsolationLevel.Snapshot, cancellationToken);
        }
        logger.LogWarning("The source SQL Server database doesn't allow snapshot isolation, so it's read table by table: nothing should write to it during the conversion");
        return null;
    }

    private static async Task<List<object?[]>> ReadRowsAsync(SqlDialect dialect, DbConnection connection, DbTransaction? transaction, TablePlan table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 0;
        command.CommandText = $"SELECT {string.Join(", ", table.Columns.Select(c => dialect.Quote(c.Name)))} FROM {dialect.Quote(table.Name)}";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new object?[table.Columns.Count];
            for (var i = 0; i < row.Length; i++)
                row[i] = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }
}

/// <summary>
/// A table's schema and name, as both servers name it. Schema empty: the server's default schema, which
/// each names its own way (dbo, public): the model's tables without a schema.
/// </summary>
public readonly record struct TableName(string Schema, string Name)
{
    public override string ToString() => Schema.Length == 0 ? Name : $"{Schema}.{Name}";
}

/// <summary>A column as the server describes it.</summary>
/// <param name="DataType">SQL Server's type name (DATA_TYPE), or Postgres's (udt_name).</param>
/// <param name="Generated">The server computes it: never written (Postgres's generated PeriodEnd).</param>
/// <param name="Identity">An identity column, which the conversion writes and then carries on after.</param>
internal sealed record ColumnInfo(string Name, string DataType, bool Generated, bool Identity);

