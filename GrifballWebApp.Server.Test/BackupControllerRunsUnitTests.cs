using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Server.DatabaseBackup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using Quartz.Extensibility;
using System.Text.Json;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// BackupController past its validation: starting a backup or an export (queued on an in-memory Quartz,
/// <see cref="InMemoryQuartz"/>), one at a time; and a made backup's status and file, read from a run history that says
/// one was made (substituted) and a file kept in a folder of the test's own. No database, host or network.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class BackupControllerRunsUnitTests
{
    private static readonly DateTimeOffset MadeAt = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly RecordingLogger<BackupController> _logger = new();
    private readonly FakeTimeProvider _time = new(MadeAt.AddMinutes(5));
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"grif-backup-controller-{Guid.NewGuid():N}");
    private InMemoryQuartz? _quartz;
    private BackupFiles? _files;

    /// <summary>The backups kept, in the test's own folder (made once: it empties the folder as it starts).</summary>
    private BackupFiles Files => _files ??= new BackupFiles(Configuration(_directory), _time);

    [TearDown]
    public async Task TearDown()
    {
        if (_quartz is not null)
            await _quartz.DisposeAsync();
        if (_files is not null)
            _files.Dispose();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    /// <summary>The site on SQLite with no scratch server: no native backup can be made.</summary>
    private static readonly IConfiguration OnSqlite = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Provider"] = "Sqlite" })
        .Build();

    private static IConfiguration Configuration(string directory) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["DatabaseBackup:Directory"] = directory })
        .Build();

    private BackupController Controller(OnDemandJobs jobs)
    {
        var configuration = Configuration(_directory);
        var runs = new BackupRuns(jobs, new EphemeralDataProtectionProvider(), _time, configuration);
        var downloads = new BackupDownloads(Files, Substitute.For<IHttpClientFactory>(), new EphemeralDataProtectionProvider());
        return new BackupController(new DatabaseBackupService(OnSqlite, NullLoggerFactory.Instance, _time), runs, downloads, null!, configuration, _logger)
            .WithUser("7", "admin", "Sysadmin");
    }

    /// <summary>The controller over an in-memory Quartz: what it queues stays queued.</summary>
    private async Task<BackupController> QueuingController()
    {
        _quartz = await InMemoryQuartz.Create();
        return Controller(_quartz.OnDemand);
    }

    /// <summary>The controller over a run history whose every backup run is <paramref name="run"/>, and nothing queued.</summary>
    private BackupController ControllerAfter(ExecutionHistoryEntry run)
    {
        var schedulers = Substitute.For<ISchedulerFactory>();
        var scheduler = Substitute.For<IScheduler>();
        schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(scheduler);
        scheduler.SchedulerName.Returns("backup-test");
        scheduler.QueryFireInstances(Arg.Any<FireInstanceQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<FireInstance>([], false));
        scheduler.GetTriggerState(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(TriggerState.None);
        var history = Substitute.For<IExecutionHistoryStore>();
        history.QueryExecutions(Arg.Any<ExecutionHistoryQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<ExecutionHistoryEntry>([run], false));
        return Controller(new OnDemandJobs(schedulers, history));
    }

    /// <summary>A backup run that made a SQLite file, kept as <paramref name="fileId"/>.</summary>
    private static ExecutionHistoryEntry MadeBackup(string fileId) => new("backup-test", "worker-1", "DatabaseBackupJob", "database-backup-sqlite",
        "DatabaseBackupJob", "database-backup-sqlite", MadeAt, TimeSpan.FromSeconds(30), true, null)
    {
        MetricsJson = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            [BackupRuns.FileMetric] = fileId,
            [BackupRuns.FileNameMetric] = "grif.sqlite",
            [BackupRuns.ContentTypeMetric] = "application/x-sqlite3",
            [BackupRuns.TablesMetric] = 12,
            [BackupRuns.RowsMetric] = 345,
        }),
    };

    /// <summary>A file kept as the SQLite backup; its ID.</summary>
    private string KeepBackup(string text)
    {
        var files = Files;
        var made = Path.Combine(_directory, $"made-{Guid.NewGuid():N}");
        File.WriteAllText(made, text);
        return files.Keep(BackupFormat.Sqlite, made);
    }

    [Test]
    public async Task StartDownload_QueuesTheBackup_AndLogsWhoStartedIt()
    {
        var controller = await QueuingController();

        var result = await controller.StartDownload("sqlite", CancellationToken.None);

        Assert.That(result, Is.InstanceOf<AcceptedResult>());
        var status = (BackupDownloadStatus)((AcceptedResult)result).Value!;
        var queued = await _quartz!.Scheduler.GetTriggerState(OnDemandJobs.TriggerKeyOf<DatabaseBackupJob>(DatabaseBackupJob.RunName(BackupFormat.Sqlite)));
        using (Assert.EnterMultipleScope())
        {
            Assert.That((status.State, status.Format, status.RunningFormat), Is.EqualTo((ExportState.Running, (string?)"sqlite", (string?)"sqlite")));
            Assert.That(queued, Is.EqualTo(TriggerState.Normal));
            Assert.That(_logger.Entries.Single().Message, Is.EqualTo("admin started a database backup (Sqlite)"));
        }
    }

    [Test]
    public async Task StartDownload_WhileOneIsQueued_IsAConflict()
    {
        var controller = await QueuingController();
        await controller.StartDownload("sqlite", CancellationToken.None);

        var result = await controller.StartDownload("postgres", CancellationToken.None);

        result.AssertProblem(StatusCodes.Status409Conflict, "A backup or export is already running; try again when it's done.");
    }

    [TestCase("bak", ".bak needs SQL Server")]
    [TestCase("dump", ".dump needs PostgreSQL")]
    public async Task StartDownload_ANativeBackupWithNoServerForIt_IsAConflictSayingWhy(string format, string why)
    {
        var controller = await QueuingController();

        var result = await controller.StartDownload(format, CancellationToken.None);

        var problem = result.AssertProblem(StatusCodes.Status409Conflict);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(problem.Detail, Does.Contain(why));
            Assert.That(_logger.Entries, Is.Empty, "Nothing was started");
        }
    }

    [Test]
    public async Task Export_QueuesTheExport_AndLogsWhoStartedIt()
    {
        var controller = await QueuingController();

        var result = await controller.Export(new ExportRequestDto("sqlite", "Data Source=copy.db"), CancellationToken.None);

        Assert.That(result, Is.InstanceOf<AcceptedResult>());
        var status = (BackupExportStatus)((AcceptedResult)result).Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That((status.State, status.Provider), Is.EqualTo((ExportState.Running, (string?)"Sqlite")));
            Assert.That((await controller.ExportStatus(CancellationToken.None)).State, Is.EqualTo(ExportState.Running));
            Assert.That(_logger.Entries.Single().Message, Is.EqualTo("admin started a database export to Sqlite"));
        }
    }

    [Test]
    public async Task Export_WhileABackupIsQueued_IsAConflict()
    {
        var controller = await QueuingController();
        await controller.StartDownload("sqlite", CancellationToken.None);

        var result = await controller.Export(new ExportRequestDto("Sqlite", "Data Source=copy.db"), CancellationToken.None);

        result.AssertProblem(StatusCodes.Status409Conflict, "A backup or export is already running; try again when it's done.");
    }

    [Test]
    public async Task Export_AConnectionStringTheProviderCantRead_IsABadRequestSayingSo()
    {
        var controller = await QueuingController();

        var result = await controller.Export(new ExportRequestDto("sqlite", "Nonsense Key=1"), CancellationToken.None);

        var problem = result.AssertProblem(StatusCodes.Status400BadRequest);
        Assert.That(problem.Detail, Does.StartWith("The connection string isn't one Sqlite reads: "));
    }

    [Test]
    public async Task ExportStatus_NothingQueuedOrRun_IsNone()
    {
        var controller = await QueuingController();

        var status = await controller.ExportStatus(CancellationToken.None);

        Assert.That(status.State, Is.EqualTo(ExportState.None));
    }

    [Test]
    public async Task DownloadStatus_AMadeBackupStillKept_IsSucceeded()
    {
        var id = KeepBackup("made here");
        var controller = ControllerAfter(MadeBackup(id));

        var status = (await controller.DownloadStatus("sqlite", CancellationToken.None)).Value!;

        Assert.That((status.State, status.FileName, status.Tables, status.Rows), Is.EqualTo((ExportState.Succeeded, (string?)"grif.sqlite", (int?)12, (int?)345)));
    }

    [Test]
    public async Task DownloadStatus_AMadeBackupWhoseFileIsGone_IsNone_SayingSo()
    {
        var controller = ControllerAfter(MadeBackup("gone-since-a-restart"));

        var status = (await controller.DownloadStatus("sqlite", CancellationToken.None)).Value!;

        Assert.That((status.State, status.Format, status.Message), Is.EqualTo((ExportState.None, (string?)"sqlite", (string?)BackupController.Gone)));
    }

    [Test]
    public async Task Download_AMadeBackup_IsTheFile_AndLogsWhoDownloadedIt()
    {
        var id = KeepBackup("made here");
        var controller = ControllerAfter(MadeBackup(id));

        var result = await controller.Download("sqlite", CancellationToken.None);

        Assert.That(result, Is.InstanceOf<FileStreamResult>());
        var file = (FileStreamResult)result;
        await using var stream = file.FileStream;
        using var reader = new StreamReader(stream);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((file.ContentType, file.FileDownloadName), Is.EqualTo(("application/x-sqlite3", "grif.sqlite")));
            Assert.That(await reader.ReadToEndAsync(), Is.EqualTo("made here"));
            Assert.That(_logger.Entries.Single().Message, Is.EqualTo("admin downloaded a database backup (grif.sqlite): 12 tables, 345 rows"));
            Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Information));
        }
    }

    [Test]
    public async Task Download_AMadeBackupWhoseFileIsGone_IsNotFound()
    {
        var controller = ControllerAfter(MadeBackup("gone-since-a-restart"));

        var result = await controller.Download("sqlite", CancellationToken.None);

        var problem = result.AssertProblem(StatusCodes.Status404NotFound);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(problem.Detail, Does.StartWith("No backup is ready in this format"));
            Assert.That(_logger.Entries, Is.Empty);
        }
    }
}
