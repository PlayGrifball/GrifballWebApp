using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Archive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using GrifballWebApp.Application.Queries;

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
    private static readonly DateTime At = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    private static HistoricalSourceListing Listing(bool isDead = false) => new() { Category = "Test", Title = "T", Url = "https://example.org/", IsDead = isDead };

    [Test]
    public void ADeadSource_IsNeverEnabled_AndNeitherIsDownloaded()
    {
        var dead = HistoricalSource.FromCatalog("dead", Listing(isDead: true), enabled: true);
        var disabled = HistoricalSource.FromCatalog("off", Listing(), enabled: false);

        Assert.Multiple(() =>
        {
            Assert.That((dead.Enabled, dead.IsDownloadable), Is.EqualTo((false, false)));
            Assert.That(disabled.IsDownloadable, Is.False);
            Assert.That(() => dead.Fetched(At, [1], "text/html", null), Throws.InvalidOperationException);
            Assert.That(() => disabled.FetchFailed(At, "boom"), Throws.InvalidOperationException);
            Assert.That(() => HistoricalSource.FromCatalog(" ", Listing(), true), Throws.ArgumentException);
        });
    }

    [Test]
    public void Fetched_KeepsAChangedContent_AndOnlyNotesAnUnchangedOne()
    {
        var source = HistoricalSource.FromCatalog("s", Listing(), enabled: true);
        var content = "a,b"u8.ToArray();

        var kept = source.Fetched(At, content, new string('x', 150), null);
        var again = source.Fetched(At.AddDays(1), content, "text/csv", kept!.Sha256);

        Assert.Multiple(() =>
        {
            Assert.That((kept.Sha256, kept.Size, kept.ContentType!.Length, kept.FetchedAt, kept.Source), Is.EqualTo(
                (HistoricalSourceSnapshot.Hash(content), 3L, HistoricalSourceSnapshot.MaxContentTypeLength, At, source)));
            Assert.That(kept.Sha256, Has.Length.EqualTo(64).And.EqualTo(kept.Sha256.ToLowerInvariant()));
            Assert.That(again, Is.Null);
            Assert.That(source.Snapshots, Is.EqualTo(new[] { kept }));
            Assert.That((source.LastFetchedAt, source.LastFetchStatus, source.LastChangedAt), Is.EqualTo(((DateTime?)At.AddDays(1), HistoricalSource.UnchangedStatus, (DateTime?)At)));
        });
    }

    [Test]
    public void FetchFailed_SaysWhy_CutToItsColumn()
    {
        var source = HistoricalSource.FromCatalog("s", Listing(), enabled: true);

        source.FetchFailed(At, new string('x', 400));

        Assert.That((source.LastFetchedAt, source.LastFetchStatus!.Length, source.LastFetchStatus[..8]), Is.EqualTo(((DateTime?)At, HistoricalSource.MaxFetchStatusLength, "Failed: ")));
    }

    [Test]
    public void UpdateCatalog_TakesTheCatalogsFields_AndLeavesEnabledAlone()
    {
        var source = HistoricalSource.FromCatalog("s", Listing(), enabled: false);

        source.UpdateCatalog(Listing() with { Title = "New", Config = "{}", Kind = HistoricalSourceKind.WebPage }, enabled: true);

        Assert.Multiple(() =>
        {
            Assert.That((source.Title, source.Config, source.Kind, source.Enabled, source.IsImported), Is.EqualTo(("New", "{}", HistoricalSourceKind.WebPage, false, true)));
            Assert.That(() => source.UpdateCatalog(Listing() with { Url = "" }, enabled: true), Throws.ArgumentException);
        });
        source.SetEnabled(true);
        Assert.That(source.IsDownloadable, Is.True);
    }

    [Test]
    public void ADeadSource_IsNeverDownloaded_AndSetEnabledRefusesToEnableIt()
    {
        var source = HistoricalSource.FromCatalog("s", Listing(), enabled: true);

        source.UpdateCatalog(Listing() with { IsDead = true }, enabled: true);
        var whileDead = (source.Enabled, source.IsDownloadable);
        var enable = source.SetEnabled(true);
        var disable = source.SetEnabled(false);
        var bornDead = HistoricalSource.FromCatalog("d", Listing() with { IsDead = true }, enabled: true);

        Assert.Multiple(() =>
        {
            Assert.That(whileDead, Is.EqualTo((true, false)), "Marked dead by the catalog: Enabled is kept, and IsDead masks it");
            Assert.That(enable, Is.EqualTo(HistoricalSourceSetEnabledResult.Dead));
            Assert.That((disable, source.Enabled, source.IsDownloadable), Is.EqualTo((HistoricalSourceSetEnabledResult.Set, false, false)));
            Assert.That((bornDead.Enabled, bornDead.SetEnabled(true)), Is.EqualTo((false, HistoricalSourceSetEnabledResult.Dead)));
        });
    }

    [Test]
    public void ASourceTheCatalogRevives_KeepsItsEnabled_OrTakesTheCatalogsWhenNeverDownloaded()
    {
        // Downloaded, then dead, then revived: its own Enabled is back, whatever the catalog says
        var downloaded = HistoricalSource.FromCatalog("r", Listing(), enabled: true);
        downloaded.FetchFailed(At, "boom");
        downloaded.UpdateCatalog(Listing(isDead: true), enabled: true);
        downloaded.UpdateCatalog(Listing(), enabled: false);

        // Listed dead from the start (stored disabled), revived before any download: the catalog's
        var bornDeadOn = HistoricalSource.FromCatalog("on", Listing(isDead: true), enabled: true);
        bornDeadOn.UpdateCatalog(Listing(), enabled: true);
        var bornDeadOff = HistoricalSource.FromCatalog("off", Listing(isDead: true), enabled: true);
        bornDeadOff.UpdateCatalog(Listing(), enabled: false);

        var stillDead = HistoricalSource.FromCatalog("d", Listing(isDead: true), enabled: true);
        stillDead.UpdateCatalog(Listing(isDead: true), enabled: true);

        var alive = HistoricalSource.FromCatalog("a", Listing(), enabled: true);
        alive.SetEnabled(false);
        alive.UpdateCatalog(Listing(), enabled: true);

        Assert.Multiple(() =>
        {
            Assert.That((downloaded.IsDead, downloaded.Enabled, downloaded.IsDownloadable), Is.EqualTo((false, true, true)), "Dead, then revived: enabled again");
            Assert.That((bornDeadOn.IsDead, bornDeadOn.Enabled, bornDeadOn.IsDownloadable), Is.EqualTo((false, true, true)), "Never downloaded: enabled as the catalog says");
            Assert.That((bornDeadOff.IsDead, bornDeadOff.Enabled), Is.EqualTo((false, false)), "Never downloaded, and the catalog says disabled");
            Assert.That((stillDead.IsDead, stillDead.IsDownloadable), Is.EqualTo((true, false)), "Still dead: never downloaded");
            Assert.That(alive.Enabled, Is.False, "Never dead: the sysadmin's disable is kept");
        });
    }

    [Test]
    public void ASourceASysadminTurnedOff_ThatDiesAndIsRevived_StaysOff()
    {
        var source = HistoricalSource.FromCatalog("s", Listing(), enabled: true);
        source.Fetched(At, [1], "text/html", null);
        source.SetEnabled(false);

        source.UpdateCatalog(Listing(isDead: true), enabled: true);
        var whileDead = (source.Enabled, source.IsDownloadable);
        source.UpdateCatalog(Listing(), enabled: true);

        Assert.Multiple(() =>
        {
            Assert.That(whileDead, Is.EqualTo((false, false)));
            Assert.That((source.IsDead, source.Enabled, source.IsDownloadable), Is.EqualTo((false, false, false)), "The catalog says enabled, but the sysadmin's off is kept");
        });
    }

    [Test]
    public void AJob_IsQueued_ByEachFactory_AsWhatItDoes()
    {
        var jobs = new[]
        {
            HistoricalImportJob.Upload("{}"), HistoricalImportJob.Refresh(HistoricalImportJobTrigger.Scheduled), HistoricalImportJob.Preview("1"),
            HistoricalImportJob.ImportSnapshots("1,2", "a/Stats"), HistoricalImportJob.ScheduledImport("3", "src"),
        };

        Assert.Multiple(() =>
        {
            Assert.That(jobs.Select(j => (j.Status, j.Stage)), Is.All.EqualTo((HistoricalImportJobStatus.Queued, (string?)HistoricalImportJob.QueuedStage)));
            Assert.That(jobs.Select(j => (j.Kind, j.Trigger, j.Guarded)), Is.EqualTo(new[]
            {
                (HistoricalImportJobKind.Import, HistoricalImportJobTrigger.Manual, false), (HistoricalImportJobKind.Refresh, HistoricalImportJobTrigger.Scheduled, false),
                (HistoricalImportJobKind.Preview, HistoricalImportJobTrigger.Manual, false), (HistoricalImportJobKind.Import, HistoricalImportJobTrigger.Manual, false),
                (HistoricalImportJobKind.Import, HistoricalImportJobTrigger.Scheduled, true),
            }));
            Assert.That((jobs[3].SnapshotIDs, jobs[3].SeasonKeys, jobs[4].ChangedSourceIDs, jobs[0].Payload), Is.EqualTo(("1,2", "a/Stats", "src", "{}")));
            Assert.That(() => HistoricalImportJob.Upload(""), Throws.ArgumentException);
        });
    }

    [Test]
    public void AddReview_StopsOnlyAGuardedJobsSeason_AndListsOnlyAPreviews()
    {
        var unguarded = HistoricalImportJob.ImportSnapshots();
        var preview = HistoricalImportJob.Preview();

        var listed = preview.AddReview(new string('k', 250), new string('n', 250), stopped: false, new HistoricalSeasonRemovals(5, 1, 4, 2, 3, 3));

        Assert.Multiple(() =>
        {
            Assert.That(() => unguarded.AddReview("a", null, stopped: true, default), Throws.InvalidOperationException);
            Assert.That(() => unguarded.AddReview("a", null, stopped: false, default), Throws.InvalidOperationException);
            Assert.That(() => preview.AddReview("a", null, stopped: true, default), Throws.InvalidOperationException);
            Assert.That((listed.Status, listed.SeasonKey.Length, listed.SeasonName!.Length, listed.Job), Is.EqualTo((HistoricalSeasonReviewStatus.Preview, 200, 200, preview)));
            Assert.That((listed.StatsBefore, listed.StatsRemoved, listed.TeamsBefore, listed.TeamsRemoved, listed.TitlesBefore, listed.TitlesRemoved), Is.EqualTo((5, 1, 4, 2, 3, 3)));
            Assert.That(preview.Reviews, Is.EqualTo(new[] { listed }));
        });
    }

    [Test]
    public void DecideReview_DecidesAStoppedSeasonOnce_AndAnApprovalImportsItAlone()
    {
        var job = HistoricalImportJob.Seed(guarded: true, snapshotIDs: "7");
        var approve = job.AddReview("a/Stats", "A", stopped: true, default);
        var skip = job.AddReview("b/Stats", "B", stopped: true, default);
        (approve.HistoricalSeasonReviewID, skip.HistoricalSeasonReviewID) = (1, 2);

        var approved = job.DecideReview(1, approve: true, At);
        var skipped = job.DecideReview(2, approve: false, At);

        Assert.Multiple(() =>
        {
            Assert.That((approved.Decision, approved.Approval?.SnapshotIDs, approved.Approval?.SeasonKeys, approved.Approval?.Guarded),
                Is.EqualTo((HistoricalSeasonReviewDecision.Decided, "7", "a/Stats", (bool?)false)));
            Assert.That(skipped, Is.EqualTo((HistoricalSeasonReviewDecision.Decided, (HistoricalImportJob?)null)));
            Assert.That((approve.Status, approve.DecidedAt, skip.Status, skip.DecidedAt), Is.EqualTo((HistoricalSeasonReviewStatus.Approved, (DateTime?)At, HistoricalSeasonReviewStatus.Skipped, (DateTime?)At)));
            Assert.That(job.DecideReview(1, approve: false, At).Decision, Is.EqualTo(HistoricalSeasonReviewDecision.NotAwaitingReview), "Decided once");
            Assert.That(job.DecideReview(3, approve: true, At).Decision, Is.EqualTo(HistoricalSeasonReviewDecision.NotFound));
            Assert.That(() => job.ApprovalQueued(2, 9), Throws.InvalidOperationException, "A skipped season queues nothing");
            Assert.That(() => job.ApprovalQueued(3, 9), Throws.InvalidOperationException);
        });
        job.ApprovalQueued(1, 9);
        Assert.Multiple(() =>
        {
            Assert.That(approve.ApprovalJobID, Is.EqualTo(9));
            Assert.That(() => job.ApprovalQueued(1, 10), Throws.InvalidOperationException, "Queued once");
        });
    }

    [Test]
    public void APreviewsSeason_IsntDecided()
    {
        var preview = HistoricalImportJob.Preview();
        preview.AddReview("a", null, stopped: false, default).HistoricalSeasonReviewID = 1;

        Assert.That(preview.DecideReview(1, approve: true, At).Decision, Is.EqualTo(HistoricalSeasonReviewDecision.NotAwaitingReview));
    }

    [Test]
    public void ApplyPreview_ImportsOnlyAFinishedPreview() => Assert.Multiple(() =>
    {
        Assert.That(HistoricalImportJob.Preview("1").ApplyPreview(), Is.Null, "Queued");
        Assert.That(HistoricalImportJob.ImportSnapshots("1").ApplyPreview(), Is.Null, "Not a preview");
    });

    [Test]
    public void TheSchedulersRow_NotesTheFirstWait_AndCarriesAPauseOverOnce()
    {
        var settings = HistoricalImportSettings.Create();

        Assert.Multiple(() =>
        {
            Assert.That(settings.HistoricalImportSettingsID, Is.EqualTo(HistoricalImportSettings.SingletonID));
            Assert.That(settings.ScheduledRunWaited("first"), Is.True);
            Assert.That(settings.ScheduledRunWaited("second"), Is.False);
            Assert.That(settings.LastScheduledRunNote, Is.EqualTo("first"));
            Assert.That(() => settings.PauseCarriedOver(), Throws.InvalidOperationException, "It wasn't paused");
        });
        settings.ScheduledRunQueued(4, At);
        var paused = HistoricalImportSettings.PausedBeforeQuartz();
        paused.PauseCarriedOver();
        Assert.That((settings.LastScheduledRunAt, settings.LastScheduledJobID, settings.LastScheduledRunNote, paused.SchedulePaused),
            Is.EqualTo(((DateTime?)At, (int?)4, (string?)null, false)));
    }

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
            Assert.Multiple(() =>
            {
                Assert.That((beatQueued, finishQueued, claimed, claimedAgain), Is.EqualTo((false, false, (DateTime?)At, (DateTime?)null)), "Only a queued job is claimed, and once");
                Assert.That((progress, read, requeued, reclaimed), Is.EqualTo((true, true, 1, (DateTime?)At.AddMinutes(3))), "A stale running job is queued again, and claimed again");
                Assert.That((finished, finishedAgain, progressEnded), Is.EqualTo((true, false, false)), "It ends once");
                Assert.That((row.Status, row.Stage, row.Error, row.Payload, row.FinishedAt, row.SnapshotIDs, row.SeasonsDone, row.SeasonsTotal),
                    Is.EqualTo((HistoricalImportJobStatus.Failed, (string?)HistoricalImportJob.FailedStage, (string?)"boom", (string?)null, (DateTime?)At.AddMinutes(4), (string?)"3,4", 1, 2)));
                Assert.That((deletedEarly, deleted), Is.EqualTo((0, 1)));
            });
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
            Assert.Multiple(() =>
            {
                Assert.That(firstMoves, Is.EqualTo((false, false, false, false)), "The first run's job is the second's now");
                Assert.That(secondMoves, Is.EqualTo((true, true, true, true)));
                Assert.That((row.Status, row.Error, row.Report, row.SnapshotIDs, row.SeasonsDone, row.StartedAt, row.FinishedAt),
                    Is.EqualTo((HistoricalImportJobStatus.Failed, (string?)"second's", (string?)null, (string?)"2", 2, (DateTime?)At.AddMinutes(2), (DateTime?)At.AddMinutes(5))));
            });
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

            Assert.Multiple(() =>
            {
                Assert.That(claim, Is.EqualTo(At.AddTicks(1234560)));
                Assert.That(beat, Is.True, "The stored start is the claim");
                Assert.That((sameStart, earlier), Is.EqualTo(((DateTime?)null, (DateTime?)null)));
                Assert.That(later, Is.EqualTo((DateTime?)At.AddTicks(1234570)));
            });
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
            Assert.Multiple(async () =>
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
            });
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }
}
