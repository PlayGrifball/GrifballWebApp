using GrifballWebApp.Database;
using GrifballWebApp.Database.Conversion;
using GrifballWebApp.Server.DatabaseBackup;
using GrifballWebApp.Server.Jobs;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Quartz;

namespace GrifballWebApp.Test;

/// <summary>
/// The Database Backup page's API: the suite's database copied into a downloaded file, or exported into a new database,
/// each as a job (BackupJobs) on the app's own Quartz setup, its store in the suite's database.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class BackupControllerTests
{
    private GrifballContext _context = null!;
    private readonly List<string> _files = [];
    private readonly List<string> _directories = [];
    private readonly List<IHost> _hosts = [];

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown()
    {
        foreach (var host in _hosts)
        {
            await host.StopAsync();
            host.Dispose();
        }
        await _context.DropDatabaseAndDispose();
        foreach (var file in _files)
        {
            SqliteConnection.ClearPool(new SqliteConnection($"Data Source={file}"));
            File.Delete(file);
        }
        foreach (var directory in _directories.Where(Directory.Exists))
            Directory.Delete(directory, recursive: true);
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

    /// <summary>
    /// The app's jobs, started, with the page's two on demand (their store in the suite's database), and the page's
    /// controller over them. The backup service reads <paramref name="backupConfiguration"/> (a missing database, a
    /// stand-in pg_dump) and logs to <paramref name="backupLogging"/>. With <paramref name="clock"/>, a made backup's expiry
    /// (BackupFiles' timer, BackupRuns' ready check) runs on it; Quartz keeps the real clock from DI.
    /// </summary>
    private async Task<BackupController> StartAsync(IConfiguration? backupConfiguration = null, ILoggerFactory? backupLogging = null, FakeTimeProvider? clock = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"grif-backup-test-{Guid.NewGuid():N}");
        _directories.Add(directory);
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = TestDatabase.Provider.ToString(),
            ["ConnectionStrings:GrifballWebApp"] = _context.Database.GetConnectionString(),
            ["DatabaseBackup:Directory"] = directory,
        });
        builder.Services.AddLogging();
        builder.Services.AddDataProtection();
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton(_ => Service(backupLogging, backupConfiguration));
        if (clock is null)
        {
            builder.Services.AddSingleton<BackupFiles>();
            builder.Services.AddTransient<BackupRuns>();
        }
        else
        {
            builder.Services.AddSingleton(sp => new BackupFiles(sp.GetRequiredService<IConfiguration>(), clock));
            builder.Services.AddTransient(sp => new BackupRuns(sp.GetRequiredService<OnDemandJobs>(), sp.GetRequiredService<IDataProtectionProvider>(), clock,
                sp.GetRequiredService<IConfiguration>()));
        }
        builder.Services.AddTransient<BackupDownloads>();
        builder.Services.AddJobs(builder.Configuration, jobs => jobs
            .AddOnDemand<DatabaseBackupJob>("database-backup", DatabaseBackupJob.Description)
            .AddOnDemand<DatabaseExportJob>("database-export", DatabaseExportJob.Description));
        var host = builder.Build();
        _hosts.Add(host);
        await host.StartAsync();
        return new BackupController(host.Services.GetRequiredService<DatabaseBackupService>(), host.Services.GetRequiredService<BackupRuns>(),
            host.Services.GetRequiredService<BackupDownloads>(), _context, Configuration(), NullLogger<BackupController>.Instance).WithUser("1", "admin", "Sysadmin");
    }

    /// <summary>A new SQLite file's connection string, unpooled, deleted after the test: an export target that every suite has.</summary>
    private string NewSqliteTarget()
    {
        var path = Path.Combine(Path.GetTempPath(), $"grif-backup-test-{Guid.NewGuid():N}.sqlite");
        _files.Add(path);
        return $"Data Source={path};Pooling=False";
    }

    private static async Task<BackupExportStatus> FinishedExportAsync(BackupController controller)
    {
        for (var i = 0; i < 1200 && (await controller.ExportStatus(CancellationToken.None)).State == ExportState.Running; i++)
            await Task.Delay(100);
        return await controller.ExportStatus(CancellationToken.None);
    }

    private static async Task<BackupDownloadStatus> StatusAsync(BackupController controller, string format)
        => (await controller.DownloadStatus(format, CancellationToken.None)).Value!;

    private static async Task<BackupDownloadStatus> FinishedDownloadAsync(BackupController controller, string format = "sqlite")
    {
        for (var i = 0; i < 1200 && (await StatusAsync(controller, format)).State == ExportState.Running; i++)
            await Task.Delay(100);
        return await StatusAsync(controller, format);
    }

    /// <summary>A backup queued, waited for and fetched, as the page does.</summary>
    private static async Task<FileStreamResult> DownloadAsync(BackupController controller, string format)
    {
        Assert.That(await controller.StartDownload(format, CancellationToken.None), Is.TypeOf<AcceptedResult>());
        var status = await FinishedDownloadAsync(controller, format);
        Assert.That(status.State, Is.EqualTo(ExportState.Succeeded), status.Message);
        return (FileStreamResult)await controller.Download(format, CancellationToken.None);
    }

    /// <summary>The download, copied to a file of the test's, the original closed.</summary>
    private async Task<string> SaveAsync(Stream download)
    {
        var copy = Path.Combine(Path.GetTempPath(), $"grif-backup-test-{Guid.NewGuid():N}.sqlite");
        _files.Add(copy);
        await using (download)
        await using (var file = File.Create(copy))
            await download.CopyToAsync(file);
        return copy;
    }

    [Test]
    public async Task Sqlite_downloads_the_whole_database_as_one_file()
    {
        _context.Seasons.Add(Season.Seed(seasonName: "Backed up"));
        await _context.SaveChangesAsync();

        var controller = await StartAsync();
        var file = await DownloadAsync(controller, "sqlite");

        var status = await StatusAsync(controller, "sqlite");
        Assert.Multiple(() =>
        {
            Assert.That(status.Format, Is.EqualTo("sqlite"));
            Assert.That(status.FileName, Is.EqualTo("grifball-backup-20261101-123045.sqlite"));
            Assert.That(status.Started, Is.Not.Null);
            Assert.That(status.Finished, Is.GreaterThanOrEqualTo(status.Started));
            Assert.That(status.Tables, Is.GreaterThan(0));
            Assert.That(file.FileDownloadName, Is.EqualTo("grifball-backup-20261101-123045.sqlite"));
            Assert.That(file.ContentType, Is.EqualTo("application/vnd.sqlite3"));
        });
        var original = ((FileStream)file.FileStream).Name;
        // Rolled back from WAL: nothing beside the file.
        Assert.That(status.Size, Is.EqualTo(new FileInfo(original).Length), "its size, for the page to show before it's downloaded");
        Assert.That(File.Exists(original + "-wal"), Is.False);

        var copy = await SaveAsync(file.FileStream);
        Assert.That(File.Exists(original), Is.True, "kept, so a download that broke off can be fetched again");
        await SaveAsync(((FileStreamResult)await controller.Download("sqlite", CancellationToken.None)).FileStream);

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
    public async Task Download_is_404_before_a_backup_is_made()
    {
        var controller = await StartAsync();

        Assert.That((await StatusAsync(controller, "sqlite")).State, Is.EqualTo(ExportState.None));
        Assert.That(await controller.Download("sqlite", CancellationToken.None), Is.TypeOf<NotFoundObjectResult>());
        Assert.That((await controller.DownloadStatus("mysql", CancellationToken.None)).Result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That(await controller.Download("mysql", CancellationToken.None), Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task A_made_backup_is_deleted_once_its_time_is_up()
    {
        // The backup's expiry on a fake clock, frozen while the backup is made and fetched, so its hour can't run out early
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var controller = await StartAsync(clock: clock);
        var file = await DownloadAsync(controller, "postgres");
        var path = ((FileStream)file.FileStream).Name;
        await file.FileStream.DisposeAsync();

        clock.Advance(TimeSpan.FromHours(2));

        Assert.Multiple(async () =>
        {
            Assert.That(File.Exists(path), Is.False);
            Assert.That((await StatusAsync(controller, "postgres")).State, Is.EqualTo(ExportState.None));
            Assert.That(await controller.Download("postgres", CancellationToken.None), Is.TypeOf<NotFoundObjectResult>());
        });
    }

    [Test]
    public async Task Each_format_keeps_one_backup_the_next_of_its_format_replaces()
    {
        var controller = await StartAsync();
        var first = await DownloadAsync(controller, "postgres");
        var firstPath = ((FileStream)first.FileStream).Name;
        await first.FileStream.DisposeAsync();

        // Another format: both kept, each shown and downloaded with its format
        var other = await DownloadAsync(controller, "sqlserver");
        var otherPath = ((FileStream)other.FileStream).Name;
        await other.FileStream.DisposeAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(File.Exists(firstPath), Is.True);
            Assert.That((await StatusAsync(controller, "postgres")).FileName, Does.EndWith(".postgres.sql"));
            Assert.That((await StatusAsync(controller, "sqlserver")).FileName, Does.EndWith(".sqlserver.sql"));
            Assert.That((await StatusAsync(controller, "sqlite")).State, Is.EqualTo(ExportState.None));
        });

        // The same format again: it replaces that one only
        var again = await DownloadAsync(controller, "postgres");
        await again.FileStream.DisposeAsync();
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(firstPath), Is.False);
            Assert.That(File.Exists(otherPath), Is.True);
            Assert.That(again.FileDownloadName, Does.EndWith(".postgres.sql"));
        });
    }

    [Test]
    public async Task A_failed_backup_says_why()
    {
        // A database that doesn't exist: the converter refuses.
        var missing = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, $"Missing_{Guid.NewGuid():N}");
        var controller = await StartAsync(Configuration(missing));

        Assert.That(await controller.StartDownload("sqlite", CancellationToken.None), Is.TypeOf<AcceptedResult>());

        var status = await FinishedDownloadAsync(controller);
        Assert.Multiple(async () =>
        {
            Assert.That(status.State, Is.EqualTo(ExportState.Failed));
            Assert.That(status.Format, Is.EqualTo("sqlite"));
            Assert.That(status.Message, Is.Not.Empty);
            Assert.That(await controller.Download("sqlite", CancellationToken.None), Is.TypeOf<NotFoundObjectResult>());
        });
        // Not left busy: the next one runs
        Assert.That(await controller.StartDownload("sqlite", CancellationToken.None), Is.TypeOf<AcceptedResult>());
        await FinishedDownloadAsync(controller);
    }

    [Test]
    public async Task A_backup_or_an_export_is_409_while_another_is_queued_or_runs()
    {
        var logging = new PausingLoggerFactory();
        var controller = await StartAsync(backupLogging: logging);
        Assert.That(await controller.StartDownload("sqlite", CancellationToken.None), Is.TypeOf<AcceptedResult>());
        Assert.That(logging.Paused.Wait(TimeSpan.FromMinutes(2)), Is.True, "the backup started");

        Assert.Multiple(async () =>
        {
            Assert.That((await StatusAsync(controller, "sqlite")).State, Is.EqualTo(ExportState.Running));
            // Another format's status says which is being made
            Assert.That((await StatusAsync(controller, "postgres")) is { State: ExportState.None, RunningFormat: "sqlite" }, Is.True);
            Assert.That(await controller.StartDownload("postgres", CancellationToken.None), Is.TypeOf<ConflictObjectResult>());
            Assert.That(await controller.Export(new ExportRequestDto("Sqlite", NewSqliteTarget()), CancellationToken.None), Is.TypeOf<ConflictObjectResult>());
            Assert.That((await controller.ExportStatus(CancellationToken.None)).State, Is.EqualTo(ExportState.None));
        });

        logging.Resume.Set();
        Assert.That((await FinishedDownloadAsync(controller)).State, Is.EqualTo(ExportState.Succeeded));
    }

    [Test]
    public async Task A_process_makes_one_backup_or_export_at_a_time_and_its_jobs_say_so()
    {
        var logging = new PausingLoggerFactory();
        var service = Service(logging);
        var first = Task.Run(() => service.CreateAsync(BackupFormat.Sqlite, CancellationToken.None));
        Assert.That(logging.Paused.Wait(TimeSpan.FromMinutes(2)), Is.True, "the first backup started");

        var protection = new ServiceCollection().AddDataProtection().Services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
        var backupRun = NSubstitute.Substitute.For<IJobExecutionContext>();
        var exportRun = NSubstitute.Substitute.For<IJobExecutionContext>();
        var directory = Path.Combine(Path.GetTempPath(), $"grif-backup-test-{Guid.NewGuid():N}");
        _directories.Add(directory);
        using var files = new BackupFiles(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DatabaseBackup:Directory"] = directory }).Build(), TimeProvider.System);
        await new DatabaseBackupJob(service, files, new ConfigurationBuilder().Build(), NullLogger<DatabaseBackupJob>.Instance).Execute(backupRun, new BackupRequest(BackupFormat.Postgres, "admin"));
        await new DatabaseExportJob(service, protection, NullLogger<DatabaseExportJob>.Instance)
            .Execute(exportRun, new ExportRequest(DatabaseProvider.Sqlite, BackupRuns.ExportProtector(protection).Protect(NewSqliteTarget(), BackupRuns.ExportRequestLifetime), "admin"));

        Assert.Multiple(async () =>
        {
            Assert.That(await service.MakeAsync(BackupFormat.Postgres, CancellationToken.None), Is.Null);
            Assert.That(await service.ExportAsync(DatabaseProvider.Sqlite, NewSqliteTarget(), CancellationToken.None), Is.Null);
            Assert.That(((JobRunReport)backupRun.Result!).Summary, Does.Contain("Another backup or export"));
            Assert.That(((JobRunReport)exportRun.Result!).Summary, Does.Contain("Another backup or export"));
        });

        logging.Resume.Set();
        var made = await first;
        await SaveAsync(made!.File);
        // Free again once it's done
        var next = await service.MakeAsync(BackupFormat.Postgres, CancellationToken.None);
        Assert.That(next, Is.Not.Null);
        File.Delete(next!.Path);
    }

    [Test]
    public async Task A_backup_made_on_a_worker_records_the_workers_address()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"grif-backup-test-{Guid.NewGuid():N}");
        _directories.Add(directory);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DatabaseBackup:Directory"] = directory,
            ["DatabaseBackup:NodeUrl"] = "http://10.42.0.7:5295",
        }).Build();
        using var files = new BackupFiles(configuration, TimeProvider.System);
        var run = NSubstitute.Substitute.For<IJobExecutionContext>();

        await new DatabaseBackupJob(Service(), files, configuration, NullLogger<DatabaseBackupJob>.Instance).Execute(run, new BackupRequest(BackupFormat.Postgres, "admin"));

        var report = (JobRunReport)run.Result!;
        Assert.Multiple(() =>
        {
            Assert.That(report.Result, Is.EqualTo(JobRunResult.Succeeded), report.Summary);
            Assert.That(report.Metrics![BackupRuns.NodeMetric], Is.EqualTo("http://10.42.0.7:5295"));
            Assert.That(files.Open((string)report.Metrics[BackupRuns.FileMetric]!), Is.Not.Null.And.Property("CanRead").True);
        });
    }

    /// <summary>Hears progress as it's reported (Progress<T> would post it to the thread pool).</summary>
    private sealed class Recorder : IProgress<ConversionProgress>
    {
        public List<ConversionProgress> Heard { get; } = [];
        public void Report(ConversionProgress value) => Heard.Add(value);
    }

    [Test]
    public async Task A_backup_and_an_export_say_how_far_they_got_table_by_table()
    {
        var script = new Recorder();
        var made = await Service().MakeAsync(BackupFormat.Postgres, CancellationToken.None, script);
        File.Delete(made!.Path);
        var export = new Recorder();
        var tables = await Service().ExportAsync(DatabaseProvider.Sqlite, NewSqliteTarget(), CancellationToken.None, export);

        Assert.Multiple(() =>
        {
            // A script: one step a table, as it's written
            Assert.That(script.Heard.Select(p => p.Done), Is.EqualTo(Enumerable.Range(0, made.Tables.Count)));
            Assert.That(script.Heard.Select(p => p.Total).Distinct(), Is.EqualTo(new[] { made.Tables.Count }));
            Assert.That(script.Heard[0].Message, Does.StartWith("Writing ").And.EndWith($"(1 of {made.Tables.Count})"));
            // A conversion: each table copied, then the copy checked
            Assert.That(export.Heard, Has.Count.EqualTo(tables!.Count * 2));
            Assert.That(export.Heard.Select(p => p.Percent), Is.Ordered);
            Assert.That(export.Heard[0].Message, Does.StartWith("Copying "));
            Assert.That(export.Heard[^1].Message, Is.EqualTo($"Checking the copy ({tables.Count} of {tables.Count})"));
            Assert.That(new ConversionProgress(1, 0, "x").Percent, Is.Zero, "nothing to do: no division");
        });
    }

    [Test]
    public async Task Export_copies_the_database_into_a_new_one()
    {
        _context.Seasons.Add(Season.Seed(seasonName: "Exported"));
        await _context.SaveChangesAsync();
        var target = NewSqliteTarget();
        var controller = await StartAsync();
        Assert.That((await controller.ExportStatus(CancellationToken.None)).State, Is.EqualTo(ExportState.None));

        var started = await controller.Export(new ExportRequestDto("sqlite", target), CancellationToken.None);

        Assert.That(((BackupExportStatus)((AcceptedResult)started).Value!).Provider, Is.EqualTo("Sqlite"));
        var status = await FinishedExportAsync(controller);
        Assert.Multiple(() =>
        {
            Assert.That(status.State, Is.EqualTo(ExportState.Succeeded), status.Message);
            Assert.That(status.Provider, Is.EqualTo("Sqlite"));
            Assert.That(status.Finished, Is.GreaterThanOrEqualTo(status.Started));
            Assert.That(status.Tables, Is.GreaterThan(0));
            Assert.That(status.Rows, Is.GreaterThan(0));
        });

        await using var exported = DatabaseConverter.NewContext(new DatabaseEndpoint(DatabaseProvider.Sqlite, target));
        Assert.That(await exported.Seasons.Select(s => s.SeasonName).ToListAsync(), Is.EqualTo(new[] { "Exported" }));
    }

    [Test]
    public async Task Export_never_fills_a_database_with_tables()
    {
        // The app's own database: the converter refuses it, so the live data can't be overwritten.
        var controller = await StartAsync();

        await controller.Export(new ExportRequestDto(TestDatabase.Provider.ToString(), _context.Database.GetConnectionString()!), CancellationToken.None);

        var status = await FinishedExportAsync(controller);
        Assert.That(status.State, Is.EqualTo(ExportState.Failed));
        Assert.That(status.Message, Does.Contain("already has tables"));
    }

    [Test]
    public async Task An_exports_connection_string_never_reaches_the_job_store_in_the_clear()
    {
        // A backup under way holds the one-at-a-time group, so an export queued meanwhile waits in the store
        var logging = new PausingLoggerFactory();
        var controller = await StartAsync(backupLogging: logging);
        Assert.That(await controller.StartDownload("sqlite", CancellationToken.None), Is.TypeOf<AcceptedResult>());
        Assert.That(logging.Paused.Wait(TimeSpan.FromMinutes(2)), Is.True, "the backup started");
        var host = _hosts.Single();
        var protection = host.Services.GetRequiredService<IDataProtectionProvider>();
        var target = NewSqliteTarget();
        var request = new ExportRequest(DatabaseProvider.Sqlite, BackupRuns.ExportProtector(protection).Protect(target, BackupRuns.ExportRequestLifetime), "admin");
        await host.Services.GetRequiredService<OnDemandJobs>().Enqueue<DatabaseExportJob, ExportRequest>(DatabaseExportJob.RunName, request, null, BackupRuns.ExecutionGroup);

        var scheduler = await host.Services.GetRequiredService<ISchedulerFactory>().GetScheduler();
        var stored = await scheduler.GetTrigger(OnDemandJobs.TriggerKeyOf<DatabaseExportJob>(DatabaseExportJob.RunName));
        var input = string.Join(" ", stored!.JobDataMap.Values.Select(v => v?.ToString()));
        Assert.Multiple(() =>
        {
            Assert.That(input, Does.Not.Contain(target), "only the protected form is stored");
            Assert.That(BackupRuns.ExportProtector(protection).Unprotect(request.ProtectedConnectionString), Is.EqualTo(target));
        });

        // Once the backup is done, the export runs, reading it back
        logging.Resume.Set();
        await FinishedDownloadAsync(controller);
        Assert.That((await FinishedExportAsync(controller)).State, Is.EqualTo(ExportState.Succeeded));
    }

    [Test]
    public async Task An_export_whose_connection_string_cant_be_read_says_to_start_it_again()
    {
        await StartAsync();
        var protection = _hosts.Single().Services.GetRequiredService<IDataProtectionProvider>();
        var context = NSubstitute.Substitute.For<IJobExecutionContext>();

        // Expired, or protected with another app's keys
        await new DatabaseExportJob(Service(), protection, NullLogger<DatabaseExportJob>.Instance)
            .Execute(context, new ExportRequest(DatabaseProvider.Sqlite, "not-protected", "admin"));

        Assert.That(((JobRunReport)context.Result!).Summary, Does.Contain("start it again"));
        Assert.That(((JobRunReport)context.Result!).Result, Is.EqualTo(JobRunResult.Failed));
    }

    [TestCase("MySql", "Data Source=x", "The provider must be")]
    [TestCase("7", "Data Source=x", "The provider must be")]
    [TestCase("Postgres", "Nonsense=1", "isn't one Postgres reads")]
    [TestCase("SqlServer", "Nonsense=1", "isn't one SqlServer reads")]
    [TestCase("Sqlite", " ", "A connection string is required")]
    public async Task Export_is_400_for_a_bad_request(string provider, string connectionString, string message)
    {
        var result = await (await StartAsync()).Export(new ExportRequestDto(provider, connectionString), CancellationToken.None);

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That((string)((BadRequestObjectResult)result).Value!, Does.Contain(message));
    }

    [TestCase("postgres", "postgres.sql", false)]
    [TestCase("SqlServer", "sqlserver.sql", true)]
    public async Task Download_writes_each_servers_script(string format, string suffix, bool byteOrderMark)
    {
        var file = await DownloadAsync(await StartAsync(), format);
        Assert.That(file.FileDownloadName, Is.EqualTo($"grifball-backup-20261101-123045.{suffix}"));
        Assert.That(file.ContentType, Is.EqualTo("application/sql"));
        var bytes = await File.ReadAllBytesAsync(await SaveAsync(file.FileStream));
        Assert.That(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }), Is.EqualTo(byteOrderMark));
        Assert.That(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('﻿'), Does.StartWith("-- GrifballWebApp database export"));
    }

    [Test]
    public async Task Download_is_400_for_another_format()
    {
        var controller = await StartAsync();

        Assert.That(await controller.StartDownload("mysql", CancellationToken.None), Is.TypeOf<BadRequestObjectResult>());
        Assert.That(await controller.StartDownload("9", CancellationToken.None), Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Download_native_is_the_servers_own_backup()
    {
        if (TestDatabase.Provider == DatabaseProvider.Sqlite)
            Assert.Ignore("SQLite has no native backup of its own: its file is the sqlite format.");
        var configuration = Configuration();
        // pg_dump as the suite's runner may not have it, or have an older one than the server: a stand-in that writes
        // the archive's magic.
        var pgDump = NativeBackupsTests.FakePgDump("printf PGDMP > \"$file\"");
        _files.Add(pgDump);
        configuration[NativeBackups.PgDumpKey] = pgDump;
        var bak = TestDatabase.Provider == DatabaseProvider.SqlServer;

        var file = await DownloadAsync(await StartAsync(configuration), bak ? "bak" : "dump");
        var bytes = await File.ReadAllBytesAsync(await SaveAsync(file.FileStream));
        Assert.That(file.FileDownloadName, Does.EndWith(bak ? ".bak" : ".dump"));
        // A compressed .bak's header (NativeBackupsTests.CompressedBakHeader); pg_dump's archive's
        Assert.That(System.Text.Encoding.ASCII.GetString(bytes, 0, bak ? 4 : 5), Is.EqualTo(bak ? NativeBackupsTests.CompressedBakHeader : "PGDMP"));
    }

    [Test]
    public async Task Download_native_of_the_other_kind_is_422_without_a_scratch_server()
    {
        var other = TestDatabase.Provider == DatabaseProvider.SqlServer ? "dump" : "bak";

        var result = await (await StartAsync()).StartDownload(other, CancellationToken.None);

        Assert.That(result, Is.TypeOf<UnprocessableEntityObjectResult>());
        Assert.That((string)((UnprocessableEntityObjectResult)result).Value!, Does.Contain("backupScratch"));
    }

    [Test]
    public async Task Download_native_fails_saying_why_when_the_server_cant_make_it_here()
    {
        if (TestDatabase.Provider == DatabaseProvider.Sqlite)
            Assert.Ignore("SQLite has no native backup of its own.");
        var configuration = Configuration();
        configuration[NativeBackups.PgDumpKey] = "/nonexistent/pg_dump";
        if (TestDatabase.Provider == DatabaseProvider.SqlServer)
            configuration["ConnectionStrings:GrifballWebApp"] = await NativeBackupsTests.ReadOnlyLoginAsync(_context.Database.GetConnectionString()!);

        var controller = await StartAsync(configuration);
        var native = TestDatabase.Provider == DatabaseProvider.SqlServer ? "bak" : "dump";
        Assert.That(await controller.StartDownload(native, CancellationToken.None), Is.TypeOf<AcceptedResult>());

        var status = await FinishedDownloadAsync(controller, native);
        Assert.That(status.State, Is.EqualTo(ExportState.Failed));
        Assert.That(status.Message, Does.Contain("instead"));
        Assert.That(await controller.Download(native, CancellationToken.None), Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public void Describe_points_at_the_network_policy_when_a_server_cant_be_reached()
    {
        Assert.That(DatabaseBackupService.Describe(new ConversionException("Refused.")), Is.EqualTo("Refused."));
        Assert.That(DatabaseBackupService.Describe(new TimeoutException("Timed out.")), Does.StartWith("Timed out.").And.Contain("networkPolicy.backend.extraEgress"));
        Assert.That(DatabaseBackupService.Describe(new SqliteException("Unable to open.", 14)), Does.Contain("networkPolicy.backend.extraEgress"));
        Assert.That(DatabaseBackupService.Describe(new InvalidOperationException("Other.")), Is.EqualTo("Other."));
    }

    [TestCase(DatabaseProvider.SqlServer, null, null, new[] { "Sqlite", "Postgres", "SqlServer", "Bak" })]
    [TestCase(DatabaseProvider.Postgres, null, null, new[] { "Sqlite", "Postgres", "SqlServer", "Dump" })]
    [TestCase(DatabaseProvider.Sqlite, null, null, new[] { "Sqlite", "Postgres", "SqlServer" })]
    [TestCase(DatabaseProvider.SqlServer, "Postgres", "Host=scratch;Database=postgres", new[] { "Sqlite", "Postgres", "SqlServer", "Bak", "Dump" })]
    [TestCase(DatabaseProvider.Postgres, "SqlServer", "Server=scratch;Database=master", new[] { "Sqlite", "Postgres", "SqlServer", "Bak", "Dump" })]
    [TestCase(DatabaseProvider.Sqlite, "Postgres", "Host=scratch;Database=postgres", new[] { "Sqlite", "Postgres", "SqlServer", "Dump" })]
    // An API pod beside a worker: the scratch server's kind only (its login is the worker's), still offering its format.
    [TestCase(DatabaseProvider.SqlServer, "Postgres", null, new[] { "Sqlite", "Postgres", "SqlServer", "Bak", "Dump" })]
    public void Formats_are_the_scripts_and_the_native_backups_of_the_servers_there(DatabaseProvider provider, string? scratch, string? scratchConnection, string[] formats)
    {
        var configuration = Configuration();
        configuration["Database:Provider"] = provider.ToString();
        configuration["DatabaseBackup:Scratch:Provider"] = scratch;
        configuration["DatabaseBackup:Scratch:ConnectionString"] = scratchConnection;

        Assert.That(Service(configuration: configuration).Formats.Select(f => f.ToString()), Is.EqualTo(formats));
    }

    [Test]
    public async Task A_backup_a_restart_took_is_no_longer_offered()
    {
        var controller = await StartAsync();
        Assert.That(await controller.StartDownload("sqlite", CancellationToken.None), Is.TypeOf<AcceptedResult>());
        Assert.That((await FinishedDownloadAsync(controller)).State, Is.EqualTo(ExportState.Succeeded));

        // The pod that made it restarted: its run still says it's ready, its file is gone.
        foreach (var file in Directory.GetFiles(_directories[^1]))
            File.Delete(file);

        var status = await StatusAsync(controller, "sqlite");
        Assert.That((status.State, status.Message), Is.EqualTo((ExportState.None, BackupController.Gone)));
        Assert.That(await controller.Download("sqlite", CancellationToken.None), Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public void An_API_pod_with_the_scratch_servers_kind_only_accepts_its_format_and_leaves_making_it_to_the_worker()
    {
        var configuration = Configuration();
        configuration["Database:Provider"] = "SqlServer";
        configuration["DatabaseBackup:Scratch:Provider"] = "Postgres";
        var service = Service(configuration: configuration);

        Assert.DoesNotThrow(() => service.RequireNativeServer(BackupFormat.Dump));
        // Only the worker, with the login, makes it.
        Assert.Throws<NativeBackupUnavailableException>(() => service.RequireNativeSource(BackupFormat.Dump, out _));

        configuration["DatabaseBackup:Scratch:Provider"] = null;
        Assert.That(Assert.Throws<NativeBackupUnavailableException>(() => service.RequireNativeServer(BackupFormat.Dump))!.Message, Does.Contain("backupScratch"));
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
        var dump = await Service(configuration: configuration).CreateAsync(BackupFormat.Dump, CancellationToken.None);
        Assert.That(dump!.FileName, Does.EndWith(".dump"));
        Assert.That(await File.ReadAllTextAsync(await SaveAsync(dump.File)), Is.EqualTo("PGDMP"));

        // SQL Server's: BACKUP DATABASE on its server, here one that isn't there.
        configuration["ConnectionStrings:GrifballWebApp"] = "Server=127.0.0.1,9;Database=nope;User Id=sa;Password=x;Connect Timeout=2;TrustServerCertificate=True";
        configuration["Database:Provider"] = "SqlServer";
        Assert.That(await Assert.CatchAsync(() => Service(configuration: configuration).CreateAsync(BackupFormat.Bak, CancellationToken.None)),
            Is.InstanceOf<Microsoft.Data.SqlClient.SqlException>());
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
        var service = new DatabaseBackupService(new ConfigurationBuilder().Build(), NullLoggerFactory.Instance, TimeProvider.System);

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(BackupFormat.Sqlite, CancellationToken.None));
        Assert.That(e!.Message, Does.Contain("ConnectionStrings:GrifballWebApp"));
    }

    [Test]
    public async Task Info_gives_the_server_and_the_last_migration()
    {
        var info = (await StartAsync()).Info();

        Assert.That(info.Provider, Is.EqualTo(TestDatabase.Provider.ToString()));
        Assert.That(info.LatestMigration, Is.EqualTo(_context.Database.GetMigrations().Last()));
        Assert.That(info.Formats, Is.EqualTo(TestDatabase.Provider switch
        {
            DatabaseProvider.SqlServer => new[] { "sqlite", "postgres", "sqlserver", "bak" },
            DatabaseProvider.Postgres => new[] { "sqlite", "postgres", "sqlserver", "dump" },
            _ => new[] { "sqlite", "postgres", "sqlserver" },
        }));
    }

    [Test]
    public void Is_sysadmin_only()
    {
        Assert.That(ControllerTestHelpers.ClassAuthorize(typeof(BackupController))?.Roles, Is.EqualTo("Sysadmin"));
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
