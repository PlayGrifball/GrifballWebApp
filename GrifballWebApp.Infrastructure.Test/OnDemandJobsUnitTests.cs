using Quartz;
using Quartz.Extensibility;
using GrifballWebApp.Infrastructure.Jobs;
using NSubstitute;
using Microsoft.Extensions.Time.Testing;
using GrifballWebApp.Application.Jobs;
using static GrifballWebApp.Infrastructure.Test.JobsUnitTests;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// OnDemandJobs.Status and Enqueue, against a substituted ISchedulerFactory/IScheduler/IExecutionHistoryStore: no
/// database, host or Quartz-managed scheduler. Enqueue goes through Quartz's own ScheduleJob extension, which stores the
/// job and its one-off trigger on the substitute (its clock a fake). GrifballWebApp.Integration.Test keeps the same
/// scenarios against a real scheduler and store.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class OnDemandJobsUnitTests
{
    private readonly ISchedulerFactory _schedulers = Substitute.For<ISchedulerFactory>();
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
    private readonly IExecutionHistoryStore _history = Substitute.For<IExecutionHistoryStore>();
    private const string SchedulerName = "test-scheduler";
    private static readonly TriggerKey Key = OnDemandJobs.TriggerKeyOf<CountingJob>("page-1");
    private static readonly TriggerKey EnqueueKey = OnDemandJobs.TriggerKeyOf<OnDemandCountingJob>("page-1");
    private static readonly DateTimeOffset EnqueueNow = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [SetUp]
    public void SetUp()
    {
        _schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        _scheduler.SchedulerName.Returns(SchedulerName);
        // The one-off triggers Enqueue stores are built on the scheduler's clock
        _scheduler.TimeProvider.Returns(new FakeTimeProvider(EnqueueNow));
        _scheduler.QueryFireInstances(Arg.Any<FireInstanceQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<FireInstance>([], false));
        _history.QueryExecutions(Arg.Any<ExecutionHistoryQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<ExecutionHistoryEntry>([], false));
    }

    private OnDemandJobs Jobs() => new(_schedulers, _history);

    [Test]
    public async Task Status_NeverRun_IsIdle()
    {
        _scheduler.GetTriggerState(Key, Arg.Any<CancellationToken>()).Returns(TriggerState.None);

        var status = await Jobs().Status<CountingJob>("page-1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(status.State, Is.EqualTo(OnDemandJobState.Idle));
            Assert.That(status.LastRun, Is.Null);
        }
    }

    [Test]
    public async Task Status_TriggerStored_IsQueued()
    {
        _scheduler.GetTriggerState(Key, Arg.Any<CancellationToken>()).Returns(TriggerState.Normal);

        Assert.That((await Jobs().Status<CountingJob>("page-1")).State, Is.EqualTo(OnDemandJobState.Queued));
    }

    [Test]
    public async Task Status_TriggerInError_IsIdle_AReplacementCanBeQueued()
    {
        _scheduler.GetTriggerState(Key, Arg.Any<CancellationToken>()).Returns(TriggerState.Error);

        Assert.That((await Jobs().Status<CountingJob>("page-1")).State, Is.EqualTo(OnDemandJobState.Idle));
    }

    [Test]
    public async Task Status_Firing_ReportsItsProgress_WithoutLookingAtTheTriggerState()
    {
        var startedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var firing = new FireInstance("fire-1", Key, null, "worker-1", FireInstanceState.Executing, startedAt, null, null) { Progress = 50, ProgressMessage = "Half way" };
        _scheduler.QueryFireInstances(Arg.Any<FireInstanceQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<FireInstance>([firing], false));

        var status = await Jobs().Status<CountingJob>("page-1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That((status.State, status.Progress, status.ProgressMessage, status.StartedAt), Is.EqualTo((OnDemandJobState.Running, (int?)50, (string?)"Half way", (DateTime?)startedAt.UtcDateTime)));
        }
        await _scheduler.DidNotReceive().GetTriggerState(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Status_AFailedLastRun_ShowsItsSummaryAsTheError_WhenThereIsNoException()
    {
        _scheduler.GetTriggerState(Key, Arg.Any<CancellationToken>()).Returns(TriggerState.None);
        var firedAt = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var run = new ExecutionHistoryEntry(SchedulerName, "worker-1", Key.Group, Key.Name, Key.Group, Key.Name, firedAt, TimeSpan.FromSeconds(3), false, null)
        {
            Summary = "3 of 10 failed",
        };
        _history.QueryExecutions(Arg.Any<ExecutionHistoryQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<ExecutionHistoryEntry>([run], false));

        var status = await Jobs().Status<CountingJob>("page-1");

        Assert.That(status.LastRun, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((status.LastRun!.Result, status.LastRun.Error, status.LastRun.FiredAt, status.LastRun.DurationMs), Is.EqualTo(("Failed", (string?)"3 of 10 failed", firedAt.UtcDateTime, 3000d)));
        }
    }

    [Test]
    public async Task Status_ASucceededLastRun_NeverShowsItsSummaryAsAnError()
    {
        _scheduler.GetTriggerState(Key, Arg.Any<CancellationToken>()).Returns(TriggerState.None);
        var run = new ExecutionHistoryEntry(SchedulerName, "worker-1", Key.Group, Key.Name, Key.Group, Key.Name, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1), true, null)
        {
            Summary = "Counted 3",
        };
        _history.QueryExecutions(Arg.Any<ExecutionHistoryQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<ExecutionHistoryEntry>([run], false));

        var status = await Jobs().Status<CountingJob>("page-1");

        Assert.That(status.LastRun!.Error, Is.Null);
    }

    [Test]
    public async Task Enqueue_NothingQueued_StoresAOneOffTrigger_ForTheJob_WithItsInput()
    {
        // Idle before the trigger is stored, queued once it is
        _scheduler.GetTriggerState(EnqueueKey, Arg.Any<CancellationToken>()).Returns(TriggerState.None, TriggerState.Normal);

        var status = await Jobs().Enqueue<OnDemandCountingJob, CountBy>("page-1", new CountBy(3), "Count to three", "counting-group");

        Assert.That(status.State, Is.EqualTo(OnDemandJobState.Queued));
        await _scheduler.Received(1).ScheduleTrigger(
            Arg.Is<ITrigger>(t => t.Key.Equals(EnqueueKey) && t.Description == "Count to three" && t.ExecutionGroup == "counting-group"
                && t.StartTimeUtc == EnqueueNow && new CountBy(3).Equals(t.JobDataMap[SchedulerConstants.JobInput])
                && t.JobKey.Equals(OnDemandJobs.JobKeyOf(typeof(OnDemandCountingJob)))),
            TriggerConflict.Keep, Arg.Any<CancellationToken>());
        await _scheduler.Received(1).AddJob(Arg.Is<IJobDetail>(j => j.Key.Equals(OnDemandJobs.JobKeyOf(typeof(OnDemandCountingJob))) && j.Durable),
            Arg.Any<AddJobOptions>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Enqueue_OneAlreadyQueued_AddsNothing_AndSaysItsQueued()
    {
        _scheduler.GetTriggerState(EnqueueKey, Arg.Any<CancellationToken>()).Returns(TriggerState.Normal);

        var status = await Jobs().Enqueue<OnDemandCountingJob, CountBy>("page-1", new CountBy(3));

        Assert.That(status.State, Is.EqualTo(OnDemandJobState.Queued));
        await _scheduler.DidNotReceiveWithAnyArgs().ScheduleTrigger(default!, default, default);
    }
}
