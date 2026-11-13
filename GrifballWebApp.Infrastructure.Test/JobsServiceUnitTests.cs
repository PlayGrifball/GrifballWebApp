using Quartz;
using Quartz.Extensibility;
using GrifballWebApp.Infrastructure.Jobs;
using NSubstitute;
using static GrifballWebApp.Infrastructure.Test.JobsTestData;
using static GrifballWebApp.Infrastructure.Test.JobsUnitTests;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// JobsService, against a substituted ISchedulerFactory/IScheduler/IExecutionHistoryStore (Quartz's own store
/// boundary): no database, host or Quartz-managed scheduler. Triggers are real Quartz objects (RecurringJob.DefaultTrigger),
/// never substituted, since Quartz builds and reads their own state. GrifballWebApp.Integration.Test keeps the same
/// scenarios against a real scheduler and store, for its own layer's coverage.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class JobsServiceUnitTests
{
    private readonly ISchedulerFactory _schedulers = Substitute.For<ISchedulerFactory>();
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
    private readonly IExecutionHistoryStore _history = Substitute.For<IExecutionHistoryStore>();
    private const string SchedulerName = "test-scheduler";

    [SetUp]
    public void SetUp()
    {
        _schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        _scheduler.SchedulerName.Returns(SchedulerName);
        // Empty by default; each test sets up what it needs.
        _scheduler.QueryJobs(Arg.Any<JobQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobHeader>([], false));
        _scheduler.QueryFireInstances(Arg.Any<FireInstanceQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<FireInstance>([], false));
        _scheduler.QueryClusterNodes(Arg.Any<CancellationToken>()).Returns(new List<ClusterNode>());
        _history.QueryJobRunStatuses(Arg.Any<JobRunStatusQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobRunStatus>([], false));
    }

    private JobsService Service(RecurringJobs? recurring = null, TimeProvider? time = null)
        => new(_schedulers, _history, recurring ?? new RecurringJobs().Add<CountingJob>(Job(TimeSpan.FromHours(1))), time ?? TimeProvider.System);

    private static JobHeader HeaderOf(JobKey key, string? description = null) => new(key, description, typeof(CountingJob).FullName!, true, false, false, false);

    [Test]
    public async Task Overview_ARecurringJob_IsScheduled_WithItsIntervalAndDescription()
    {
        _scheduler.QueryJobs(Arg.Any<JobQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobHeader>([HeaderOf(Key, "Counts its runs")], false));
        var trigger = Job(TimeSpan.FromHours(1)).DefaultTrigger(DateTimeOffset.UtcNow);
        _scheduler.GetTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(trigger);
        _scheduler.GetTriggerState(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(TriggerState.Normal);

        var overview = await Service().Overview();

        var dto = overview.Jobs.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((dto.Group, dto.Name, dto.Description, dto.Recurring, dto.Enabled, dto.State), Is.EqualTo((RecurringJobs.Group, "counting", (string?)"Counts its runs", true, true, "Scheduled")));
            Assert.That((dto.IntervalSeconds, dto.DefaultIntervalSeconds), Is.EqualTo(((long?)3600, (long?)3600)));
            Assert.That(overview.Nodes, Is.Empty);
        }
    }

    [Test]
    public async Task Overview_ATriggerThatFailedToFire_IsError()
    {
        _scheduler.QueryJobs(Arg.Any<JobQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobHeader>([HeaderOf(Key)], false));
        _scheduler.GetTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(Job(TimeSpan.FromHours(1)).DefaultTrigger(DateTimeOffset.UtcNow));
        _scheduler.GetTriggerState(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(TriggerState.Error);

        var dto = (await Service().Overview()).Jobs.Single();

        Assert.That(dto.State, Is.EqualTo("Error"));
    }

    [Test]
    public async Task Overview_ATurnedOffJob_HasNoTrigger_AndIsOff()
    {
        _scheduler.QueryJobs(Arg.Any<JobQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobHeader>([HeaderOf(Key)], false));
        _scheduler.GetTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns((ITrigger?)null);

        var dto = (await Service(new RecurringJobs().Add<CountingJob>(Job(TimeSpan.FromHours(1), enabled: false))).Overview()).Jobs.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((dto.State, dto.Enabled, dto.NextRunAt), Is.EqualTo(("Off", false, (DateTime?)null)));
            Assert.That(dto.IntervalSeconds, Is.Null);
        }
    }

    [Test]
    public async Task Overview_APausedJob_ReportsWhoAndWhyFromTheStore()
    {
        _scheduler.QueryJobs(Arg.Any<JobQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobHeader>([HeaderOf(Key)], false));
        _scheduler.GetTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(Job(TimeSpan.FromHours(1)).DefaultTrigger(DateTimeOffset.UtcNow));
        _scheduler.GetTriggerState(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(TriggerState.Paused);
        var pausedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _scheduler.GetTriggerPause(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(new PauseInfo("Paused on the Jobs page", "sysadmin", pausedAt));

        var dto = (await Service().Overview()).Jobs.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((dto.State, dto.PausedReason, dto.PausedBy, dto.PausedAt), Is.EqualTo(("Paused", (string?)"Paused on the Jobs page", (string?)"sysadmin", (DateTime?)pausedAt.UtcDateTime)));
            Assert.That(dto.NextRunAt, Is.Null, "Not due while paused");
        }
    }

    [Test]
    public async Task Overview_ARunningJob_ListsItsFiringsNode_AndTheClusterNodes()
    {
        _scheduler.QueryJobs(Arg.Any<JobQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobHeader>([HeaderOf(Key)], false));
        _scheduler.GetTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(Job(TimeSpan.FromHours(1)).DefaultTrigger(DateTimeOffset.UtcNow));
        _scheduler.GetTriggerState(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(TriggerState.Blocked);
        var firedAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var firing = new FireInstance("fire-1", new TriggerKey(Key.Name, Key.Group), Key, "worker-1", FireInstanceState.Executing, firedAt, null, null);
        _scheduler.QueryFireInstances(Arg.Any<FireInstanceQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<FireInstance>([firing], false));
        var checkedInAt = new DateTimeOffset(2026, 2, 1, 0, 1, 0, TimeSpan.Zero);
        _scheduler.QueryClusterNodes(Arg.Any<CancellationToken>()).Returns(new List<ClusterNode> { new("worker-1", checkedInAt, TimeSpan.FromSeconds(10), ClusterNodeState.Alive, true) });

        var overview = await Service().Overview();

        var dto = overview.Jobs.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(dto.State, Is.EqualTo("Blocked"));
            var running = dto.Running.Single();
            Assert.That((running.FireInstanceID, running.Node, running.StartedAt), Is.EqualTo(("fire-1", "worker-1", firedAt.UtcDateTime)));
            var node = overview.Nodes.Single();
            Assert.That((node.InstanceID, node.LastCheckInAt, node.State), Is.EqualTo(("worker-1", (DateTime?)checkedInAt.UtcDateTime, "Alive")));
        }
    }

    [Test]
    public async Task Overview_AJobWithAStatus_ReportsItsLastRun()
    {
        _scheduler.QueryJobs(Arg.Any<JobQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobHeader>([HeaderOf(Key)], false));
        _scheduler.GetTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(Job(TimeSpan.FromHours(1)).DefaultTrigger(DateTimeOffset.UtcNow));
        _scheduler.GetTriggerState(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(TriggerState.Normal);
        var firedAt = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var status = new JobRunStatus(SchedulerName, Key, firedAt, JobRunResult.Failed) { LastDuration = TimeSpan.FromSeconds(2), ConsecutiveFailures = 3, RunCount = 10, FailureCount = 3, LastFailureMessage = "boom" };
        _history.QueryJobRunStatuses(Arg.Any<JobRunStatusQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobRunStatus>([status], false));

        var dto = (await Service().Overview()).Jobs.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dto.Status, Is.Not.Null);
            Assert.That((dto.Status!.LastResult, dto.Status.LastRunAt, dto.Status.ConsecutiveFailures, dto.Status.RunCount, dto.Status.FailureCount, dto.Status.LastFailureMessage),
                Is.EqualTo(("Failed", firedAt.UtcDateTime, 3, 10L, 3L, (string?)"boom")));
        }
    }

    [Test]
    public async Task History_OrdersNewestFirst()
    {
        var older = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var newer = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var entries = new[]
        {
            new ExecutionHistoryEntry(SchedulerName, "worker-1", Key.Group, Key.Name, Key.Group, Key.Name, older, TimeSpan.FromSeconds(1), true, null),
            new ExecutionHistoryEntry(SchedulerName, "worker-1", Key.Group, Key.Name, Key.Group, Key.Name, newer, TimeSpan.FromSeconds(2), false, "boom") { Manual = true },
        };
        _history.QueryExecutions(Arg.Any<ExecutionHistoryQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<ExecutionHistoryEntry>(entries, false));

        var history = await Service().History(Key, 10);

        Assert.That(history.Select(r => (r.FiredAt, r.Result, r.Error, r.Manual)), Is.EqualTo(new[]
        {
            (newer.UtcDateTime, "Failed", (string?)"boom", true),
            (older.UtcDateTime, "Succeeded", (string?)null, false),
        }));
    }

    [Test]
    public async Task RunNow_JobDoesntExistInTheStore_ReturnsNotFound()
    {
        _scheduler.Exists(Key, Arg.Any<CancellationToken>()).Returns(false);

        Assert.That(await Service().RunNow(Key), Is.EqualTo(JobChange.NotFound));
    }

    [Test]
    public async Task RunNow_ADisabledJob_ReturnsConflict()
    {
        _scheduler.Exists(Key, Arg.Any<CancellationToken>()).Returns(true);

        Assert.That(await Service(new RecurringJobs().Add<CountingJob>(Job(TimeSpan.FromHours(1), enabled: false))).RunNow(Key), Is.EqualTo(JobChange.Conflict));
    }

    [Test]
    public async Task RunNow_Done_TriggersTheJob_WithTheRequestingUserInItsData()
    {
        _scheduler.Exists(Key, Arg.Any<CancellationToken>()).Returns(true);

        Assert.That(await Service().RunNow(Key, byUserID: 42), Is.EqualTo(JobChange.Done));

        await _scheduler.Received(1).TriggerJob(Key, Arg.Is<JobDataMap>(d => Equals(d[JobUser.DataKey], "42")), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetPaused_JobDoesntExist_ReturnsNotFound()
    {
        _scheduler.Exists(Key, Arg.Any<CancellationToken>()).Returns(false);

        Assert.That(await Service().SetPaused(Key, true, "sysadmin"), Is.EqualTo(JobChange.NotFound));
    }

    [Test]
    public async Task SetPaused_True_PausesWithTheReasonAndWhoAskedFor()
    {
        _scheduler.Exists(Key, Arg.Any<CancellationToken>()).Returns(true);

        Assert.That(await Service().SetPaused(Key, true, "sysadmin"), Is.EqualTo(JobChange.Done));

        await _scheduler.Received(1).PauseJobWith(Key, Arg.Is<PauseDetails>(p => p.Reason == JobsService.PausedReason && p.RequestedBy == "sysadmin"), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetPaused_False_Resumes()
    {
        _scheduler.Exists(Key, Arg.Any<CancellationToken>()).Returns(true);

        Assert.That(await Service().SetPaused(Key, false, "sysadmin"), Is.EqualTo(JobChange.Done));

        await _scheduler.Received(1).ResumeJob(Key, Arg.Any<CancellationToken>());
    }

    [Test]
    public void SetInterval_OutOfRange_ThrowsWithoutAskingTheScheduler()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => Service().SetInterval(Key, TimeSpan.FromSeconds(1)), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(_scheduler.ReceivedCalls(), Is.Empty);
        }
    }

    [Test]
    public async Task SetInterval_UnknownJob_ReturnsNotFound()
        => Assert.That(await Service().SetInterval(new JobKey("nope", RecurringJobs.Group), TimeSpan.FromMinutes(1)), Is.EqualTo(JobChange.NotFound));

    [Test]
    public async Task SetInterval_ADisabledJob_ReturnsConflict()
        => Assert.That(await Service(new RecurringJobs().Add<CountingJob>(Job(TimeSpan.FromHours(1), enabled: false))).SetInterval(Key, TimeSpan.FromMinutes(5)), Is.EqualTo(JobChange.Conflict));

    [Test]
    public async Task SetInterval_NoStoredTrigger_ReturnsConflict()
    {
        _scheduler.GetTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns((ITrigger?)null);

        Assert.That(await Service().SetInterval(Key, TimeSpan.FromMinutes(5)), Is.EqualTo(JobChange.Conflict));
    }

    [Test]
    public async Task SetInterval_Done_ReschedulesWithTheNewInterval()
    {
        var current = Job(TimeSpan.FromHours(1)).DefaultTrigger(DateTimeOffset.UtcNow);
        _scheduler.GetTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(current);
        _scheduler.GetTriggerState(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(TriggerState.Normal);
        ITrigger? captured = null;
        _scheduler.RescheduleJob(Arg.Any<TriggerKey>(), Arg.Do<ITrigger>(t => captured = t), Arg.Any<CancellationToken>()).Returns((DateTimeOffset?)null);

        var change = await Service().SetInterval(Key, TimeSpan.FromMinutes(10));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(change, Is.EqualTo(JobChange.Done));
            Assert.That(((ISimpleTrigger)captured!).RepeatInterval, Is.EqualTo(TimeSpan.FromMinutes(10)));
        }
        await _scheduler.DidNotReceive().PauseTriggerWith(Arg.Any<TriggerKey>(), Arg.Any<PauseDetails>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetInterval_APausedJob_KeepsThePause()
    {
        var current = Job(TimeSpan.FromHours(1)).DefaultTrigger(DateTimeOffset.UtcNow);
        _scheduler.GetTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(current);
        _scheduler.GetTriggerState(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(TriggerState.Paused);
        _scheduler.GetTriggerPause(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(new PauseInfo(JobsService.PausedReason, "sysadmin", DateTimeOffset.UtcNow));
        _scheduler.RescheduleJob(Arg.Any<TriggerKey>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>()).Returns((DateTimeOffset?)null);

        Assert.That(await Service().SetInterval(Key, TimeSpan.FromMinutes(10)), Is.EqualTo(JobChange.Done));

        await _scheduler.Received(1).PauseTriggerWith(new TriggerKey(Key.Name, Key.Group), Arg.Is<PauseDetails>(p => p!.Reason == JobsService.PausedReason && p.RequestedBy == "sysadmin"), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ResetSchedule_Done_ReschedulesToTheDefaultInterval()
    {
        var current = Job(TimeSpan.FromHours(1)).DefaultTrigger(DateTimeOffset.UtcNow);
        _scheduler.GetTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(current);
        _scheduler.GetTriggerState(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(TriggerState.Normal);
        _scheduler.RescheduleJob(Arg.Any<TriggerKey>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>()).Returns((DateTimeOffset?)null);

        Assert.That(await Service().ResetSchedule(Key), Is.EqualTo(JobChange.Done));

        await _scheduler.Received(1).RescheduleJob(new TriggerKey(Key.Name, Key.Group), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Interrupt_DelegatesToTheScheduler()
    {
        _scheduler.InterruptFireInstance("fire-1", Arg.Any<CancellationToken>()).Returns(true);

        Assert.That(await Service().Interrupt("fire-1"), Is.True);
    }
}
