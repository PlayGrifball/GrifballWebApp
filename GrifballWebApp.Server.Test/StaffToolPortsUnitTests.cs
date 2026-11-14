using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.Scheduler;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using Quartz.Extensibility;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The staff tools' ports on Infrastructure: <see cref="JobControl"/> on JobsService (a substituted scheduler, as JobsService's own
/// unit tests do) and <see cref="ScheduleRecommendationRuns"/> on an in-memory Quartz.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class StaffToolPortsUnitTests
{
    private static readonly JobKey Cleanup = new("cleanup", "Maintenance");
    private static readonly JobKey Pull = OnDemandJobs.JobKeyOf(typeof(ScheduleRecommendationJob));

    private readonly ISchedulerFactory _schedulers = Substitute.For<ISchedulerFactory>();
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
    private InMemoryQuartz? _quartz;

    [TearDown]
    public async Task TearDownQuartz()
    {
        if (_quartz is not null)
            await _quartz.DisposeAsync();
    }

    private JobControl Control()
    {
        _schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        _scheduler.SchedulerName.Returns("ports-test");
        _scheduler.QueryJobs(Arg.Any<JobQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobHeader>([new JobHeader(Cleanup, "Cleans up", "Cleanup", true, false, false, false)], false));
        _scheduler.QueryFireInstances(Arg.Any<FireInstanceQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<FireInstance>([], false));
        _scheduler.GetTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns((ITrigger?)null);
        _scheduler.QueryClusterNodes(Arg.Any<CancellationToken>()).Returns(new List<ClusterNode>());
        _scheduler.Exists(Cleanup, Arg.Any<CancellationToken>()).Returns(true);
        _scheduler.Exists(Pull, Arg.Any<CancellationToken>()).Returns(true);
        var history = Substitute.For<IExecutionHistoryStore>();
        history.QueryJobRunStatuses(Arg.Any<JobRunStatusQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobRunStatus>([], false));
        var recurring = new RecurringJobs().AddOnDemand<ScheduleRecommendationJob>("schedule-recommendation", "Works out a schedule");
        return new JobControl(new JobsService(_schedulers, history, recurring, new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero))));
    }

    [Test]
    public async Task JobControl_IsTheJobsPage_ItsListAndItsAnswers()
    {
        var control = Control();

        var listed = await control.List();
        var ran = await control.RunNow("Maintenance", "cleanup", 7);
        var paused = await control.SetPaused("Maintenance", "cleanup", true, "Staffer");
        var missing = await control.RunNow("Nope", "nothing", null);
        var onDemand = await control.SetPaused(Pull.Group, Pull.Name, true, "Staffer");

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
        _quartz = await InMemoryQuartz.Create();
        var runs = new ScheduleRecommendationRuns(_quartz.OnDemand);

        var before = await runs.Status(41);
        var queued = await runs.Queue(41, "commish", 1);
        var after = await runs.Status(41);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(before.State, Is.EqualTo(OnDemandJobState.Idle));
            Assert.That(queued.State, Is.EqualTo(OnDemandJobState.Queued));
            Assert.That(after.State, Is.EqualTo(OnDemandJobState.Queued));
        }
    }
}
