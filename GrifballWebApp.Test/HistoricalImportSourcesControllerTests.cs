using GrifballWebApp.Database;
using GrifballWebApp.Domain.Models;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Historical.Sources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace GrifballWebApp.Test;

/// <summary>The historical import page's actions: sources, refresh, preview and apply, reviews, jobs and the schedule.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportSourcesControllerTests
{
    private GrifballContext _context = null!;
    private HistoricalImportSignal _signal = null!;
    private HistoricalImportController _controller = null!;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _signal = new HistoricalImportSignal();
        var sources = new HistoricalSourceService(_context, new HistoricalSourceFetcher(new HttpClient(new FakeSourcesHandler(_ => FakeSourcesHandler.Status(HttpStatusCode.NotFound)))),
            TimeProvider.System, NullLogger<HistoricalSourceService>.Instance);
        _controller = new HistoricalImportController(new HistoricalImportJobs(_context, _signal), sources);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private static int QueuedJob(IActionResult result) => ((HistoricalImportQueued)((AcceptedAtActionResult)result).Value!).JobID;

    [Test]
    public async Task Sources_AreListed_AndOneCanBeDisabled()
    {
        var sources = (await _controller.Sources(CancellationToken.None)).Value!;

        Assert.Multiple(async () =>
        {
            Assert.That(sources, Has.Count.EqualTo(HistoricalSourceCatalog.Sources.Count));
            Assert.That(await _controller.SetSourceEnabled("late-league-website", new SetEnabledRequest(false), CancellationToken.None), Is.InstanceOf<NoContentResult>());
            Assert.That(await _controller.SetSourceEnabled("nope", new SetEnabledRequest(true), CancellationToken.None), Is.InstanceOf<NotFoundResult>());
            Assert.That(await _controller.SetSourceEnabled("2018-fl18-schedule", new SetEnabledRequest(true), CancellationToken.None), Is.InstanceOf<ConflictObjectResult>(), "A dead source can't be enabled");
            Assert.That((await _controller.Sources(CancellationToken.None)).Value!.Single(s => s.SourceID == "late-league-website").Enabled, Is.False);
        });
    }

    [Test]
    public async Task RefreshAndPreview_QueueTheirJobs_AndWakeTheWorker()
    {
        var refresh = QueuedJob(await _controller.Refresh(CancellationToken.None));
        var preview = QueuedJob(await _controller.Preview(CancellationToken.None));

        var jobs = (await _controller.Jobs(CancellationToken.None)).Value!;
        Assert.Multiple(async () =>
        {
            Assert.That(jobs.Select(j => (j.JobID, j.Kind, j.Trigger, j.Status)), Is.EqualTo(new[]
            {
                (preview, "Preview", "Manual", "Queued"), (refresh, "Refresh", "Manual", "Queued"),
            }), "Newest first");
            Assert.That(await _signal.WaitAsync(TimeSpan.Zero, CancellationToken.None), Is.True);
            Assert.That((await _controller.Job(refresh, CancellationToken.None)).Value!.Kind, Is.EqualTo("Refresh"));
        });
    }

    [Test]
    public async Task ApplyPreview_ImportsAFinishedPreview_Only()
    {
        var preview = await _context.Queue(HistoricalImportJob.Preview("4,5"));
        await _context.Finish(preview);
        var running = await _context.Queue(HistoricalImportJob.Preview());
        await _context.Start(running);

        var applied = QueuedJob(await _controller.ApplyPreview(preview.HistoricalImportJobID, CancellationToken.None));

        Assert.Multiple(async () =>
        {
            var job = await _context.HistoricalImportJobs.AsNoTracking().SingleAsync(j => j.HistoricalImportJobID == applied);
            Assert.That((job.Kind, job.SnapshotIDs, job.Guarded, job.SeasonKeys), Is.EqualTo((HistoricalImportJobKind.Import, "4,5", false, (string?)null)));
            Assert.That(await _controller.ApplyPreview(running.HistoricalImportJobID, CancellationToken.None), Is.InstanceOf<ConflictResult>());
            Assert.That(await _controller.ApplyPreview(999, CancellationToken.None), Is.InstanceOf<NotFoundResult>());
        });
    }

    [Test]
    public async Task Reviews_AreApprovedOrSkipped_Once()
    {
        var job = await _context.Queue(HistoricalImportJob.Seed(guarded: true, snapshotIDs: "7"));
        var approve = job.AddReview("a/Stats", "A", stopped: true, new HistoricalSeasonRemovals(5, 3, 0, 0, 0, 0));
        var skip = job.AddReview("b/Stats", null, stopped: true, default);
        await _context.SaveChangesAsync();
        await _context.Finish(job);

        var approvalJob = QueuedJob(await _controller.ApproveReview(approve.HistoricalSeasonReviewID, CancellationToken.None));
        var skipped = await _controller.SkipReview(skip.HistoricalSeasonReviewID, CancellationToken.None);

        var reviews = (await _controller.Job(job.HistoricalImportJobID, CancellationToken.None)).Value!.Reviews;
        Assert.Multiple(async () =>
        {
            Assert.That(skipped, Is.InstanceOf<NoContentResult>());
            Assert.That(reviews.Select(r => (r.SeasonKey, r.Status, r.ApprovalJobID)), Is.EqualTo(new[] { ("a/Stats", "Approved", (int?)approvalJob), ("b/Stats", "Skipped", null) }));
            Assert.That((reviews[0].StatsBefore, reviews[0].StatsRemoved, reviews[0].SeasonName), Is.EqualTo((5, 3, (string?)"A")));
            var queued = await _context.HistoricalImportJobs.AsNoTracking().SingleAsync(j => j.HistoricalImportJobID == approvalJob);
            Assert.That((queued.SnapshotIDs, queued.SeasonKeys, queued.Guarded), Is.EqualTo(("7", "a/Stats", false)));
            Assert.That(await _controller.ApproveReview(approve.HistoricalSeasonReviewID, CancellationToken.None), Is.InstanceOf<ConflictResult>());
            Assert.That(await _controller.SkipReview(999, CancellationToken.None), Is.InstanceOf<NotFoundResult>());
        });
    }

    [Test]
    public async Task TheSchedule_IsShown_AndPaused()
    {
        await using var jobs = await InMemoryJobs.Create(TimeProvider.System, services => services.AddSingleton(_context), HistoricalRefreshJob.Definition);
        var schedule = new HistoricalImportSchedule(jobs.Factory, new HistoricalImportJobs(_context, _signal));
        var beforeTheWorker = await _controller.SetSchedulePaused(schedule, new SetPausedRequest(true), CancellationToken.None);
        await jobs.Seed();

        var before = (await _controller.Schedule(schedule, CancellationToken.None)).Value!;
        // Signed in: the pause records who
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "sysadmin")], "test")),
            },
        };
        var paused = (await _controller.SetSchedulePaused(schedule, new SetPausedRequest(true), CancellationToken.None)).Value!;

        Assert.Multiple(async () =>
        {
            Assert.That(beforeTheWorker.Result, Is.InstanceOf<ConflictResult>(), "No trigger until a worker makes it");
            Assert.That((before.Paused, before.LastRunAt, before.Interval), Is.EqualTo((false, (DateTime?)null, TimeSpan.FromDays(1))));
            Assert.That(before.NextRunAt, Is.Not.Null);
            Assert.That((paused.Paused, paused.NextRunAt), Is.EqualTo((true, (DateTime?)null)));
            Assert.That(await jobs.Scheduler.GetTriggerState(HistoricalRefreshJob.TriggerKey), Is.EqualTo(Quartz.TriggerState.Paused), "On the trigger");
            Assert.That((await jobs.Scheduler.GetTriggerPause(HistoricalRefreshJob.TriggerKey))?.RequestedBy, Is.EqualTo("sysadmin"));
        });
    }
}
