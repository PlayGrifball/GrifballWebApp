using System.Data.Common;
using System.Text;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Conversion;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GrifballWebApp.Server.DatabaseBackup;

/// <summary>
/// What a download is: a SQLite database file; a SQL script for PostgreSQL or SQL Server; or a native backup, SQL
/// Server's .bak or PostgreSQL's pg_dump archive. Each from whichever server the app is on: a native backup of the other
/// kind is made on the scratch server (backupScratch).
/// </summary>
public enum BackupFormat { Sqlite, Postgres, SqlServer, Bak, Dump }

/// <summary>A finished backup: the file, deleted once the stream is closed, its name and type, and the tables it holds (empty for a native backup).</summary>
public sealed record BackupFile(FileStream File, string FileName, string ContentType, IReadOnlyList<ConvertedTable> Tables);

public enum ExportState { None, Running, Succeeded, Failed }

/// <summary>The last export to a server: never its connection string.</summary>
public sealed record BackupExportStatus(ExportState State, string? Provider = null, DateTimeOffset? Started = null, DateTimeOffset? Finished = null,
    int Tables = 0, int Rows = 0, string? Message = null);

/// <summary>
/// The app's database, whichever server it's on, as a download (<see cref="BackupFormat"/>): copied into a new SQLite
/// file by the converter (DatabaseConverter), every table, row history included, checked against the source before
/// it's handed over; written as a script that builds it again on PostgreSQL or SQL Server (DatabaseScriptExporter); or
/// a server's native backup (NativeBackups): the app's own server's, or, of the other kind, the scratch server's
/// (DatabaseBackup:Scratch), backing up a temporary copy of the database made there by the converter and dropped after.
/// Each is written to the temp folder and deleted when the download's stream closes.
/// Or the database copied straight into a new one on another server, any of the three (<see cref="StartExport"/>), in
/// the background: the converter creates it, or takes an empty one, and drops it again (or undoes its migrations) if
/// anything fails. One backup or export at a time: the copy holds a table's rows in memory. The export's status is
/// this process's (backend.replicas is 1).
/// </summary>
public sealed class DatabaseBackupService(IConfiguration configuration, ILoggerFactory loggerFactory, TimeProvider timeProvider,
    IHostApplicationLifetime lifetime)
{
    private readonly SemaphoreSlim _running = new(1, 1);
    private readonly ILogger _logger = loggerFactory.CreateLogger<DatabaseBackupService>();
    private volatile BackupExportStatus _export = new(ExportState.None);

    public BackupExportStatus Export => _export;

    private DatabaseEndpoint Source() => new(configuration.GetDatabaseProvider(),
        configuration.GetConnectionString("GrifballWebApp") ?? throw new InvalidOperationException("ConnectionStrings:GrifballWebApp is not set"));

    private DatabaseConverter Converter() => new(loggerFactory.CreateLogger<DatabaseConverter>());

    /// <summary>The scratch server (the chart's backupScratch), as its administrator; null when there's none.</summary>
    private DatabaseEndpoint? Scratch()
    {
        var connectionString = configuration["DatabaseBackup:Scratch:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;
        var provider = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Provider"] = configuration["DatabaseBackup:Scratch:Provider"] })
            .Build()
            .GetDatabaseProvider();
        return new DatabaseEndpoint(provider, connectionString);
    }

    /// <summary>Where a native backup of the server can be made: the app's own server, else the scratch server; null for neither.</summary>
    private DatabaseEndpoint? NativeSource(DatabaseProvider server, out bool scratch)
    {
        scratch = false;
        if (configuration.GetDatabaseProvider() == server)
            return Source();
        scratch = true;
        return Scratch() is { } s && s.Provider == server ? s : null;
    }

    /// <summary>The formats a download can be in here: always SQLite and the two scripts; .bak and .dump where a server of their kind is.</summary>
    public IReadOnlyList<BackupFormat> Formats
    {
        get
        {
            var formats = new List<BackupFormat> { BackupFormat.Sqlite, BackupFormat.Postgres, BackupFormat.SqlServer };
            if (NativeSource(DatabaseProvider.SqlServer, out _) is not null)
                formats.Add(BackupFormat.Bak);
            if (NativeSource(DatabaseProvider.Postgres, out _) is not null)
                formats.Add(BackupFormat.Dump);
            return formats;
        }
    }

    /// <summary>A new backup in the format; null when another backup or export is running.</summary>
    public async Task<BackupFile?> CreateAsync(BackupFormat format, CancellationToken cancellationToken)
    {
        if (!await _running.WaitAsync(0, cancellationToken))
            return null;
        var path = Path.Combine(Path.GetTempPath(), $"grif-backup-{Guid.NewGuid():N}");
        try
        {
            var source = Source();
            var stamp = timeProvider.GetUtcNow().ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            IReadOnlyList<ConvertedTable> tables = [];
            string fileName, contentType;
            switch (format)
            {
                case BackupFormat.Sqlite:
                {
                    // Unpooled, so nothing holds the file open once the conversion is done.
                    var target = new DatabaseEndpoint(DatabaseProvider.Sqlite, new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
                    tables = await Converter().ConvertAsync(source, target, cancellationToken);
                    await SingleFileAsync(target.ConnectionString, cancellationToken);
                    (fileName, contentType) = ($"grifball-backup-{stamp}.sqlite", "application/vnd.sqlite3");
                    break;
                }
                case BackupFormat.Postgres or BackupFormat.SqlServer:
                {
                    var target = format == BackupFormat.Postgres ? DatabaseProvider.Postgres : DatabaseProvider.SqlServer;
                    // sqlcmd reads a file as UTF-8 when it starts with the byte order mark; psql takes UTF-8 as it is.
                    await using (var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: target == DatabaseProvider.SqlServer)))
                        tables = await new DatabaseScriptExporter(loggerFactory.CreateLogger<DatabaseConverter>()).WriteAsync(source, target, writer, cancellationToken);
                    (fileName, contentType) = ($"grifball-backup-{stamp}.{(target == DatabaseProvider.Postgres ? "postgres" : "sqlserver")}.sql", "application/sql");
                    break;
                }
                default:
                {
                    var bak = format == BackupFormat.Bak;
                    var server = bak ? DatabaseProvider.SqlServer : DatabaseProvider.Postgres;
                    var native = NativeSource(server, out var scratch) ?? throw new NativeBackupUnavailableException(
                        $"A {(bak ? ".bak" : ".dump")} needs {(bak ? "SQL Server" : "PostgreSQL")}, which the site doesn't run on: turn on the chart's backupScratch " +
                        $"for a scratch {(bak ? "SQL Server" : "PostgreSQL")} to make it on, or download a {(bak ? "SQL Server" : "PostgreSQL")} script instead.");
                    if (scratch)
                        await ViaScratchAsync(source, native, copy => NativeAsync(copy, scratch: true, path, cancellationToken), cancellationToken);
                    else
                        await NativeAsync(native, scratch: false, path, cancellationToken);
                    (fileName, contentType) = ($"grifball-backup-{stamp}.{(bak ? "bak" : "dump")}", "application/octet-stream");
                    break;
                }
            }

            var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            return new BackupFile(file, fileName, contentType, tables);
        }
        catch
        {
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
                File.Delete(file);
            throw;
        }
        finally
        {
            _running.Release();
        }
    }

    /// <summary>The database's native backup on its server. On the scratch server: in SQL Server's own folder, one file, overwritten each time.</summary>
    private Task NativeAsync(DatabaseEndpoint database, bool scratch, string path, CancellationToken cancellationToken) => database.Provider == DatabaseProvider.SqlServer
        ? NativeBackups.SqlServerAsync(database.ConnectionString, scratch ? null : configuration[NativeBackups.SqlServerDirectoryKey], path, cancellationToken,
            fileName: scratch ? "download_scratch.bak" : null)
        : NativeBackups.PgDumpAsync(database.ConnectionString, configuration.GetValue(NativeBackups.PgDumpKey, "pg_dump")!, path, cancellationToken);

    /// <summary>
    /// The source copied into a new database on the scratch server (the converter, which checks the copy), the action
    /// run on that copy, and the copy dropped, whatever happens.
    /// </summary>
    private async Task ViaScratchAsync(DatabaseEndpoint source, DatabaseEndpoint scratch, Func<DatabaseEndpoint, Task> action, CancellationToken cancellationToken)
    {
        var name = $"grif_backup_{Guid.NewGuid():N}";
        // Unpooled, so no idle connection holds the copy when it's dropped.
        var copy = new DatabaseEndpoint(scratch.Provider, scratch.Provider == DatabaseProvider.SqlServer
            ? new SqlConnectionStringBuilder(scratch.ConnectionString) { InitialCatalog = name, Pooling = false }.ConnectionString
            : new NpgsqlConnectionStringBuilder(scratch.ConnectionString) { Database = name, Pooling = false }.ConnectionString);
        _logger.LogInformation("Copying the database into {Database} on the scratch {Provider} server for its native backup", name, scratch.Provider);
        try
        {
            await Converter().ConvertAsync(source, copy, cancellationToken);
            await action(copy);
        }
        finally
        {
            await using var context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(copy.Provider, copy.ConnectionString).Options);
            await context.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Starts copying the database into a new one on the given server (missing, or without tables); its progress is in
    /// <see cref="Export"/>. Null when a backup or export is already running. Throws ArgumentException for a connection
    /// string the provider can't read.
    /// </summary>
    public Task? StartExport(DatabaseProvider provider, string connectionString)
    {
        Validate(provider, connectionString);
        if (!_running.Wait(0))
            return null;
        _export = new BackupExportStatus(ExportState.Running, provider.ToString(), timeProvider.GetUtcNow());
        return Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Exporting the database to a new {Provider} database", provider);
                var tables = await Converter().ConvertAsync(Source(), new DatabaseEndpoint(provider, connectionString), lifetime.ApplicationStopping);
                _export = _export with { State = ExportState.Succeeded, Finished = timeProvider.GetUtcNow(), Tables = tables.Count, Rows = tables.Sum(t => t.Rows) };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "The export to {Provider} failed", provider);
                _export = _export with { State = ExportState.Failed, Finished = timeProvider.GetUtcNow(), Message = Describe(e) };
            }
            finally
            {
                _running.Release();
            }
        });
    }

    private static void Validate(DatabaseProvider provider, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("A connection string is required.");
        DbConnectionStringBuilder builder = provider switch
        {
            DatabaseProvider.SqlServer => new SqlConnectionStringBuilder(),
            DatabaseProvider.Postgres => new NpgsqlConnectionStringBuilder(),
            _ => new SqliteConnectionStringBuilder(),
        };
        // Throws ArgumentException for keys the provider doesn't know.
        builder.ConnectionString = connectionString;
    }

    /// <summary>What went wrong, for the page: the converter's reasons as they are; a server it couldn't reach, with where to look.</summary>
    public static string Describe(Exception e) => e switch
    {
        ConversionException => e.Message,
        OperationCanceledException => "The app stopped during the export; the target was put back as it was.",
        DbException or TimeoutException => $"{e.Message} If the server couldn't be reached: the backend's NetworkPolicy lets it reach its own database only; add the target to networkPolicy.backend.extraEgress.",
        _ => e.Message,
    };

    /// <summary>
    /// The app opens SQLite in WAL mode (SqliteConnectionSetup), which keeps recent writes in a -wal file beside the
    /// database: back to a rollback journal, so everything is in the one file that's downloaded.
    /// </summary>
    private static async Task SingleFileAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = DELETE";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
