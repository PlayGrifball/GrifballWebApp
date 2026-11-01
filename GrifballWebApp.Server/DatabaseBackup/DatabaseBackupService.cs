using System.Data.Common;
using System.Text;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Conversion;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace GrifballWebApp.Server.DatabaseBackup;

/// <summary>
/// What a download is: a SQLite database file; a SQL script for PostgreSQL or SQL Server, from whichever server the app is
/// on; or the app's own server's native backup (SQL Server's .bak, PostgreSQL's pg_dump archive, SQLite's file).
/// </summary>
public enum BackupFormat { Sqlite, Postgres, SqlServer, Native }

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
/// the server's own backup (NativeBackups). Each is written to the temp folder and deleted when the download's stream
/// closes.
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

    /// <summary>The app's own server's native backup's file extension: bak, dump, or sqlite.</summary>
    public string NativeExtension => configuration.GetDatabaseProvider() switch
    {
        DatabaseProvider.SqlServer => "bak",
        DatabaseProvider.Postgres => "dump",
        _ => "sqlite",
    };

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
            if (format == BackupFormat.Native && source.Provider == DatabaseProvider.Sqlite)
                format = BackupFormat.Sqlite;
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
                    if (source.Provider == DatabaseProvider.SqlServer)
                        await NativeBackups.SqlServerAsync(source.ConnectionString, configuration[NativeBackups.SqlServerDirectoryKey], path, cancellationToken);
                    else
                        await NativeBackups.PgDumpAsync(source.ConnectionString, configuration[NativeBackups.PgDumpKey] ?? "pg_dump", path, cancellationToken);
                    (fileName, contentType) = ($"grifball-backup-{stamp}.{NativeExtension}", "application/octet-stream");
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
