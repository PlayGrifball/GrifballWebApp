using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Archive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using GrifballWebApp.Application.Queries;
using static GrifballWebApp.Domain.Test.HistoricalAggregateUnitTests;
using static GrifballWebApp.Domain.Test.HistoricalAggregateTestData;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The historical archive's aggregates (#320): a source and its snapshots, a job and its season reviews, and the
/// scheduler's row. Each begins through its factories and moves only through its own methods, which refuse what can't
/// be; the database refuses the combinations that can't be.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalAggregateTests
{
    [Test]
    public async Task TheWorkersMoves_RefuseAJobNotInTheStateTheyMoveFrom()
    {
        var context = await SetUpFixture.NewGrifballContext();
        try
        {
            var jobs = context.HistoricalImportJobs;
            var job = await context.Queue(HistoricalImportJob.Upload("{}"));
            var id = job.HistoricalImportJobID;

            var beatQueued = await HistoricalImportJob.BeatAsync(jobs, id, At, At);
            var finishQueued = await HistoricalImportJob.FinishAsync(jobs, id, At, true, "{}", null, At);
            var claimed = await HistoricalImportJob.ClaimAsync(jobs, id, At);
            var claimedAgain = await HistoricalImportJob.ClaimAsync(jobs, id, At.AddMinutes(1));
            var progress = await HistoricalImportJob.ReportProgressAsync(jobs, id, At, new string('s', 300), 1, 2, At.AddMinutes(1));
            var read = await HistoricalImportJob.RecordSnapshotsReadAsync(jobs, id, At, [3, 4]);
            var requeued = await HistoricalImportJob.RequeueStaleAsync(jobs, At.AddMinutes(2));
            var reclaimed = await HistoricalImportJob.ClaimAsync(jobs, id, At.AddMinutes(3));
            var finished = await HistoricalImportJob.FinishAsync(jobs, id, At.AddMinutes(3), false, null, "boom", At.AddMinutes(4));
            var finishedAgain = await HistoricalImportJob.FinishAsync(jobs, id, At.AddMinutes(3), true, "{}", null, At.AddMinutes(5));
            var progressEnded = await HistoricalImportJob.ReportProgressAsync(jobs, id, At.AddMinutes(3), "late", 2, 2, At.AddMinutes(5));
            var deletedEarly = await jobs.DeleteEndedAsync(At.AddMinutes(4));

            context.ChangeTracker.Clear();
            var row = await jobs.AsNoTracking().SingleAsync();
            var deleted = await jobs.DeleteEndedAsync(At.AddMinutes(5));
            using (Assert.EnterMultipleScope())
            {
                Assert.That((beatQueued, finishQueued, claimed, claimedAgain), Is.EqualTo((false, false, (DateTime?)At, (DateTime?)null)), "Only a queued job is claimed, and once");
                Assert.That((progress, read, requeued, reclaimed), Is.EqualTo((true, true, 1, (DateTime?)At.AddMinutes(3))), "A stale running job is queued again, and claimed again");
                Assert.That((finished, finishedAgain, progressEnded), Is.EqualTo((true, false, false)), "It ends once");
                Assert.That((row.Status, row.Stage, row.Error, row.Payload, row.FinishedAt, row.SnapshotIDs, row.SeasonsDone, row.SeasonsTotal),
                    Is.EqualTo((HistoricalImportJobStatus.Failed, (string?)HistoricalImportJob.FailedStage, (string?)"boom", (string?)null, (DateTime?)At.AddMinutes(4), (string?)"3,4", 1, 2)));
                Assert.That((deletedEarly, deleted), Is.EqualTo((0, 1)));
            }
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task AClaimRequeuedAsStale_AndClaimedAgain_IsRefusedItsMoves_TheNewClaimKeepsThem()
    {
        var context = await SetUpFixture.NewGrifballContext();
        try
        {
            var jobs = context.HistoricalImportJobs;
            var id = (await context.Queue(HistoricalImportJob.Upload("{}"))).HistoricalImportJobID;

            var first = (await HistoricalImportJob.ClaimAsync(jobs, id, At))!.Value;
            await HistoricalImportJob.RequeueStaleAsync(jobs, At.AddMinutes(1));
            var second = (await HistoricalImportJob.ClaimAsync(jobs, id, At.AddMinutes(2)))!.Value;

            var firstMoves = (
                await HistoricalImportJob.BeatAsync(jobs, id, first, At.AddMinutes(3)),
                await HistoricalImportJob.ReportProgressAsync(jobs, id, first, "first's", 1, 1, At.AddMinutes(3)),
                await HistoricalImportJob.RecordSnapshotsReadAsync(jobs, id, first, [1]),
                await HistoricalImportJob.FinishAsync(jobs, id, first, true, "{\"first\":1}", null, At.AddMinutes(3)));
            var secondMoves = (
                await HistoricalImportJob.BeatAsync(jobs, id, second, At.AddMinutes(4)),
                await HistoricalImportJob.ReportProgressAsync(jobs, id, second, "second's", 2, 2, At.AddMinutes(4)),
                await HistoricalImportJob.RecordSnapshotsReadAsync(jobs, id, second, [2]),
                await HistoricalImportJob.FinishAsync(jobs, id, second, false, null, "second's", At.AddMinutes(5)));

            var row = await jobs.AsNoTracking().SingleAsync();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(firstMoves, Is.EqualTo((false, false, false, false)), "The first run's job is the second's now");
                Assert.That(secondMoves, Is.EqualTo((true, true, true, true)));
                Assert.That((row.Status, row.Error, row.Report, row.SnapshotIDs, row.SeasonsDone, row.StartedAt, row.FinishedAt),
                    Is.EqualTo((HistoricalImportJobStatus.Failed, (string?)"second's", (string?)null, (string?)"2", 2, (DateTime?)At.AddMinutes(2), (DateTime?)At.AddMinutes(5))));
            }
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task AClaim_IsItsStartToTheMicrosecond_AndAlwaysLaterThanTheLast()
    {
        var context = await SetUpFixture.NewGrifballContext();
        try
        {
            var jobs = context.HistoricalImportJobs;
            var id = (await context.Queue(HistoricalImportJob.Upload("{}"))).HistoricalImportJobID;
            // Finer than Postgres keeps: the claim is cut to the microsecond, so the stored start matches it on each provider
            var now = At.AddTicks(1234567);

            var claim = (await HistoricalImportJob.ClaimAsync(jobs, id, now))!.Value;
            var beat = await HistoricalImportJob.BeatAsync(jobs, id, claim, now);
            await HistoricalImportJob.RequeueStaleAsync(jobs, now.AddMinutes(1));
            // A clock gone back, or one that hasn't moved: the job waits for a later claim rather than sharing a start
            var sameStart = await HistoricalImportJob.ClaimAsync(jobs, id, now);
            var earlier = await HistoricalImportJob.ClaimAsync(jobs, id, now.AddSeconds(-1));
            var later = await HistoricalImportJob.ClaimAsync(jobs, id, now.AddTicks(10));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(claim, Is.EqualTo(At.AddTicks(1234560)));
                Assert.That(beat, Is.True, "The stored start is the claim");
                Assert.That((sameStart, earlier), Is.EqualTo(((DateTime?)null, (DateTime?)null)));
                Assert.That(later, Is.EqualTo((DateTime?)At.AddTicks(1234570)));
            }
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task AProgressStage_IsCutToItsColumn()
    {
        var context = await SetUpFixture.NewGrifballContext();
        try
        {
            var job = await context.Queue(HistoricalImportJob.Refresh());
            var claim = await context.Start(job, At);
            await HistoricalImportJob.ReportProgressAsync(context.HistoricalImportJobs, job.HistoricalImportJobID, claim, new string('s', 300), 0, 0, At);

            var stage = await context.HistoricalImportJobs.AsNoTracking().Select(j => j.Stage).SingleAsync();
            Assert.That(stage, Has.Length.EqualTo(HistoricalImportJob.MaxStageLength));
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task ASourcesSnapshots_AreReadThroughIt_TheLatestByTimeThenID()
    {
        var context = await SetUpFixture.NewGrifballContext();
        try
        {
            var a = HistoricalSource.FromCatalog("a", Listing(), enabled: true);
            var b = HistoricalSource.FromCatalog("b", Listing(), enabled: true);
            context.HistoricalSources.AddRange(a, b);
            var a1 = a.Fetched(At, [1], "x", null)!;
            var a2 = a.Fetched(At.AddDays(1), [2], "x", null)!;
            var b1 = b.Fetched(At, [3], "x", null)!;
            var b2 = b.Fetched(At, [4], "x", null)!;
            await context.SaveChangesAsync();

            var latest = await context.HistoricalSources.SelectMany(s => s.Snapshots).Where(HistoricalSourceSnapshot.Latest)
                .Select(s => s.HistoricalSourceSnapshotID).OrderBy(i => i).ToListAsync();

            Assert.That(latest, Is.EqualTo(new[] { a2.HistoricalSourceSnapshotID, b2.HistoricalSourceSnapshotID }.Order()),
                $"Not {a1.HistoricalSourceSnapshotID} (older) nor {b1.HistoricalSourceSnapshotID} (as old, a lower ID)");
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [Test]
    public async Task TheDatabase_RefusesAJobOrReviewWhoseFieldsDontFitItsStatus()
    {
        var context = await SetUpFixture.NewGrifballContext();

        // Past the private setters, as only something outside the class could (raw SQL, a bad migration)
        async Task<bool> Refused<T>(Func<GrifballContext, T> add, Action<EntityEntry<T>> corrupt) where T : class
        {
            await using var ctx = context.NewContextLike();
            var entity = add(ctx);
            corrupt(ctx.Entry(entity));
            try
            {
                await ctx.SaveChangesAsync();
                return false;
            }
            catch (DbUpdateException)
            {
                return true;
            }
        }
        HistoricalImportJob Job(GrifballContext ctx, HistoricalImportJob job) => ctx.HistoricalImportJobs.Add(job).Entity;
        HistoricalSeasonReview Review(GrifballContext ctx)
        {
            var job = Job(ctx, HistoricalImportJob.Seed(guarded: true));
            return job.AddReview("a", null, stopped: true, default);
        }

        try
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(await Refused(c => Job(c, HistoricalImportJob.Refresh()), e => e.Property(j => j.FinishedAt).CurrentValue = At), Is.True, "A queued job hasn't finished");
                Assert.That(await Refused(c => Job(c, HistoricalImportJob.Refresh()), e => e.Property(j => j.Status).CurrentValue = HistoricalImportJobStatus.Succeeded), Is.True, "An ended job says when");
                Assert.That(await Refused(c => Job(c, HistoricalImportJob.Upload("{}")), e =>
                {
                    e.Property(j => j.Status).CurrentValue = HistoricalImportJobStatus.Failed;
                    e.Property(j => j.FinishedAt).CurrentValue = At;
                }), Is.True, "An ended job keeps no archive");
                Assert.That(await Refused(c => Job(c, HistoricalImportJob.Refresh()), e => e.Property(j => j.Payload).CurrentValue = "{}"), Is.True, "Only an import has an archive");
                Assert.That(await Refused(c => Job(c, HistoricalImportJob.Upload("{}")), _ => { }), Is.False);
                Assert.That(await Refused(Review, e => e.Property(r => r.Status).CurrentValue = HistoricalSeasonReviewStatus.Approved), Is.True, "A decision says when");
                Assert.That(await Refused(Review, e => e.Property(r => r.DecidedAt).CurrentValue = At), Is.True, "A stopped season isn't decided yet");
                Assert.That(await Refused(Review, e => e.Property(r => r.ApprovalJobID).CurrentValue = 1), Is.True, "Only an approval queues an import");
                Assert.That(await Refused(Review, _ => { }), Is.False);
                Assert.That(await Refused(c => { var s = HistoricalImportSettings.Create(); s.HistoricalImportSettingsID = 2; return c.HistoricalImportSettings.Add(s).Entity; }, _ => { }), Is.True, "One row");
            }
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }
}
