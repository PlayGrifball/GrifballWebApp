using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using Quartz.Extensibility;
using static GrifballWebApp.Infrastructure.Test.JobsUnitTests;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// OnDemandJobs.Enqueue against a substituted Quartz scheduler: a run is a one-off trigger named after what it's for,
/// in its job's group, carrying its input, kept if one is already stored; nothing is added while one is queued.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class OnDemandJobsEnqueueUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly TriggerKey Key = OnDemandJobs.TriggerKeyOf<OnDemandCountingJob>("page-1");

    private readonly ISchedulerFactory _schedulers = Substitute.For<ISchedulerFactory>();
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
    private readonly IExecutionHistoryStore _history = Substitute.For<IExecutionHistoryStore>();

    [SetUp]
    public void SetUp()
    {
        _schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        _scheduler.SchedulerName.Returns("test-scheduler");
        _scheduler.TimeProvider.Returns(new FakeTimeProvider(Now));
        _scheduler.QueryFireInstances(Arg.Any<FireInstanceQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<FireInstance>([], false));
        _history.QueryExecutions(Arg.Any<ExecutionHistoryQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<ExecutionHistoryEntry>([], false));
    }

    private OnDemandJobs Jobs() => new(_schedulers, _history);

    [Test]
    public async Task Enqueue_NothingQueued_StoresAOneOffTrigger_ForTheJob_WithItsInput()
    {
        // Idle before the trigger is stored, queued once it is
        _scheduler.GetTriggerState(Key, Arg.Any<CancellationToken>()).Returns(TriggerState.None, TriggerState.Normal);

        var status = await Jobs().Enqueue<OnDemandCountingJob, CountBy>("page-1", new CountBy(3), "Count to three", "counting-group");

        Assert.That(status.State, Is.EqualTo(OnDemandJobState.Queued));
        await _scheduler.Received(1).ScheduleTrigger(
            Arg.Is<ITrigger>(t => t.Key.Equals(Key) && t.Description == "Count to three" && t.ExecutionGroup == "counting-group"
                && t.StartTimeUtc == Now && new CountBy(3).Equals(t.JobDataMap[SchedulerConstants.JobInput])
                && t.JobKey.Equals(OnDemandJobs.JobKeyOf(typeof(OnDemandCountingJob)))),
            TriggerConflict.Keep, Arg.Any<CancellationToken>());
        await _scheduler.Received(1).AddJob(Arg.Is<IJobDetail>(j => j.Key.Equals(OnDemandJobs.JobKeyOf(typeof(OnDemandCountingJob))) && j.Durable),
            Arg.Any<AddJobOptions>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Enqueue_OneAlreadyQueued_AddsNothing_AndSaysItsQueued()
    {
        _scheduler.GetTriggerState(Key, Arg.Any<CancellationToken>()).Returns(TriggerState.Normal);

        var status = await Jobs().Enqueue<OnDemandCountingJob, CountBy>("page-1", new CountBy(3));

        Assert.That(status.State, Is.EqualTo(OnDemandJobState.Queued));
        await _scheduler.DidNotReceiveWithAnyArgs().ScheduleTrigger(default!, default, default);
    }
}
