using GrifballWebApp.Database;
using GrifballWebApp.Database.Conversion;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.DatabaseBackup;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Test;

/// <summary>The Database Backup page's API: the suite's database copied into a downloaded SQLite file.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class BackupControllerTests
{
    private GrifballContext _context = null!;
    private readonly List<string> _files = [];

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
        foreach (var file in _files)
        {
            SqliteConnection.ClearPool(new SqliteConnection($"Data Source={file}"));
            File.Delete(file);
        }
    }

    private IConfiguration Configuration(string? connectionString = null) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:GrifballWebApp"] = connectionString ?? _context.Database.GetConnectionString(),
            ["Database:Provider"] = TestDatabase.Provider.ToString(),
        }).Build();

    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 30, 45, TimeSpan.Zero);

    private DatabaseBackupService Service(ILoggerFactory? loggerFactory = null, IConfiguration? configuration = null)
        => new(configuration ?? Configuration(), loggerFactory ?? NullLoggerFactory.Instance, new FakeTimeProvider(Now));

    private BackupController Controller(DatabaseBackupService service)
        => new BackupController(service, _context, Configuration(), NullLogger<BackupController>.Instance).WithUser("1", "admin", "Sysadmin");

    /// <summary>The download, copied to a file of the test's, the original closed (and so deleted).</summary>
    private async Task<string> SaveAsync(FileStream download)
    {
        var copy = Path.Combine(Path.GetTempPath(), $"grif-backup-test-{Guid.NewGuid():N}.sqlite");
        _files.Add(copy);
        await using (download)
        await using (var file = File.Create(copy))
            await download.CopyToAsync(file);
        return copy;
    }

    [Test]
    public void Is_sysadmin_only()
    {
        Assert.That(ControllerTestHelpers.ClassAuthorize(typeof(BackupController))?.Roles, Is.EqualTo("Sysadmin"));
    }

    [Test]
    public void Info_gives_the_server_and_the_last_migration()
    {
        var info = Controller(Service()).Info();

        Assert.That(info.Provider, Is.EqualTo(TestDatabase.Provider.ToString()));
        Assert.That(info.LatestMigration, Is.EqualTo(_context.Database.GetMigrations().Last()));
    }

    [Test]
    public async Task Sqlite_downloads_the_whole_database_as_one_file()
    {
        _context.Seasons.Add(new Season { SeasonName = "Backed up" });
        await _context.SaveChangesAsync();

        var result = await Controller(Service()).Sqlite(CancellationToken.None);

        var file = result as FileStreamResult;
        Assert.That(file, Is.Not.Null);
        Assert.That(file!.FileDownloadName, Is.EqualTo("grifball-backup-20261101-123045.sqlite"));
        Assert.That(file.ContentType, Is.EqualTo("application/vnd.sqlite3"));
        var original = ((FileStream)file.FileStream).Name;
        // Rolled back from WAL: nothing beside the file.
        Assert.That(File.Exists(original + "-wal"), Is.False);

        var copy = await SaveAsync((FileStream)file.FileStream);
        Assert.That(File.Exists(original), Is.False, "the temp file goes once the download is closed");

        await using (var connection = new SqliteConnection($"Data Source={copy};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode";
            Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo("delete"));
        }

        await using var backup = DatabaseConverter.NewContext(new DatabaseEndpoint(DatabaseProvider.Sqlite, $"Data Source={copy};Pooling=False"));
        Assert.That(await backup.Seasons.Select(s => s.SeasonName).ToListAsync(), Is.EqualTo(new[] { "Backed up" }));
        Assert.That(await backup.Database.GetAppliedMigrationsAsync(), Is.EqualTo(backup.Database.GetMigrations()));
    }

    [Test]
    public async Task Sqlite_is_409_while_another_backup_is_being_made()
    {
        var logging = new PausingLoggerFactory();
        var service = Service(logging);
        var first = Task.Run(() => service.CreateAsync(CancellationToken.None));
        Assert.That(logging.Paused.Wait(TimeSpan.FromMinutes(2)), Is.True, "the first backup started");

        var second = await Controller(service).Sqlite(CancellationToken.None);
        Assert.That(second, Is.TypeOf<ConflictObjectResult>());

        logging.Resume.Set();
        var backup = await first;
        Assert.That(backup, Is.Not.Null);
        await SaveAsync(backup!.File);

        // And free again once it's done.
        var third = await Service().CreateAsync(CancellationToken.None);
        Assert.That(third, Is.Not.Null);
        await SaveAsync(third!.File);
    }

    [Test]
    public void A_failed_backup_lets_the_next_run()
    {
        // A database that doesn't exist: the converter refuses.
        var missing = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, $"Missing_{Guid.NewGuid():N}");
        var service = Service(configuration: Configuration(missing));

        Assert.ThrowsAsync<ConversionException>(() => service.CreateAsync(CancellationToken.None));
        Assert.ThrowsAsync<ConversionException>(() => service.CreateAsync(CancellationToken.None), "not left busy");
    }

    [Test]
    public void Without_a_connection_string_it_says_so()
    {
        var service = new DatabaseBackupService(new ConfigurationBuilder().Build(), NullLoggerFactory.Instance, TimeProvider.System);

        var e = Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(CancellationToken.None));
        Assert.That(e!.Message, Does.Contain("ConnectionStrings:GrifballWebApp"));
    }

    /// <summary>Holds the converter at its first log line, a backup under way, until the test resumes it.</summary>
    private sealed class PausingLoggerFactory : ILoggerFactory, ILogger
    {
        public readonly ManualResetEventSlim Paused = new();
        public readonly ManualResetEventSlim Resume = new();

        public ILogger CreateLogger(string categoryName) => this;
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (Paused.IsSet)
                return;
            Paused.Set();
            Resume.Wait(TimeSpan.FromMinutes(2));
        }
    }
}
