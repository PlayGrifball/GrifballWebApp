using GrifballWebApp.Application.Historical;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Infrastructure.Historical;
using GrifballWebApp.Infrastructure.Historical.Sources;
using GrifballWebApp.Server.Historical;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using System.Text;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// HistoricalImportController's upload, sources and schedule, past what HistoricalImportControllerDatabaseUnitTests
/// covers: on a temp-file SQLite GrifballContext with the real HistoricalImportJobs and HistoricalSourceService (the
/// built-in catalog, never downloaded: its fetcher stays null), and a substituted scheduler for the schedule.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportControllerActionsUnitTests : SqliteGrifballContextFixture
{
    private readonly ISchedulerFactory _schedulers = Substitute.For<ISchedulerFactory>();
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();

    private HistoricalImportJobs Jobs() => new(Db, new HistoricalImportSignal());

    private HistoricalImportController Controller() => new HistoricalImportController(Jobs(),
            new HistoricalSourceService(Db, null!, new FakeTimeProvider(), new RecordingLogger<HistoricalSourceService>()))
        .WithUser("1", "admin", "Sysadmin");

    private HistoricalImportSchedule Schedule()
    {
        _schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
        _scheduler.GetTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns((ITrigger?)null);
        return new HistoricalImportSchedule(_schedulers, Jobs());
    }

    private static FormFile Upload(string name, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, name, $"{name}.json");
    }

    [Test]
    public async Task Import_AnArchive_QueuesItsImport()
    {
        var result = await Controller().Import(Upload("seasons", """{"seasons":[]}"""), null, null, null, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<AcceptedAtActionResult>());
        var accepted = (AcceptedAtActionResult)result;
        var jobID = ((HistoricalImportQueued)accepted.Value!).JobID;
        Db.ChangeTracker.Clear();
        var job = await Db.HistoricalImportJobs.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(accepted.ActionName, Is.EqualTo(nameof(HistoricalImportController.Job)));
            Assert.That((job.HistoricalImportJobID, job.Kind), Is.EqualTo((jobID, HistoricalImportJobKind.Import)));
        }
    }

    [Test]
    public async Task Import_AFileThatIsNotJson_IsABadRequest_QueuingNothing()
    {
        var result = await Controller().Import(Upload("seasons", "not json"), null, null, null, CancellationToken.None);

        var problem = result.AssertProblem(StatusCodes.Status400BadRequest);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(problem.Detail, Does.StartWith("Could not read the archive: "));
            Assert.That(await Db.HistoricalImportJobs.AnyAsync(), Is.False);
        }
    }

    [Test]
    public async Task Sources_AreTheCatalogs_AndOneCanBeTurnedOffAndOn()
    {
        var controller = Controller();

        var sources = (await controller.Sources(CancellationToken.None)).Value!;
        var source = sources[0];
        var off = await controller.SetSourceEnabled(source.SourceID, new SetEnabledRequest(!source.Enabled), CancellationToken.None);

        Db.ChangeTracker.Clear();
        var saved = await Db.HistoricalSources.SingleAsync(s => s.HistoricalSourceID == source.SourceID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sources, Is.Not.Empty);
            Assert.That(off, Is.InstanceOf<NoContentResult>());
            Assert.That(saved.Enabled, Is.EqualTo(!source.Enabled));
        }
    }

    [Test]
    public async Task SetSourceEnabled_AnUnknownSource_IsNotFound()
    {
        var result = await Controller().SetSourceEnabled("no-such-source", new SetEnabledRequest(true), CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task Schedule_BeforeAWorkerMadeItsTrigger_IsDailyAndNotPaused()
    {
        var schedule = Schedule();

        var result = (await Controller().Schedule(schedule, CancellationToken.None)).Value!;

        Assert.That((result.Paused, result.Interval, result.NextRunAt), Is.EqualTo((false, TimeSpan.FromDays(1), (DateTime?)null)));
    }

    [Test]
    public async Task SetSchedulePaused_ResumingATriggerNotMadeYet_IsAConflict()
    {
        var schedule = Schedule();
        _scheduler.ResumeTrigger(HistoricalRefreshJob.TriggerKey, Arg.Any<CancellationToken>()).Returns(false);

        var result = await Controller().SetSchedulePaused(schedule, new SetPausedRequest(false), CancellationToken.None);

        Assert.That(result.Result, Is.InstanceOf<ConflictResult>());
    }

    [Test]
    public async Task SetSchedulePaused_Resuming_ResumesItAndItsRetry_AndIsTheSchedule()
    {
        var schedule = Schedule();
        _scheduler.ResumeTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await Controller().SetSchedulePaused(schedule, new SetPausedRequest(false), CancellationToken.None);

        Assert.That(result.Value!.Paused, Is.False);
        await _scheduler.Received(1).ResumeTrigger(HistoricalRefreshJob.RetryTriggerKey, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetSchedulePaused_Pausing_PausesItByTheCaller_AndDropsTheRetry()
    {
        var schedule = Schedule();
        // The store as a worker left it: the refresh's trigger there, and paused once paused
        var paused = false;
        var trigger = Substitute.For<ISimpleTrigger>();
        trigger.Key.Returns(HistoricalRefreshJob.TriggerKey);
        trigger.RepeatInterval.Returns(TimeSpan.FromHours(6));
        _scheduler.GetTrigger(HistoricalRefreshJob.TriggerKey, Arg.Any<CancellationToken>()).Returns(trigger);
        _scheduler.GetTriggerState(HistoricalRefreshJob.TriggerKey, Arg.Any<CancellationToken>()).Returns(_ => paused ? TriggerState.Paused : TriggerState.Normal);
        _scheduler.PauseTriggerWith(HistoricalRefreshJob.TriggerKey, Arg.Any<PauseDetails>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            paused = true;
            return true;
        });

        var result = await Controller().SetSchedulePaused(schedule, new SetPausedRequest(true), CancellationToken.None);

        Assert.That((result.Value!.Paused, result.Value.Interval, result.Value.NextRunAt), Is.EqualTo((true, TimeSpan.FromHours(6), (DateTime?)null)),
            "The schedule read again: paused, so no next run");
        await _scheduler.Received(1).UnscheduleJob(HistoricalRefreshJob.RetryTriggerKey, Arg.Any<CancellationToken>());
        await _scheduler.Received(1).PauseTriggerWith(HistoricalRefreshJob.TriggerKey, Arg.Is<PauseDetails>(p => p.RequestedBy == "admin"), Arg.Any<CancellationToken>());
    }
}
