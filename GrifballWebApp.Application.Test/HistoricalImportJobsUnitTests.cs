using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Test;

/// <summary>HistoricalImportJobs: queues imports, lists them, applies a finished preview, decides a stopped season's
/// review, and makes the scheduler's settings row once.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
internal sealed class HistoricalImportJobsUnitTests : ApplicationDbContextTests
{
    private static readonly string[] ExpectedSeasonKeys = ["agla/winter-2019", "agla/spring-2019"];

    private HistoricalImportSignal _signal = null!;
    private HistoricalImportJobs _jobs = null!;

    [SetUp]
    public void SetUp()
    {
        _signal = new HistoricalImportSignal();
        _jobs = new HistoricalImportJobs(Context, _signal);
    }

    [Test]
    public async Task Enqueue_SavesTheJob_AndWakesTheWorker()
    {
        var woke = _signal.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        var jobId = await _jobs.Enqueue(HistoricalImportJob.Refresh());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(jobId, Is.GreaterThan(0));
            Assert.That(await woke, Is.True);
            Assert.That(await Context.HistoricalImportJobs.AnyAsync(j => j.HistoricalImportJobID == jobId), Is.True);
        }
    }

    [Test]
    public async Task Enqueue_OfAnArchiveInput_StoresItAsThePayload()
    {
        var input = new HistoricalArchiveInput { Seasons = new UnifiedArchive() };

        var jobId = await _jobs.Enqueue(input);

        var job = await Context.HistoricalImportJobs.SingleAsync(j => j.HistoricalImportJobID == jobId);
        Assert.That(job.Payload, Does.Contain("Seasons"));
    }

    [Test]
    public async Task AnyActive_QueuedOrRunning_IsTrue()
    {
        Assert.That(await _jobs.AnyActive(), Is.False);

        await _jobs.Enqueue(HistoricalImportJob.Refresh());

        Assert.That(await _jobs.AnyActive(), Is.True);
    }

    [Test]
    public async Task List_NewestFirst_WithNeedsReviewCount()
    {
        var older = HistoricalImportJob.Refresh();
        Context.HistoricalImportJobs.Add(older);
        await Context.SaveChangesAsync();
        var newer = HistoricalImportJob.ScheduledImport("1,2", "src1");
        Context.HistoricalImportJobs.Add(newer);
        await Context.SaveChangesAsync();
        newer.AddReview("agla/winter-2019", "Winter 2019", stopped: true, new HistoricalSeasonRemovals(10, 6, 5, 3, 2, 1));
        await Context.SaveChangesAsync();

        var list = await _jobs.List(10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(list.Select(j => j.JobID), Is.EqualTo(new[] { newer.HistoricalImportJobID, older.HistoricalImportJobID }));
            Assert.That(list[0].NeedsReview, Is.EqualTo(1));
            Assert.That(list[0].Kind, Is.EqualTo(nameof(HistoricalImportJobKind.Import)));
            Assert.That(list[1].Kind, Is.EqualTo(nameof(HistoricalImportJobKind.Refresh)));
        }
    }

    [Test]
    public async Task List_Take_LimitsTheRows()
    {
        Context.HistoricalImportJobs.AddRange(HistoricalImportJob.Refresh(), HistoricalImportJob.Refresh(), HistoricalImportJob.Refresh());
        await Context.SaveChangesAsync();

        var list = await _jobs.List(2);

        Assert.That(list, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Get_NoSuchJob_ReturnsNull()
    {
        Assert.That(await _jobs.Get(999), Is.Null);
    }

    [Test]
    public async Task Get_IncludesItsReviews_OrderedByID()
    {
        var job = HistoricalImportJob.ScheduledImport("1,2", "src1");
        Context.HistoricalImportJobs.Add(job);
        await Context.SaveChangesAsync();
        job.AddReview("agla/winter-2019", "Winter 2019", stopped: true, new HistoricalSeasonRemovals(10, 6, 5, 3, 2, 1));
        job.AddReview("agla/spring-2019", "Spring 2019", stopped: true, new HistoricalSeasonRemovals(4, 1, 2, 0, 0, 0));
        await Context.SaveChangesAsync();

        var dto = await _jobs.Get(job.HistoricalImportJobID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dto, Is.Not.Null);
            Assert.That(dto!.JobID, Is.EqualTo(job.HistoricalImportJobID));
            Assert.That(dto.Guarded, Is.True);
            Assert.That(dto.Reviews.Select(r => r.SeasonKey), Is.EqualTo(ExpectedSeasonKeys));
            Assert.That(dto.Reviews[0].StatsRemoved, Is.EqualTo(6));
            Assert.That(dto.Reviews[0].Status, Is.EqualTo(nameof(HistoricalSeasonReviewStatus.NeedsReview)));
        }
    }

    [Test]
    public async Task ApplyPreview_NoSuchJob_ReturnsNotFound()
    {
        var (decision, jobId) = await _jobs.ApplyPreview(999);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision, Is.EqualTo(HistoricalJobDecision.NotFound));
            Assert.That(jobId, Is.Null);
        }
    }

    [Test]
    public async Task ApplyPreview_StillQueued_ReturnsConflict()
    {
        var preview = HistoricalImportJob.Preview("5,6");
        Context.HistoricalImportJobs.Add(preview);
        await Context.SaveChangesAsync();

        var (decision, jobId) = await _jobs.ApplyPreview(preview.HistoricalImportJobID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision, Is.EqualTo(HistoricalJobDecision.Conflict));
            Assert.That(jobId, Is.Null);
        }
    }

    private async Task<HistoricalImportJob> FinishedPreview(string snapshotIDs)
    {
        var preview = HistoricalImportJob.Preview(snapshotIDs);
        Context.HistoricalImportJobs.Add(preview);
        await Context.SaveChangesAsync();
        var claim = await HistoricalImportJob.ClaimAsync(Context.HistoricalImportJobs, preview.HistoricalImportJobID, DateTime.UtcNow);
        await HistoricalImportJob.FinishAsync(Context.HistoricalImportJobs, preview.HistoricalImportJobID, claim!.Value, succeeded: true, report: "{}", error: null, DateTime.UtcNow);
        Context.ChangeTracker.Clear();
        return preview;
    }

    [Test]
    public async Task ApplyPreview_FinishedPreview_QueuesAnImportOfItsSnapshots()
    {
        var preview = await FinishedPreview("5,6");

        var (decision, jobId) = await _jobs.ApplyPreview(preview.HistoricalImportJobID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision, Is.EqualTo(HistoricalJobDecision.Done));
            Assert.That(jobId, Is.Not.Null);
            var queued = await Context.HistoricalImportJobs.SingleAsync(j => j.HistoricalImportJobID == jobId);
            Assert.That(queued.Kind, Is.EqualTo(HistoricalImportJobKind.Import));
            Assert.That(queued.SnapshotIDs, Is.EqualTo("5,6"));
        }
    }

    [Test]
    public async Task Decide_NoSuchReview_ReturnsNotFound()
    {
        var (decision, jobId) = await _jobs.Decide(999, approve: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision, Is.EqualTo(HistoricalJobDecision.NotFound));
            Assert.That(jobId, Is.Null);
        }
    }

    private async Task<(HistoricalImportJob Job, HistoricalSeasonReview Review)> GuardedJobWithReview()
    {
        var job = HistoricalImportJob.ScheduledImport("1,2", "src1");
        Context.HistoricalImportJobs.Add(job);
        await Context.SaveChangesAsync();
        var review = job.AddReview("agla/winter-2019", "Winter 2019", stopped: true, new HistoricalSeasonRemovals(10, 6, 5, 3, 2, 1));
        await Context.SaveChangesAsync();
        return (job, review);
    }

    [Test]
    public async Task Decide_Approve_QueuesAnImportOfThatSeason_AndRecordsTheApprovalJob()
    {
        var (job, review) = await GuardedJobWithReview();

        var (decision, jobId) = await _jobs.Decide(review.HistoricalSeasonReviewID, approve: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision, Is.EqualTo(HistoricalJobDecision.Done));
            Assert.That(jobId, Is.Not.Null);
            var queued = await Context.HistoricalImportJobs.SingleAsync(j => j.HistoricalImportJobID == jobId);
            Assert.That((queued.Kind, queued.SnapshotIDs, queued.SeasonKeys), Is.EqualTo((HistoricalImportJobKind.Import, "1,2", "agla/winter-2019")));
            var reread = await Context.HistoricalImportJobs.Include(j => j.Reviews).SingleAsync(j => j.HistoricalImportJobID == job.HistoricalImportJobID);
            var rereadReview = reread.Reviews.Single();
            Assert.That(rereadReview.ApprovalJobID, Is.EqualTo(jobId));
            Assert.That(rereadReview.Status, Is.EqualTo(HistoricalSeasonReviewStatus.Approved));
        }
    }

    [Test]
    public async Task Decide_Skip_LeavesItSkipped_AndQueuesNothing()
    {
        var (_, review) = await GuardedJobWithReview();

        var (decision, jobId) = await _jobs.Decide(review.HistoricalSeasonReviewID, approve: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision, Is.EqualTo(HistoricalJobDecision.Done));
            Assert.That(jobId, Is.Null);
            Assert.That(await Context.HistoricalImportJobs.CountAsync(), Is.EqualTo(1), "No approval job queued");
            var rereadReview = await Context.Set<HistoricalSeasonReview>().SingleAsync(r => r.HistoricalSeasonReviewID == review.HistoricalSeasonReviewID);
            Assert.That(rereadReview.Status, Is.EqualTo(HistoricalSeasonReviewStatus.Skipped));
        }
    }

    [Test]
    public async Task Decide_AlreadyDecided_ReturnsConflict()
    {
        var (_, review) = await GuardedJobWithReview();
        await _jobs.Decide(review.HistoricalSeasonReviewID, approve: false);
        Context.ChangeTracker.Clear();

        var (decision, jobId) = await _jobs.Decide(review.HistoricalSeasonReviewID, approve: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision, Is.EqualTo(HistoricalJobDecision.Conflict));
            Assert.That(jobId, Is.Null);
        }
    }

    [Test]
    public async Task Settings_FirstCall_CreatesTheSingletonRow()
    {
        var settings = await _jobs.Settings();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings, Is.Not.Null);
            Assert.That(await Context.HistoricalImportSettings.CountAsync(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Settings_SecondCall_ReturnsTheSameRow()
    {
        var first = await _jobs.Settings();
        Context.ChangeTracker.Clear();

        var second = await _jobs.Settings();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.HistoricalImportSettingsID, Is.EqualTo(first.HistoricalImportSettingsID));
            Assert.That(await Context.HistoricalImportSettings.CountAsync(), Is.EqualTo(1));
        }
    }
}
