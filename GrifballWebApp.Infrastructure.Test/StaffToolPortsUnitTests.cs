using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.Scheduler;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using Quartz.Extensibility;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// The staff tools' ports: <see cref="JobControl"/> on JobsService and <see cref="ScheduleRecommendationRuns"/> on OnDemandJobs,
/// against a substituted scheduler, as JobsServiceUnitTests and OnDemandJobsUnitTests do.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class StaffToolPortsUnitTests
{
    private static readonly JobKey Cleanup = new("cleanup", "Maintenance");
    private static readonly JobKey Recommend = OnDemandJobs.JobKeyOf(typeof(ScheduleRecommendationJob));
    private static readonly TriggerKey SeasonRun = OnDemandJobs.TriggerKeyOf<ScheduleRecommendationJob>(ScheduleRecommendationJob.RunName(41));
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ISchedulerFactory _schedulers = Substitute.For<ISchedulerFactory>();
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
    private readonly IExecutionHistoryStore _history = Substitute.For<IExecutionHistoryStore>();

    [SetUp]
    public void SetUp()
    {
        _schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        _scheduler.SchedulerName.Returns("ports-test");
        _scheduler.TimeProvider.Returns(new FakeTimeProvider(Now));
        _scheduler.QueryJobs(Arg.Any<JobQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobHeader>([new JobHeader(Cleanup, "Cleans up", "Cleanup", true, false, false, false)], false));
        _scheduler.QueryFireInstances(Arg.Any<FireInstanceQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<FireInstance>([], false));
        _scheduler.GetTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns((ITrigger?)null);
        _scheduler.QueryClusterNodes(Arg.Any<CancellationToken>()).Returns(new List<ClusterNode>());
        _scheduler.Exists(Cleanup, Arg.Any<CancellationToken>()).Returns(true);
        _scheduler.Exists(Recommend, Arg.Any<CancellationToken>()).Returns(true);
        _history.QueryJobRunStatuses(Arg.Any<JobRunStatusQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobRunStatus>([], false));
        _history.QueryExecutions(Arg.Any<ExecutionHistoryQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<ExecutionHistoryEntry>([], false));
    }

    private JobControl Control()
    {
        var recurring = new RecurringJobs().AddOnDemand<ScheduleRecommendationJob>("schedule-recommendation", "Works out a schedule");
        return new JobControl(new JobsService(_schedulers, _history, recurring, new FakeTimeProvider(Now)));
    }

    [Test]
    public async Task JobControl_IsTheJobsPage_ItsListAndItsAnswers()
    {
        var control = Control();

        var listed = await control.List();
        var ran = await control.RunNow("Maintenance", "cleanup", 7);
        var paused = await control.SetPaused("Maintenance", "cleanup", true, "Staffer");
        var missing = await control.RunNow("Nope", "nothing", null);
        var onDemand = await control.SetPaused(Recommend.Group, Recommend.Name, true, "Staffer");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(listed.Select(j => (j.Group, j.Name)), Is.EqualTo(new[] { ("Maintenance", "cleanup") }));
            Assert.That(ran, Is.EqualTo(JobControlResult.Done));
            Assert.That(paused, Is.EqualTo(JobControlResult.Done));
            Assert.That(missing, Is.EqualTo(JobControlResult.NotFound));
            Assert.That(onDemand, Is.EqualTo(JobControlResult.Conflict), "an on-demand job has no schedule to pause");
        }
        await _scheduler.Received(1).TriggerJob(Cleanup, Arg.Any<JobDataMap>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ScheduleRecommendationRuns_QueueASeasonsRun_AndShowIt()
    {
        // Idle until the season's trigger is stored, queued once it is
        _scheduler.GetTriggerState(SeasonRun, Arg.Any<CancellationToken>()).Returns(TriggerState.None, TriggerState.None, TriggerState.Normal);
        var runs = new ScheduleRecommendationRuns(new OnDemandJobs(_schedulers, _history));

        var before = await runs.Status(41);
        var queued = await runs.Queue(41, "commish", 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(before.State, Is.EqualTo(OnDemandJobState.Idle));
            Assert.That(queued.State, Is.EqualTo(OnDemandJobState.Queued));
        }
        await _scheduler.Received(1).ScheduleTrigger(
            Arg.Is<ITrigger>(t => t.Key.Equals(SeasonRun) && t.Description == "Season 41"
                && new RecommendSchedule(41, "commish", 1).Equals(t.JobDataMap[SchedulerConstants.JobInput])),
            TriggerConflict.Keep, Arg.Any<CancellationToken>());
    }
}
