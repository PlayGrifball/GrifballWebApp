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

/// <summary>The last download's backup: being made, made (and ready, as <see cref="FileName"/>), or failed, and why.</summary>
public sealed record BackupDownloadStatus(ExportState State, string? Format = null, DateTimeOffset? Started = null, DateTimeOffset? Finished = null,
    string? FileName = null, int Tables = 0, int Rows = 0, string? Message = null);

/// <summary>A made backup, opened to read: the file stays for the next download until it's discarded.</summary>
public sealed record ReadyDownload(FileStream File, string FileName, string ContentType);

/// <summary>
/// The app's database, whichever server it's on, as a download (<see cref="BackupFormat"/>): copied into a new SQLite
/// file by the converter (DatabaseConverter), every table, row history included, checked against the source before
/// it's handed over; written as a script that builds it again on PostgreSQL or SQL Server (DatabaseScriptExporter); or
/// a server's native backup (NativeBackups): the app's own server's, or, of the other kind, the scratch server's
/// (DatabaseBackup:Scratch), backing up a temporary copy of the database made there by the converter and dropped after.
/// Each is made in the background (<see cref="StartDownload"/>) in the temp folder, and kept for the page to fetch
/// until the next one starts or <see cref="ReadyFor"/> has passed.
/// Or the database copied straight into a new one on another server, any of the three (<see cref="StartExport"/>), in
/// the background: the converter creates it, or takes an empty one, and drops it again (or undoes its migrations) if
/// anything fails. One backup or export at a time: the copy holds a table's rows in memory. The export's status is
/// this process's (backend.replicas is 1).
/// </summary>
public sealed class DatabaseBackupService(IConfiguration configuration, ILoggerFactory loggerFactory, TimeProvider timeProvider,
    IHostApplicationLifetime lifetime, IScratchScaler? scratchScaler = null)
{
    private readonly SemaphoreSlim _running = new(1, 1);
    private readonly ILogger _logger = loggerFactory.CreateLogger<DatabaseBackupService>();
    private volatile BackupExportStatus _export = new(ExportState.None);

    public BackupExportStatus Export => _export;

    private sealed record ReadyBackup(string Path, string FileName, string ContentType);

    private readonly Lock _readyLock = new();
    private ReadyBackup? _ready;
    private ITimer? _expiry;
    private volatile BackupDownloadStatus _download = new(ExportState.None);

    public BackupDownloadStatus Download => _download;

    /// <summary>How long a made backup waits to be fetched.</summary>
    public TimeSpan ReadyFor { get; init; } = TimeSpan.FromHours(1);

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
        try
        {
            var built = await BuildAsync(format, cancellationToken);
            var file = new FileStream(built.Path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            return new BackupFile(file, built.FileName, built.ContentType, built.Tables);
        }
        finally
        {
            _running.Release();
        }
    }

    /// <summary>
    /// Starts making a backup in the format, in the background, since a large one takes longer than a proxy waits for
    /// a response (Cloudflare's 100 seconds); its progress is in <see cref="Download"/>, and once it's made
    /// <see cref="OpenReady"/> hands it over, for <see cref="ReadyFor"/> or until the next one starts. Null when a
    /// backup or export is already running. Throws NativeBackupUnavailableException at once for a native backup with
    /// no server of its kind here.
    /// </summary>
    public Task? StartDownload(BackupFormat format)
    {
        if (format is BackupFormat.Bak or BackupFormat.Dump)
            RequireNativeSource(format, out _);
        if (!_running.Wait(0))
            return null;
        DiscardReady();
        _download = new BackupDownloadStatus(ExportState.Running, format.ToString().ToLowerInvariant(), timeProvider.GetUtcNow());
        return Task.Run(async () =>
        {
            try
            {
                var built = await BuildAsync(format, lifetime.ApplicationStopping);
                lock (_readyLock)
                {
                    _ready = new ReadyBackup(built.Path, built.FileName, built.ContentType);
                    _expiry = timeProvider.CreateTimer(_ => DiscardReady(), null, ReadyFor, Timeout.InfiniteTimeSpan);
                    _download = _download with
                    {
                        State = ExportState.Succeeded, Finished = timeProvider.GetUtcNow(), FileName = built.FileName,
                        Tables = built.Tables.Count, Rows = built.Tables.Sum(t => t.Rows),
                    };
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "The {Format} backup failed", format);
                _download = _download with { State = ExportState.Failed, Finished = timeProvider.GetUtcNow(), Message = DescribeBackup(e) };
            }
            finally
            {
                _running.Release();
            }
        });
    }

    /// <summary>The last backup made, opened to read; null when there's none (not made, or discarded since).</summary>
    public ReadyDownload? OpenReady()
    {
        lock (_readyLock)
        {
            if (_ready is null)
                return null;
            // FileShare.Delete: discarded meanwhile, it's still read to the end.
            var file = new FileStream(_ready.Path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, FileOptions.Asynchronous);
            return new ReadyDownload(file, _ready.FileName, _ready.ContentType);
        }
    }

    /// <summary>The last backup made deleted, and its status with it.</summary>
    private void DiscardReady()
    {
        lock (_readyLock)
        {
            _expiry?.Dispose();
            _expiry = null;
            if (_ready is null)
                return;
            File.Delete(_ready.Path);
            _ready = null;
            if (_download.State == ExportState.Succeeded)
                _download = new BackupDownloadStatus(ExportState.None);
        }
    }

    /// <summary>What went wrong with a backup, for the page.</summary>
    public static string DescribeBackup(Exception e) => e switch
    {
        OperationCanceledException => "The app stopped while the backup was being made.",
        _ => e.Message,
    };

    /// <summary>Where a native backup in the format is made; throws NativeBackupUnavailableException when there's no server of its kind here.</summary>
    private DatabaseEndpoint RequireNativeSource(BackupFormat format, out bool scratch)
    {
        var bak = format == BackupFormat.Bak;
        return NativeSource(bak ? DatabaseProvider.SqlServer : DatabaseProvider.Postgres, out scratch) ?? throw new NativeBackupUnavailableException(
            $"A {(bak ? ".bak" : ".dump")} needs {(bak ? "SQL Server" : "PostgreSQL")}, which the site doesn't run on: turn on the chart's backupScratch " +
            $"for a scratch {(bak ? "SQL Server" : "PostgreSQL")} to make it on, or download a {(bak ? "SQL Server" : "PostgreSQL")} script instead.");
    }

    /// <summary>The backup made in a new file in the temp folder; the file deleted again if it fails.</summary>
    private async Task<(string Path, string FileName, string ContentType, IReadOnlyList<ConvertedTable> Tables)> BuildAsync(BackupFormat format, CancellationToken cancellationToken)
    {
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
                    var native = RequireNativeSource(format, out var scratch);
                    if (scratch)
                        await ViaScratchAsync(source, native, copy => NativeAsync(copy, scratch: true, path, cancellationToken), cancellationToken);
                    else
                        await NativeAsync(native, scratch: false, path, cancellationToken);
                    (fileName, contentType) = ($"grifball-backup-{stamp}.{(format == BackupFormat.Bak ? "bak" : "dump")}", "application/octet-stream");
                    break;
                }
            }
            return (path, fileName, contentType, tables);
        }
        catch
        {
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
                File.Delete(file);
            throw;
        }
    }

    /// <summary>The database's native backup on its server. On the scratch server: in SQL Server's own folder, one file, overwritten each time.</summary>
    private Task NativeAsync(DatabaseEndpoint database, bool scratch, string path, CancellationToken cancellationToken) => database.Provider == DatabaseProvider.SqlServer
        ? NativeBackups.SqlServerAsync(database.ConnectionString, scratch ? null : configuration[NativeBackups.SqlServerDirectoryKey], path, cancellationToken,
            fileName: scratch ? "download_scratch.bak" : null)
        : NativeBackups.PgDumpAsync(database.ConnectionString, configuration.GetValue(NativeBackups.PgDumpKey, "pg_dump")!, path, cancellationToken);

    /// <summary>
    /// The source copied into a new database on the scratch server (the converter, which checks the copy), the action
    /// run on that copy, and the copy dropped, whatever happens. A scratch server started on demand
    /// (IScratchScaler) is started first, and stopped again after.
    /// </summary>
    private async Task ViaScratchAsync(DatabaseEndpoint source, DatabaseEndpoint scratch, Func<DatabaseEndpoint, Task> action, CancellationToken cancellationToken)
    {
        var name = $"grif_backup_{Guid.NewGuid():N}";
        // Unpooled, so no idle connection holds the copy when it's dropped.
        var copy = new DatabaseEndpoint(scratch.Provider, scratch.Provider == DatabaseProvider.SqlServer
            ? new SqlConnectionStringBuilder(scratch.ConnectionString) { InitialCatalog = name, Pooling = false }.ConnectionString
            : new NpgsqlConnectionStringBuilder(scratch.ConnectionString) { Database = name, Pooling = false }.ConnectionString);
        try
        {
            if (scratchScaler is not null)
            {
                _logger.LogInformation("Starting the scratch {Provider} server", scratch.Provider);
                await scratchScaler.StartAsync(cancellationToken);
                await WaitForConnectionsAsync(scratch, cancellationToken);
            }
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
        finally
        {
            if (scratchScaler is not null)
                await scratchScaler.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>How long a just-started scratch server may refuse logins (SQL Server finishing its upgrade scripts).</summary>
    public TimeSpan ScratchLoginWait { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Until the started scratch server lets its administrator in: its pod is ready once its port is open, a little before.</summary>
    private async Task WaitForConnectionsAsync(DatabaseEndpoint scratch, CancellationToken cancellationToken)
    {
        var deadline = timeProvider.GetUtcNow() + ScratchLoginWait;
        while (true)
        {
            try
            {
                await using DbConnection connection = scratch.Provider == DatabaseProvider.SqlServer ? new SqlConnection(scratch.ConnectionString) : new NpgsqlConnection(scratch.ConnectionString);
                await connection.OpenAsync(cancellationToken);
                return;
            }
            catch (DbException) when (timeProvider.GetUtcNow() < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
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
