using System.Data.Common;
using System.Text;
using GrifballWebApp.Infrastructure.Conversion;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GrifballWebApp.Infrastructure.DatabaseBackup;

/// <summary>
/// What a download is: a SQLite database file; a SQL script for PostgreSQL or SQL Server; or a native backup, SQL
/// Server's .bak or PostgreSQL's pg_dump archive. Each from whichever server the app is on: a native backup of the other
/// kind is made on the scratch server (backupScratch).
/// </summary>
public enum BackupFormat { Sqlite, Postgres, SqlServer, Bak, Dump }

/// <summary>A finished backup: the file, deleted once the stream is closed, its name and type, and the tables it holds (empty for a native backup).</summary>
public sealed record BackupFile(FileStream File, string FileName, string ContentType, IReadOnlyList<ConvertedTable> Tables);

/// <summary>A made backup, in a file of its own: its path, name and type, and the tables it holds (empty for a native backup).</summary>
public sealed record BuiltBackup(string Path, string FileName, string ContentType, IReadOnlyList<ConvertedTable> Tables);

/// <summary>
/// The app's database, whichever server it's on, as a download (<see cref="BackupFormat"/>): copied into a new SQLite
/// file by the converter (DatabaseConverter), every table, row history included, checked against the source before
/// it's handed over; written as a script that builds it again on PostgreSQL or SQL Server (DatabaseScriptExporter); or
/// a server's native backup (NativeBackups): the app's own server's, or, of the other kind, the scratch server's
/// (DatabaseBackup:Scratch), backing up a temporary copy of the database made there by the converter and dropped after.
/// Or the database copied straight into a new one on another server, any of the three (<see cref="ExportAsync"/>): the
/// converter creates it, or takes an empty one, and drops it again (or undoes its migrations) if anything fails. The
/// Database Backup page runs both as jobs (BackupJobs), on the pod that runs the jobs. One backup or export at a time in
/// a process: the copy holds a table's rows in memory.
/// </summary>
public sealed class DatabaseBackupService(IConfiguration configuration, ILoggerFactory loggerFactory, TimeProvider timeProvider,
    IScratchScaler? scratchScaler = null)
{
    private readonly SemaphoreSlim _running = new(1, 1);
    private readonly ILogger _logger = loggerFactory.CreateLogger<DatabaseBackupService>();
    private DatabaseEndpoint Source() => new(configuration.GetDatabaseProvider(),
        configuration.GetConnectionString("GrifballWebApp") ?? throw new InvalidOperationException("ConnectionStrings:GrifballWebApp is not set"));

    private DatabaseConverter Converter() => new(loggerFactory.CreateLogger<DatabaseConverter>());

    /// <summary>
    /// The scratch server's kind; null when there's none. Set alone (DatabaseBackup:Scratch:Provider, without the
    /// connection string) on an API pod: it offers and accepts the scratch server's format, which the worker makes.
    /// </summary>
    private DatabaseProvider? ScratchProvider()
    {
        var provider = configuration["DatabaseBackup:Scratch:Provider"];
        if (string.IsNullOrWhiteSpace(provider) && string.IsNullOrWhiteSpace(configuration["DatabaseBackup:Scratch:ConnectionString"]))
            return null;
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Provider"] = provider })
            .Build()
            .GetDatabaseProvider();
    }

    /// <summary>The scratch server (the chart's backupScratch), as its administrator; null when there's none here.</summary>
    private DatabaseEndpoint? Scratch()
    {
        var connectionString = configuration["DatabaseBackup:Scratch:ConnectionString"];
        return string.IsNullOrWhiteSpace(connectionString) ? null : new DatabaseEndpoint(ScratchProvider()!.Value, connectionString);
    }

    /// <summary>Whether a native backup of the server can be made: the app's own server, or the scratch server, here or on the worker.</summary>
    private bool HasNativeServer(DatabaseProvider server) => configuration.GetDatabaseProvider() == server || ScratchProvider() == server;

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
            if (HasNativeServer(DatabaseProvider.SqlServer))
                formats.Add(BackupFormat.Bak);
            if (HasNativeServer(DatabaseProvider.Postgres))
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

    /// <summary>A new backup in the format, in a file the caller keeps (BackupFiles); null when another backup or export is running.</summary>
    public async Task<BuiltBackup?> MakeAsync(BackupFormat format, CancellationToken cancellationToken, IProgress<ConversionProgress>? progress = null)
    {
        if (!await _running.WaitAsync(0, cancellationToken))
            return null;
        try
        {
            return await BuildAsync(format, cancellationToken, progress);
        }
        finally
        {
            _running.Release();
        }
    }

    /// <summary>
    /// The database copied into a new one on the given server (missing, or without tables): the tables copied; null when
    /// another backup or export is running. Throws ArgumentException for a connection string the provider can't read.
    /// </summary>
    public async Task<IReadOnlyList<ConvertedTable>?> ExportAsync(DatabaseProvider provider, string connectionString, CancellationToken cancellationToken,
        IProgress<ConversionProgress>? progress = null)
    {
        Validate(provider, connectionString);
        if (!await _running.WaitAsync(0, cancellationToken))
            return null;
        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Exporting the database to a new {Provider} database", provider);
            return await Converter().ConvertAsync(Source(), new DatabaseEndpoint(provider, connectionString), progress, cancellationToken);
        }
        finally
        {
            _running.Release();
        }
    }

    /// <summary>What went wrong with a backup, for the page.</summary>
    public static string DescribeBackup(Exception e) => e switch
    {
        OperationCanceledException => "The app stopped while the backup was being made.",
        _ => e.Message,
    };

    /// <summary>Throws NativeBackupUnavailableException when the format's server is neither the app's nor the scratch server (checked before the job starts).</summary>
    public void RequireNativeServer(BackupFormat format)
    {
        if (!HasNativeServer(format == BackupFormat.Bak ? DatabaseProvider.SqlServer : DatabaseProvider.Postgres))
            throw NoNativeServer(format);
    }

    private static NativeBackupUnavailableException NoNativeServer(BackupFormat format)
    {
        var bak = format == BackupFormat.Bak;
        return new NativeBackupUnavailableException(
            $"A {(bak ? ".bak" : ".dump")} needs {(bak ? "SQL Server" : "PostgreSQL")}, which the site doesn't run on: turn on the chart's backupScratch " +
            $"for a scratch {(bak ? "SQL Server" : "PostgreSQL")} to make it on, or download a {(bak ? "SQL Server" : "PostgreSQL")} script instead.");
    }

    /// <summary>Where a native backup in the format is made; throws NativeBackupUnavailableException when there's no server of its kind here.</summary>
    public DatabaseEndpoint RequireNativeSource(BackupFormat format, out bool scratch)
    {
        return NativeSource(format == BackupFormat.Bak ? DatabaseProvider.SqlServer : DatabaseProvider.Postgres, out scratch) ?? throw NoNativeServer(format);
    }

    /// <summary>The backup made in a new file in the temp folder; the file deleted again if it fails.</summary>
    private async Task<BuiltBackup> BuildAsync(BackupFormat format, CancellationToken cancellationToken, IProgress<ConversionProgress>? progress = null)
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
                    tables = await Converter().ConvertAsync(source, target, progress, cancellationToken);
                    await SingleFileAsync(target.ConnectionString, cancellationToken);
                    (fileName, contentType) = ($"grifball-backup-{stamp}.sqlite", "application/vnd.sqlite3");
                    break;
                }
                case BackupFormat.Postgres or BackupFormat.SqlServer:
                {
                    var target = format == BackupFormat.Postgres ? DatabaseProvider.Postgres : DatabaseProvider.SqlServer;
                    // sqlcmd reads a file as UTF-8 when it starts with the byte order mark; psql takes UTF-8 as it is.
                    await using (var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: target == DatabaseProvider.SqlServer)))
                        tables = await new DatabaseScriptExporter(loggerFactory.CreateLogger<DatabaseConverter>()).WriteAsync(source, target, writer, cancellationToken, progress);
                    (fileName, contentType) = ($"grifball-backup-{stamp}.{(target == DatabaseProvider.Postgres ? "postgres" : "sqlserver")}.sql", "application/sql");
                    break;
                }
                default:
                    await NativeBackupAsync(format, source, path, progress, cancellationToken);
                    (fileName, contentType) = ($"grifball-backup-{stamp}.{(format == BackupFormat.Bak ? "bak" : "dump")}", "application/octet-stream");
                    break;
            }
            return new BuiltBackup(path, fileName, contentType, tables);
        }
        catch
        {
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
                File.Delete(file);
            throw;
        }
    }

    /// <summary>The native backup in the format (.bak or .dump) written to the path: made on the app's own server, or on a copy on the scratch server.</summary>
    private async Task NativeBackupAsync(BackupFormat format, DatabaseEndpoint source, string path, IProgress<ConversionProgress>? progress, CancellationToken cancellationToken)
    {
        var native = RequireNativeSource(format, out var scratch);
        var making = $"Making the {(format == BackupFormat.Bak ? ".bak on SQL Server" : ".dump with pg_dump")}";
        if (scratch)
            await ViaScratchAsync(source, native, copy =>
            {
                // The copy's steps were the bar's; the backup itself says nothing until it's done
                progress?.Report(new ConversionProgress(99, 100, making));
                return NativeAsync(copy, scratch: true, path, cancellationToken);
            }, cancellationToken, progress);
        else
        {
            progress?.Report(new ConversionProgress(0, 1, making));
            await NativeAsync(native, scratch: false, path, cancellationToken);
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
    private async Task ViaScratchAsync(DatabaseEndpoint source, DatabaseEndpoint scratch, Func<DatabaseEndpoint, Task> action, CancellationToken cancellationToken,
        IProgress<ConversionProgress>? progress = null)
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
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("Starting the scratch {Provider} server", scratch.Provider);
                progress?.Report(new ConversionProgress(0, 1, $"Starting the scratch {scratch.Provider} server"));
                await scratchScaler.StartAsync(cancellationToken);
                await WaitForConnectionsAsync(scratch, cancellationToken);
            }
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Copying the database into {Database} on the scratch {Provider} server for its native backup", name, scratch.Provider);
            try
            {
                await Converter().ConvertAsync(source, copy, progress, cancellationToken);
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

    /// <summary>Throws ArgumentException for a connection string the provider can't read.</summary>
    public static void Validate(DatabaseProvider provider, string connectionString)
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
