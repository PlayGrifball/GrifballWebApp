using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.SeasonMatchPage;
using GrifballWebApp.Server.Jobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using Quartz.Extensibility;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// JobsController's actions over a real JobsService, against a substituted scheduler and run history (as JobsService's
/// own unit tests do): what each answers for a job there, not there, or one it can't change.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class JobsControllerActionsUnitTests
{
    private static readonly JobKey Cleanup = new("cleanup", "Maintenance");
    private static readonly JobKey Pull = OnDemandJobs.JobKeyOf(typeof(SeasonMatchPullJob));

    private readonly ISchedulerFactory _schedulers = Substitute.For<ISchedulerFactory>();
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
    private readonly IExecutionHistoryStore _history = Substitute.For<IExecutionHistoryStore>();
    private readonly JobsController _controller;

    public JobsControllerActionsUnitTests()
    {
        _schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        _scheduler.SchedulerName.Returns("jobs-test");
        var recurring = new RecurringJobs().AddOnDemand<SeasonMatchPullJob>("Season match pull", "Pulls a match's games");
        _controller = new JobsController(new JobsService(_schedulers, _history, recurring, new FakeTimeProvider()))
            .WithUser("7", "admin", "Sysadmin");
    }

    [Test]
    public async Task List_IsEveryJobInTheStore()
    {
        _scheduler.QueryJobs(Arg.Any<JobQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobHeader>([new JobHeader(Cleanup, "Cleans up", "Cleanup", true, false, false, false)], false));
        _scheduler.QueryFireInstances(Arg.Any<FireInstanceQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<FireInstance>([], false));
        _scheduler.QueryClusterNodes(Arg.Any<CancellationToken>()).Returns(new List<ClusterNode>());
        _scheduler.GetTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns((ITrigger?)null);
        _history.QueryJobRunStatuses(Arg.Any<JobRunStatusQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobRunStatus>([], false));

        var overview = (await _controller.List(CancellationToken.None)).Value!;

        var job = overview.Jobs.Single();
        Assert.That((job.Group, job.Name, job.Description, job.State), Is.EqualTo(("Maintenance", "cleanup", (string?)"Cleans up", "Off")));
    }

    [TestCase(0, 1)]
    [TestCase(10, 10)]
    [TestCase(5000, 200)]
    public async Task History_AsksForTheRunsAskedFor_WithinItsLimits(int take, int asked)
    {
        var firedAt = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var run = new ExecutionHistoryEntry("jobs-test", "worker-1", Cleanup.Group, Cleanup.Name, Cleanup.Group, Cleanup.Name, firedAt, TimeSpan.FromSeconds(2), true, null);
        _history.QueryExecutions(Arg.Any<ExecutionHistoryQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<ExecutionHistoryEntry>([run], false));

        var runs = (await _controller.History("Maintenance", "cleanup", take, CancellationToken.None)).Value!;

        Assert.That(runs.Select(r => (r.FiredAt, r.Result, r.Node)), Is.EqualTo(new[] { (firedAt.UtcDateTime, "Succeeded", "worker-1") }));
        await _history.Received(1).QueryExecutions(Arg.Is<ExecutionHistoryQuery>(q => q.Job!.Equals(Cleanup) && q.Take == asked), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Run_TriggersTheJob_AsTheCaller()
    {
        _scheduler.Exists(Cleanup, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.Run("Maintenance", "cleanup", CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NoContentResult>());
        await _scheduler.Received(1).TriggerJob(Cleanup, Arg.Is<JobDataMap>(x => (string)x[JobUser.DataKey] == "7"), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Run_AJobNotThere_IsNotFound()
    {
        _scheduler.Exists(Cleanup, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _controller.Run("Maintenance", "cleanup", CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task SetPaused_PausesTheJob_ByTheCaller()
    {
        _scheduler.Exists(Cleanup, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.SetPaused("Maintenance", "cleanup", new JobPausedRequest(true), CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NoContentResult>());
        await _scheduler.Received(1).PauseJobWith(Cleanup, Arg.Is<PauseDetails>(x => x.RequestedBy == "admin"), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetPaused_AnOnDemandJob_IsAConflict()
    {
        _scheduler.Exists(Pull, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _controller.SetPaused(Pull.Group, Pull.Name, new JobPausedRequest(true), CancellationToken.None);

        Assert.That(result, Is.InstanceOf<ConflictResult>());
        await _scheduler.DidNotReceiveWithAnyArgs().PauseJobWith(default!, default!, default);
    }

    [Test]
    public async Task SetInterval_AJobThatIsNotRecurring_IsNotFound()
    {
        var result = await _controller.SetInterval("Maintenance", "cleanup", new JobIntervalRequest(3600), CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task ResetSchedule_AJobThatIsNotRecurring_IsNotFound()
    {
        var result = await _controller.ResetSchedule("Maintenance", "cleanup", CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [TestCase(true, typeof(NoContentResult))]
    [TestCase(false, typeof(NotFoundResult))]
    public async Task Interrupt_AsksTheRunToStop(bool running, Type answer)
    {
        _scheduler.InterruptFireInstance("fire-1", Arg.Any<CancellationToken>()).Returns(running);

        var result = await _controller.Interrupt("fire-1", CancellationToken.None);

        Assert.That(result, Is.TypeOf(answer));
        await _scheduler.Received(1).InterruptFireInstance("fire-1", Arg.Any<CancellationToken>());
    }
}
