using GrifballWebApp.Database;
using GrifballWebApp.Database.Conversion;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.DatabaseBackup;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Test;

/// <summary>The Database Backup page's API: the suite's database copied into a downloaded SQLite file, or exported into a new database.</summary>
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

    private static IHostApplicationLifetime Lifetime(CancellationToken stopping = default)
    {
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(stopping);
        return lifetime;
    }

    private DatabaseBackupService Service(ILoggerFactory? loggerFactory = null, IConfiguration? configuration = null, IHostApplicationLifetime? lifetime = null)
        => new(configuration ?? Configuration(), loggerFactory ?? NullLoggerFactory.Instance, new FakeTimeProvider(Now), lifetime ?? Lifetime());

    /// <summary>A new SQLite file's connection string, unpooled, deleted after the test: an export target that every suite has.</summary>
    private string NewSqliteTarget()
    {
        var path = Path.Combine(Path.GetTempPath(), $"grif-backup-test-{Guid.NewGuid():N}.sqlite");
        _files.Add(path);
        return $"Data Source={path};Pooling=False";
    }

    private static async Task<BackupExportStatus> FinishedAsync(BackupController controller)
    {
        for (var i = 0; i < 1200 && controller.ExportStatus().State == ExportState.Running; i++)
            await Task.Delay(100);
        return controller.ExportStatus();
    }

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
        Assert.That(info.NativeExtension, Is.EqualTo(TestDatabase.Provider switch { DatabaseProvider.SqlServer => "bak", DatabaseProvider.Postgres => "dump", _ => "sqlite" }));
    }

    [Test]
    public async Task Sqlite_downloads_the_whole_database_as_one_file()
    {
        _context.Seasons.Add(new Season { SeasonName = "Backed up" });
        await _context.SaveChangesAsync();

        var result = await Controller(Service()).Download("sqlite", CancellationToken.None);

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
        var first = Task.Run(() => service.CreateAsync(BackupFormat.Sqlite, CancellationToken.None));
        Assert.That(logging.Paused.Wait(TimeSpan.FromMinutes(2)), Is.True, "the first backup started");

        var second = await Controller(service).Download("sqlite", CancellationToken.None);
        Assert.That(second, Is.TypeOf<ConflictObjectResult>());

        logging.Resume.Set();
        var backup = await first;
        Assert.That(backup, Is.Not.Null);
        await SaveAsync(backup!.File);

        // And free again once it's done.
        var third = await Service().CreateAsync(BackupFormat.Sqlite, CancellationToken.None);
        Assert.That(third, Is.Not.Null);
        await SaveAsync(third!.File);
    }

    [Test]
    public async Task A_failed_backup_lets_the_next_run()
    {
        // A database that doesn't exist: the converter refuses.
        var missing = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, $"Missing_{Guid.NewGuid():N}");
        var service = Service(configuration: Configuration(missing));

        await Assert.ThrowsAsync<ConversionException>(() => service.CreateAsync(BackupFormat.Sqlite, CancellationToken.None));
        await Assert.ThrowsAsync<ConversionException>(() => service.CreateAsync(BackupFormat.Sqlite, CancellationToken.None), "not left busy");
    }

    [Test]
    public async Task Without_a_connection_string_it_says_so()
    {
        var service = new DatabaseBackupService(new ConfigurationBuilder().Build(), NullLoggerFactory.Instance, TimeProvider.System, Lifetime());

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(BackupFormat.Sqlite, CancellationToken.None));
        Assert.That(e!.Message, Does.Contain("ConnectionStrings:GrifballWebApp"));
    }

    [Test]
    public async Task Export_copies_the_database_into_a_new_one()
    {
        _context.Seasons.Add(new Season { SeasonName = "Exported" });
        await _context.SaveChangesAsync();
        var target = NewSqliteTarget();
        var controller = Controller(Service());
        Assert.That(controller.ExportStatus().State, Is.EqualTo(ExportState.None));

        var started = controller.Export(new ExportRequestDto("sqlite", target));

        var accepted = started as AcceptedResult;
        Assert.That(accepted, Is.Not.Null);
        Assert.That(((BackupExportStatus)accepted!.Value!).Provider, Is.EqualTo("Sqlite"));
        var status = await FinishedAsync(controller);
        Assert.That(status.State, Is.EqualTo(ExportState.Succeeded), status.Message);
        Assert.That(status.Started, Is.EqualTo(Now));
        Assert.That(status.Finished, Is.EqualTo(Now));
        Assert.That(status.Tables, Is.GreaterThan(0));
        Assert.That(status.Rows, Is.GreaterThan(0));

        await using var exported = DatabaseConverter.NewContext(new DatabaseEndpoint(DatabaseProvider.Sqlite, target));
        Assert.That(await exported.Seasons.Select(s => s.SeasonName).ToListAsync(), Is.EqualTo(new[] { "Exported" }));
    }

    [Test]
    public async Task Export_never_fills_a_database_with_tables()
    {
        // The app's own database: the converter refuses it, so the live data can't be overwritten.
        var controller = Controller(Service());

        controller.Export(new ExportRequestDto(TestDatabase.Provider.ToString(), _context.Database.GetConnectionString()!));

        var status = await FinishedAsync(controller);
        Assert.That(status.State, Is.EqualTo(ExportState.Failed));
        Assert.That(status.Message, Does.Contain("already has tables"));
    }

    [Test]
    public async Task Export_says_when_the_app_stopped_during_it()
    {
        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();
        var controller = Controller(Service(lifetime: Lifetime(stopping.Token)));

        controller.Export(new ExportRequestDto("Sqlite", NewSqliteTarget()));

        var status = await FinishedAsync(controller);
        Assert.That(status.State, Is.EqualTo(ExportState.Failed));
        Assert.That(status.Message, Does.Contain("The app stopped"));
    }

    [Test]
    public async Task Export_is_409_while_a_backup_runs()
    {
        var logging = new PausingLoggerFactory();
        var service = Service(logging);
        var backup = Task.Run(() => service.CreateAsync(BackupFormat.Sqlite, CancellationToken.None));
        Assert.That(logging.Paused.Wait(TimeSpan.FromMinutes(2)), Is.True, "the backup started");

        var export = Controller(service).Export(new ExportRequestDto("Sqlite", NewSqliteTarget()));

        Assert.That(export, Is.TypeOf<ConflictObjectResult>());
        Assert.That(service.Export.State, Is.EqualTo(ExportState.None));
        logging.Resume.Set();
        await SaveAsync((await backup)!.File);
    }

    [TestCase("MySql", "Data Source=x", "The provider must be")]
    [TestCase("7", "Data Source=x", "The provider must be")]
    [TestCase("Postgres", "Nonsense=1", "isn't one Postgres reads")]
    [TestCase("SqlServer", "Nonsense=1", "isn't one SqlServer reads")]
    [TestCase("Sqlite", " ", "A connection string is required")]
    public void Export_is_400_for_a_bad_request(string provider, string connectionString, string message)
    {
        var result = Controller(Service()).Export(new ExportRequestDto(provider, connectionString));

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That((string)((BadRequestObjectResult)result).Value!, Does.Contain(message));
    }

    [Test]
    public void Describe_points_at_the_network_policy_when_a_server_cant_be_reached()
    {
        Assert.That(DatabaseBackupService.Describe(new ConversionException("Refused.")), Is.EqualTo("Refused."));
        Assert.That(DatabaseBackupService.Describe(new TimeoutException("Timed out.")), Does.StartWith("Timed out.").And.Contain("networkPolicy.backend.extraEgress"));
        Assert.That(DatabaseBackupService.Describe(new SqliteException("Unable to open.", 14)), Does.Contain("networkPolicy.backend.extraEgress"));
        Assert.That(DatabaseBackupService.Describe(new InvalidOperationException("Other.")), Is.EqualTo("Other."));
    }

    [TestCase("postgres", "postgres.sql", false)]
    [TestCase("SqlServer", "sqlserver.sql", true)]
    public async Task Download_writes_each_servers_script(string format, string suffix, bool byteOrderMark)
    {
        var result = await Controller(Service()).Download(format, CancellationToken.None);

        var file = (FileStreamResult)result;
        Assert.That(file.FileDownloadName, Is.EqualTo($"grifball-backup-20261101-123045.{suffix}"));
        Assert.That(file.ContentType, Is.EqualTo("application/sql"));
        var bytes = await File.ReadAllBytesAsync(await SaveAsync((FileStream)file.FileStream));
        Assert.That(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }), Is.EqualTo(byteOrderMark));
        Assert.That(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'), Does.StartWith("-- GrifballWebApp database export"));
    }

    [Test]
    public async Task Download_is_400_for_another_format()
    {
        var result = await Controller(Service()).Download("mysql", CancellationToken.None);

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That(await Controller(Service()).Download("9", CancellationToken.None), Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Download_native_is_the_servers_own_backup()
    {
        var configuration = Configuration();
        // pg_dump as the suite's runner may not have it, or have an older one than the server: a stand-in that writes
        // the archive's magic. NativeBackupsTests runs the real one where it can.
        var pgDump = NativeBackupsTests.FakePgDump("printf PGDMP > \"$file\"");
        _files.Add(pgDump);
        configuration[NativeBackups.PgDumpKey] = pgDump;

        var result = await Controller(Service(configuration: configuration)).Download("native", CancellationToken.None);

        var file = (FileStreamResult)result;
        var bytes = await File.ReadAllBytesAsync(await SaveAsync((FileStream)file.FileStream));
        switch (TestDatabase.Provider)
        {
            case DatabaseProvider.SqlServer:
                Assert.That(file.FileDownloadName, Does.EndWith(".bak"));
                // Microsoft Tape Format, every .bak's header
                Assert.That(System.Text.Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("TAPE"));
                break;
            case DatabaseProvider.Postgres:
                Assert.That(file.FileDownloadName, Does.EndWith(".dump"));
                Assert.That(System.Text.Encoding.ASCII.GetString(bytes, 0, 5), Is.EqualTo("PGDMP"));
                break;
            default:
                Assert.That(file.FileDownloadName, Does.EndWith(".sqlite"));
                Assert.That(System.Text.Encoding.ASCII.GetString(bytes, 0, 15), Is.EqualTo("SQLite format 3"));
                break;
        }
    }

    [TestCase(DatabaseProvider.SqlServer, "bak")]
    [TestCase(DatabaseProvider.Postgres, "dump")]
    [TestCase(DatabaseProvider.Sqlite, "sqlite")]
    public void Native_backup_is_the_servers_kind(DatabaseProvider provider, string extension)
    {
        var configuration = Configuration();
        configuration["Database:Provider"] = provider.ToString();

        Assert.That(Service(configuration: configuration).NativeExtension, Is.EqualTo(extension));
    }

    [Test]
    public async Task Native_backup_goes_to_the_servers_own_tool()
    {
        // PostgreSQL's: pg_dump (a stand-in), on the app's connection string.
        var configuration = Configuration("Host=db.example;Database=GrifballWebApp");
        configuration["Database:Provider"] = "Postgres";
        var pgDump = NativeBackupsTests.FakePgDump("printf PGDMP > \"$file\"");
        _files.Add(pgDump);
        configuration[NativeBackups.PgDumpKey] = pgDump;
        var dump = await Service(configuration: configuration).CreateAsync(BackupFormat.Native, CancellationToken.None);
        Assert.That(dump!.FileName, Does.EndWith(".dump"));
        Assert.That(await File.ReadAllTextAsync(await SaveAsync(dump.File)), Is.EqualTo("PGDMP"));

        // SQL Server's: BACKUP DATABASE on its server, here one that isn't there.
        configuration["ConnectionStrings:GrifballWebApp"] = "Server=127.0.0.1,9;Database=nope;User Id=sa;Password=x;Connect Timeout=2;TrustServerCertificate=True";
        configuration["Database:Provider"] = "SqlServer";
        Assert.That(await Assert.CatchAsync(() => Service(configuration: configuration).CreateAsync(BackupFormat.Native, CancellationToken.None)),
            Is.InstanceOf<Microsoft.Data.SqlClient.SqlException>());
    }

    [Test]
    public async Task Download_native_is_422_when_the_server_cant_make_it_here()
    {
        var configuration = Configuration();
        configuration[NativeBackups.PgDumpKey] = "/nonexistent/pg_dump";
        if (TestDatabase.Provider == DatabaseProvider.Sqlite)
            Assert.Ignore("SQLite's native backup is its file: always available.");
        if (TestDatabase.Provider == DatabaseProvider.SqlServer)
            configuration["ConnectionStrings:GrifballWebApp"] = await NativeBackupsTests.ReadOnlyLoginAsync(_context.Database.GetConnectionString()!);

        var result = await Controller(Service(configuration: configuration)).Download("native", CancellationToken.None);

        Assert.That(result, Is.TypeOf<UnprocessableEntityObjectResult>());
        Assert.That((string)((UnprocessableEntityObjectResult)result).Value!, Does.Contain("instead"));
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
