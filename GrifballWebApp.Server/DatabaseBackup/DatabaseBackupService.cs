using GrifballWebApp.Database;
using GrifballWebApp.Database.Conversion;
using Microsoft.Data.Sqlite;

namespace GrifballWebApp.Server.DatabaseBackup;

/// <summary>A finished backup: the SQLite file, deleted once the stream is closed, and what it holds.</summary>
public sealed record BackupFile(FileStream File, string FileName, IReadOnlyList<ConvertedTable> Tables);

/// <summary>
/// The app's database, whichever server it's on, copied into a new SQLite file by the converter (DatabaseConverter):
/// every table, row history included, checked against the source before it's handed over. The file is a complete
/// backup on its own, and the converter (/app/convert) loads it into SQL Server, PostgreSQL or SQLite again.
/// It's written to the temp folder and deleted when the download's stream closes. One at a time: the copy holds a
/// table's rows in memory.
/// </summary>
public sealed class DatabaseBackupService(IConfiguration configuration, ILoggerFactory loggerFactory, TimeProvider timeProvider)
{
    private readonly SemaphoreSlim _running = new(1, 1);

    /// <summary>A new backup; null when another is being made.</summary>
    public async Task<BackupFile?> CreateAsync(CancellationToken cancellationToken)
    {
        if (!await _running.WaitAsync(0, cancellationToken))
            return null;
        var path = Path.Combine(Path.GetTempPath(), $"grif-backup-{Guid.NewGuid():N}.sqlite");
        try
        {
            var source = new DatabaseEndpoint(configuration.GetDatabaseProvider(),
                configuration.GetConnectionString("GrifballWebApp") ?? throw new InvalidOperationException("ConnectionStrings:GrifballWebApp is not set"));
            // Unpooled, so nothing holds the file open once the conversion is done.
            var target = new DatabaseEndpoint(DatabaseProvider.Sqlite, new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
            var tables = await new DatabaseConverter(loggerFactory.CreateLogger<DatabaseConverter>()).ConvertAsync(source, target, cancellationToken);
            await SingleFileAsync(target.ConnectionString, cancellationToken);

            var fileName = $"grifball-backup-{timeProvider.GetUtcNow():yyyyMMdd-HHmmss}.sqlite";
            var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            return new BackupFile(file, fileName, tables);
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
