using GrifballWebApp.Infrastructure.DatabaseBackup;
using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using Quartz.Extensibility;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// BackupRuns: the Database Backup page's statuses, read from the backup and export jobs' state and last run, and
/// starting one, one at a time. A real OnDemandJobs over a substituted Quartz scheduler and execution history: no
/// database, host or network. The clock is fake, so how long a made backup stays ready is exact.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class BackupRunsStatusUnitTests
{
    private const string SchedulerName = "test-scheduler";
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);
    private static readonly TriggerKey PostgresKey = OnDemandJobs.TriggerKeyOf<DatabaseBackupJob>(DatabaseBackupJob.RunName(BackupFormat.Postgres));
    private static readonly TriggerKey DumpKey = OnDemandJobs.TriggerKeyOf<DatabaseBackupJob>(DatabaseBackupJob.RunName(BackupFormat.Dump));
    private static readonly TriggerKey ExportKey = OnDemandJobs.TriggerKeyOf<DatabaseExportJob>(DatabaseExportJob.RunName);

    private const string ReadyMetrics = """
        {"file":"abc123","fileName":"grifball-backup-20261113-073000.dump","contentType":"application/octet-stream","size":5,"format":"dump","node":"http://10.42.0.7:5295"}
        """;

    private readonly ISchedulerFactory _schedulers = Substitute.For<ISchedulerFactory>();
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
    private readonly IExecutionHistoryStore _history = Substitute.For<IExecutionHistoryStore>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();

    [SetUp]
    public void SetUp()
    {
        _schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        _scheduler.SchedulerName.Returns(SchedulerName);
        // The one-off triggers are built on the scheduler's clock
        _scheduler.TimeProvider.Returns(_time);
        _scheduler.QueryFireInstances(Arg.Any<FireInstanceQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<FireInstance>([], false));
        _scheduler.GetTriggerState(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(TriggerState.None);
        _history.QueryExecutions(Arg.Any<ExecutionHistoryQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<ExecutionHistoryEntry>([], false));
    }

    private BackupRuns Runs(IConfiguration? configuration = null) =>
        new(new OnDemandJobs(_schedulers, _history), _protection, _time, configuration ?? new ConfigurationBuilder().Build());

    /// <summary>The trigger's last run, as the execution history keeps it.</summary>
    private void LastRun(TriggerKey key, DateTimeOffset firedAt, TimeSpan duration, JobRunResult result, string? summary = null, string? metricsJson = null)
    {
        var run = new ExecutionHistoryEntry(SchedulerName, "worker-1", key.Group, key.Name, key.Group, key.Name, firedAt, duration, true, null)
        {
            Result = result,
            Summary = summary,
            MetricsJson = metricsJson,
        };
        _history.QueryExecutions(Arg.Is<ExecutionHistoryQuery>(q => key.Equals(q.Trigger)), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ExecutionHistoryEntry>([run], false));
    }

    /// <summary>The trigger fired and its job is executing, this far along.</summary>
    private void Firing(TriggerKey key, int progress, string message)
    {
        var firing = new FireInstance("fire-1", key, null, "worker-1", FireInstanceState.Executing, Now.AddMinutes(-2), null, null)
        {
            Progress = progress,
            ProgressMessage = message,
        };
        _scheduler.QueryFireInstances(Arg.Any<FireInstanceQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<FireInstance>([firing], false));
    }

    private void Queued(TriggerKey key) => _scheduler.GetTriggerState(key, Arg.Any<CancellationToken>()).Returns(TriggerState.Normal);

    [Test]
    public async Task DownloadStatus_NeverMade_IsNone_WithNothingRunning()
    {
        var status = await Runs().DownloadStatusAsync(BackupFormat.Sqlite);

        Assert.That(status, Is.EqualTo(new BackupDownloadStatus(ExportState.None, "sqlite")));
    }

    [Test]
    public async Task DownloadStatus_AnotherFormatBeingMade_SaysWhich_AndThatFormatsOwnIsRunning()
    {
        Firing(PostgresKey, 40, "Writing Users (4 of 10)");
        var runs = Runs();

        var sqlite = await runs.DownloadStatusAsync(BackupFormat.Sqlite);
        var postgres = await runs.DownloadStatusAsync(BackupFormat.Postgres);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((sqlite.State, sqlite.RunningFormat), Is.EqualTo((ExportState.None, (string?)"postgres")));
            Assert.That((postgres.State, postgres.Format, postgres.Progress, postgres.ProgressMessage, postgres.Started),
                Is.EqualTo((ExportState.Running, (string?)"postgres", (int?)40, (string?)"Writing Users (4 of 10)", (DateTimeOffset?)Now.AddMinutes(-2))));
        }
    }

    [Test]
    public async Task DownloadStatus_MadeWithinReadyFor_IsReady_WithItsFileSizeAndTimes()
    {
        var fired = Now.AddMinutes(-30);
        LastRun(DumpKey, fired, TimeSpan.FromSeconds(90), JobRunResult.Succeeded, "made", ReadyMetrics);

        var status = await Runs().DownloadStatusAsync(BackupFormat.Dump);

        Assert.That(status, Is.EqualTo(new BackupDownloadStatus(ExportState.Succeeded, "dump", fired, fired.AddSeconds(90),
            "grifball-backup-20261113-073000.dump", Tables: null, Rows: null, Size: 5)));
    }

    [Test]
    public async Task DownloadStatus_ATableByTableBackup_CarriesItsCounts()
    {
        LastRun(PostgresKey, Now.AddMinutes(-5), TimeSpan.FromSeconds(10), JobRunResult.Succeeded, "made",
            """{"file":"f","fileName":"b.sql","tables":12,"rows":3400}""");

        var status = await Runs().DownloadStatusAsync(BackupFormat.Postgres);

        Assert.That((status.State, status.Tables, status.Rows, status.Size), Is.EqualTo((ExportState.Succeeded, (int?)12, (int?)3400, (long?)null)));
    }

    [Test]
    public async Task DownloadStatus_OlderThanReadyFor_IsNone_AsIfNeverMade()
    {
        // Finished 61 minutes ago: the default hour is up
        LastRun(DumpKey, Now.AddMinutes(-62), TimeSpan.FromMinutes(1), JobRunResult.Succeeded, "made", ReadyMetrics);

        var status = await Runs().DownloadStatusAsync(BackupFormat.Dump);

        Assert.That(status, Is.EqualTo(new BackupDownloadStatus(ExportState.None, "dump")));
    }

    [Test]
    public async Task DownloadStatus_ReadyForFromConfiguration_KeepsItLonger()
    {
        LastRun(DumpKey, Now.AddMinutes(-62), TimeSpan.FromMinutes(1), JobRunResult.Succeeded, "made", ReadyMetrics);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DatabaseBackup:ReadyFor"] = "02:00:00" }).Build();

        var status = await Runs(configuration).DownloadStatusAsync(BackupFormat.Dump);

        Assert.That(status.State, Is.EqualTo(ExportState.Succeeded));
    }

    [Test]
    public async Task DownloadStatus_AFailedRun_SaysWhy()
    {
        var fired = Now.AddMinutes(-3);
        LastRun(DumpKey, fired, TimeSpan.FromSeconds(4), JobRunResult.Failed, "pg_dump failed (1): no route to host", """{"format":"dump"}""");

        var status = await Runs().DownloadStatusAsync(BackupFormat.Dump);

        Assert.That(status, Is.EqualTo(new BackupDownloadStatus(ExportState.Failed, "dump", fired, fired.AddSeconds(4), Message: "pg_dump failed (1): no route to host")));
    }

    [Test]
    public async Task Ready_AMadeBackup_IsItsFileOnTheNodeThatMadeIt()
    {
        LastRun(DumpKey, Now.AddMinutes(-30), TimeSpan.FromSeconds(90), JobRunResult.Succeeded, "made", ReadyMetrics);

        var ready = await Runs().ReadyAsync(BackupFormat.Dump);

        Assert.That(ready, Is.EqualTo(new ReadyBackup("abc123", "grifball-backup-20261113-073000.dump", "application/octet-stream", "http://10.42.0.7:5295")));
    }

    [Test]
    public async Task Ready_WithNoContentType_IsAnOctetStream_OnThisPod()
    {
        LastRun(DumpKey, Now.AddMinutes(-30), TimeSpan.FromSeconds(90), JobRunResult.Succeeded, "made", """{"file":"abc","fileName":"b.dump"}""");

        var ready = await Runs().ReadyAsync(BackupFormat.Dump);

        Assert.That(ready, Is.EqualTo(new ReadyBackup("abc", "b.dump", "application/octet-stream")));
    }

    [TestCase(JobRunResult.Failed, -30, ReadyMetrics, Description = "it failed")]
    [TestCase(JobRunResult.Succeeded, -90, ReadyMetrics, Description = "it's no longer kept")]
    [TestCase(JobRunResult.Succeeded, -30, """{"fileName":"b.dump"}""", Description = "no file was recorded")]
    public async Task Ready_IsNull_WhenThereIsNothingToFetch(JobRunResult result, int firedMinutesAgo, string metrics)
    {
        LastRun(DumpKey, Now.AddMinutes(firedMinutesAgo), TimeSpan.FromSeconds(90), result, "summary", metrics);

        Assert.That(await Runs().ReadyAsync(BackupFormat.Dump), Is.Null);
    }

    [Test]
    public async Task Ready_WhileTheNextOneIsBeingMade_IsNull()
    {
        LastRun(DumpKey, Now.AddMinutes(-30), TimeSpan.FromSeconds(90), JobRunResult.Succeeded, "made", ReadyMetrics);
        Queued(DumpKey);

        Assert.That(await Runs().ReadyAsync(BackupFormat.Dump), Is.Null);
    }

    [Test]
    public async Task ExportStatus_NeverRun_IsNone()
        => Assert.That(await Runs().ExportStatusAsync(), Is.EqualTo(new BackupExportStatus(ExportState.None)));

    [Test]
    public async Task ExportStatus_Queued_IsRunning()
    {
        Queued(ExportKey);

        Assert.That((await Runs().ExportStatusAsync()).State, Is.EqualTo(ExportState.Running));
    }

    [Test]
    public async Task ExportStatus_Succeeded_HasItsProviderAndCounts()
    {
        var fired = Now.AddMinutes(-10);
        LastRun(ExportKey, fired, TimeSpan.FromMinutes(2), JobRunResult.Succeeded, "Postgres: 40 tables, 900 rows", """{"provider":"Postgres","tables":40,"rows":900}""");

        var status = await Runs().ExportStatusAsync();

        Assert.That(status, Is.EqualTo(new BackupExportStatus(ExportState.Succeeded, "Postgres", fired, fired.AddMinutes(2), 40, 900)));
    }

    [Test]
    public async Task ExportStatus_Failed_SaysWhy()
    {
        var fired = Now.AddMinutes(-10);
        LastRun(ExportKey, fired, TimeSpan.FromSeconds(3), JobRunResult.Failed, "The target database already has tables", """{"provider":"SqlServer"}""");

        var status = await Runs().ExportStatusAsync();

        Assert.That(status, Is.EqualTo(new BackupExportStatus(ExportState.Failed, "SqlServer", fired, fired.AddSeconds(3), Message: "The target database already has tables")));
    }

    [Test]
    public async Task StartDownload_NothingRunning_QueuesTheFormatsRun_InTheBackupsGroup()
    {
        var status = await Runs().StartDownloadAsync(BackupFormat.Dump, "admin", 7);

        await _scheduler.Received(1).ScheduleTrigger(
            Arg.Is<ITrigger>(t => t.Key.Equals(DumpKey) && t.ExecutionGroup == BackupRuns.ExecutionGroup
                && new BackupRequest(BackupFormat.Dump, "admin", 7).Equals(t.JobDataMap[SchedulerConstants.JobInput])),
            TriggerConflict.Keep, Arg.Any<CancellationToken>());
        Assert.That(status?.Format, Is.EqualTo("dump"));
    }

    [Test]
    public async Task StartDownload_AnExportQueued_StartsNothing()
    {
        Queued(ExportKey);

        var status = await Runs().StartDownloadAsync(BackupFormat.Sqlite, "admin", 7);

        Assert.That(status, Is.Null);
        await _scheduler.DidNotReceiveWithAnyArgs().ScheduleTrigger(default!, default, default);
    }

    [Test]
    public async Task StartDownload_AnotherFormatBeingMade_StartsNothing()
    {
        Firing(PostgresKey, 10, "Writing");

        Assert.That(await Runs().StartDownloadAsync(BackupFormat.Sqlite, "admin", 7), Is.Null);
        await _scheduler.DidNotReceiveWithAnyArgs().ScheduleTrigger(default!, default, default);
    }

    [Test]
    public async Task StartExport_QueuesItWithTheConnectionStringProtected()
    {
        const string connectionString = "Data Source=/data/copy.db";

        var status = await Runs().StartExportAsync(DatabaseProvider.Sqlite, connectionString, "admin", 7);

        var call = _scheduler.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IScheduler.ScheduleTrigger));
        var trigger = (ITrigger)call.GetArguments()[0]!;
        var request = (ExportRequest)trigger.JobDataMap[SchedulerConstants.JobInput]!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(trigger.Key, Is.EqualTo(ExportKey));
            Assert.That((request.Provider, request.RequestedBy, request.RequestedByUserID), Is.EqualTo((DatabaseProvider.Sqlite, (string?)"admin", (int?)7)));
            Assert.That(request.ProtectedConnectionString, Does.Not.Contain("copy.db"), "never in the job store in the clear");
            Assert.That(BackupRuns.ExportProtector(_protection).Unprotect(request.ProtectedConnectionString), Is.EqualTo(connectionString));
            Assert.That(status?.Provider, Is.EqualTo("Sqlite"));
        }
    }

    [Test]
    public async Task StartExport_ABackupBeingMade_StartsNothing()
    {
        Firing(DumpKey, 50, "Making the .dump");

        Assert.That(await Runs().StartExportAsync(DatabaseProvider.Postgres, "Host=db;Database=copy", "admin", 7), Is.Null);
        await _scheduler.DidNotReceiveWithAnyArgs().ScheduleTrigger(default!, default, default);
    }

    [Test]
    public async Task StartExport_AConnectionStringTheProviderCantRead_IsRefused_BeforeAnythingIsQueued()
    {
        Assert.That(async () => await Runs().StartExportAsync(DatabaseProvider.Postgres, "Nonsense Key=1", "admin", 7), Throws.InstanceOf<ArgumentException>());
        await _schedulers.DidNotReceiveWithAnyArgs().GetScheduler(default);
    }
}
