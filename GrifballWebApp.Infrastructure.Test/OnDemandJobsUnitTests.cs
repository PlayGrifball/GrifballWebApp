using Quartz;
using Quartz.Extensibility;
using GrifballWebApp.Infrastructure.Jobs;
using NSubstitute;
using GrifballWebApp.Application.Jobs;
using static GrifballWebApp.Infrastructure.Test.JobsUnitTests;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// OnDemandJobs.Status, against a substituted ISchedulerFactory/IScheduler/IExecutionHistoryStore: no database, host
/// or Quartz-managed scheduler. GrifballWebApp.Integration.Test keeps the same scenarios against a real scheduler and
/// store (and Enqueue, which needs Quartz's own ScheduleJob extension on a real store), for its own layer's coverage.
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

    [SetUp]
    public void SetUp()
    {
        _schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        _scheduler.SchedulerName.Returns(SchedulerName);
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
            EffectiveResult = JobRunResult.Failed,
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
            EffectiveResult = JobRunResult.Succeeded,
            Summary = "Counted 3",
        };
        _history.QueryExecutions(Arg.Any<ExecutionHistoryQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<ExecutionHistoryEntry>([run], false));

        var status = await Jobs().Status<CountingJob>("page-1");

        Assert.That(status.LastRun!.Error, Is.Null);
    }
}
