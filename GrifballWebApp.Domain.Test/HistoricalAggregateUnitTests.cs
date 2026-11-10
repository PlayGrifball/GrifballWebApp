using GrifballWebApp.Domain.Archive;
using static GrifballWebApp.Domain.Test.HistoricalAggregateTestData;

namespace GrifballWebApp.Domain.Test;

/// <summary>HistoricalAggregateTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalAggregateUnitTests
{
    internal static readonly DateTime At = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void ADeadSource_IsNeverEnabled_AndNeitherIsDownloaded()
    {
        var dead = HistoricalSource.FromCatalog("dead", Listing(isDead: true), enabled: true);
        var disabled = HistoricalSource.FromCatalog("off", Listing(), enabled: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((dead.Enabled, dead.IsDownloadable), Is.EqualTo((false, false)));
            Assert.That(disabled.IsDownloadable, Is.False);
            Assert.That(() => dead.Fetched(At, [1], "text/html", null), Throws.InvalidOperationException);
            Assert.That(() => disabled.FetchFailed(At, "boom"), Throws.InvalidOperationException);
            Assert.That(() => HistoricalSource.FromCatalog(" ", Listing(), true), Throws.ArgumentException);
        }
    }

    [Test]
    public void Fetched_KeepsAChangedContent_AndOnlyNotesAnUnchangedOne()
    {
        var source = HistoricalSource.FromCatalog("s", Listing(), enabled: true);
        var content = "a,b"u8.ToArray();

        var kept = source.Fetched(At, content, new string('x', 150), null);
        var again = source.Fetched(At.AddDays(1), content, "text/csv", kept!.Sha256);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((kept.Sha256, kept.Size, kept.ContentType!.Length, kept.FetchedAt, kept.Source), Is.EqualTo(
                (HistoricalSourceSnapshot.Hash(content), 3L, HistoricalSourceSnapshot.MaxContentTypeLength, At, source)));
            Assert.That(kept.Sha256, Has.Length.EqualTo(64).And.EqualTo(kept.Sha256.ToLowerInvariant()));
            Assert.That(again, Is.Null);
            Assert.That(source.Snapshots, Is.EqualTo(new[] { kept }));
            Assert.That((source.LastFetchedAt, source.LastFetchStatus, source.LastChangedAt), Is.EqualTo(((DateTime?)At.AddDays(1), HistoricalSource.UnchangedStatus, (DateTime?)At)));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That((source.Title, source.Config, source.Kind, source.Enabled, source.IsImported), Is.EqualTo(("New", "{}", HistoricalSourceKind.WebPage, false, true)));
            Assert.That(() => source.UpdateCatalog(Listing() with { Url = "" }, enabled: true), Throws.ArgumentException);
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(whileDead, Is.EqualTo((true, false)), "Marked dead by the catalog: Enabled is kept, and IsDead masks it");
            Assert.That(enable, Is.EqualTo(HistoricalSourceSetEnabledResult.Dead));
            Assert.That((disable, source.Enabled, source.IsDownloadable), Is.EqualTo((HistoricalSourceSetEnabledResult.Set, false, false)));
            Assert.That((bornDead.Enabled, bornDead.SetEnabled(true)), Is.EqualTo((false, HistoricalSourceSetEnabledResult.Dead)));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That((downloaded.IsDead, downloaded.Enabled, downloaded.IsDownloadable), Is.EqualTo((false, true, true)), "Dead, then revived: enabled again");
            Assert.That((bornDeadOn.IsDead, bornDeadOn.Enabled, bornDeadOn.IsDownloadable), Is.EqualTo((false, true, true)), "Never downloaded: enabled as the catalog says");
            Assert.That((bornDeadOff.IsDead, bornDeadOff.Enabled), Is.EqualTo((false, false)), "Never downloaded, and the catalog says disabled");
            Assert.That((stillDead.IsDead, stillDead.IsDownloadable), Is.EqualTo((true, false)), "Still dead: never downloaded");
            Assert.That(alive.Enabled, Is.False, "Never dead: the sysadmin's disable is kept");
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(whileDead, Is.EqualTo((false, false)));
            Assert.That((source.IsDead, source.Enabled, source.IsDownloadable), Is.EqualTo((false, false, false)), "The catalog says enabled, but the sysadmin's off is kept");
        }
    }

    [Test]
    public void AJob_IsQueued_ByEachFactory_AsWhatItDoes()
    {
        var jobs = new[]
        {
            HistoricalImportJob.Upload("{}"), HistoricalImportJob.Refresh(HistoricalImportJobTrigger.Scheduled), HistoricalImportJob.Preview("1"),
            HistoricalImportJob.ImportSnapshots("1,2", "a/Stats"), HistoricalImportJob.ScheduledImport("3", "src"),
        };

        using (Assert.EnterMultipleScope())
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
        }
    }

    [Test]
    public void AddReview_StopsOnlyAGuardedJobsSeason_AndListsOnlyAPreviews()
    {
        var unguarded = HistoricalImportJob.ImportSnapshots();
        var preview = HistoricalImportJob.Preview();

        var listed = preview.AddReview(new string('k', 250), new string('n', 250), stopped: false, new HistoricalSeasonRemovals(5, 1, 4, 2, 3, 3));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => unguarded.AddReview("a", null, stopped: true, default), Throws.InvalidOperationException);
            Assert.That(() => unguarded.AddReview("a", null, stopped: false, default), Throws.InvalidOperationException);
            Assert.That(() => preview.AddReview("a", null, stopped: true, default), Throws.InvalidOperationException);
            Assert.That((listed.Status, listed.SeasonKey.Length, listed.SeasonName!.Length, listed.Job), Is.EqualTo((HistoricalSeasonReviewStatus.Preview, 200, 200, preview)));
            Assert.That((listed.StatsBefore, listed.StatsRemoved, listed.TeamsBefore, listed.TeamsRemoved, listed.TitlesBefore, listed.TitlesRemoved), Is.EqualTo((5, 1, 4, 2, 3, 3)));
            Assert.That(preview.Reviews, Is.EqualTo(new[] { listed }));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That((approved.Decision, approved.Approval?.SnapshotIDs, approved.Approval?.SeasonKeys, approved.Approval?.Guarded),
                Is.EqualTo((HistoricalSeasonReviewDecision.Decided, "7", "a/Stats", (bool?)false)));
            Assert.That(skipped, Is.EqualTo((HistoricalSeasonReviewDecision.Decided, (HistoricalImportJob?)null)));
            Assert.That((approve.Status, approve.DecidedAt, skip.Status, skip.DecidedAt), Is.EqualTo((HistoricalSeasonReviewStatus.Approved, (DateTime?)At, HistoricalSeasonReviewStatus.Skipped, (DateTime?)At)));
            Assert.That(job.DecideReview(1, approve: false, At).Decision, Is.EqualTo(HistoricalSeasonReviewDecision.NotAwaitingReview), "Decided once");
            Assert.That(job.DecideReview(3, approve: true, At).Decision, Is.EqualTo(HistoricalSeasonReviewDecision.NotFound));
            Assert.That(() => job.ApprovalQueued(2, 9), Throws.InvalidOperationException, "A skipped season queues nothing");
            Assert.That(() => job.ApprovalQueued(3, 9), Throws.InvalidOperationException);
        }
        job.ApprovalQueued(1, 9);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(approve.ApprovalJobID, Is.EqualTo(9));
            Assert.That(() => job.ApprovalQueued(1, 10), Throws.InvalidOperationException, "Queued once");
        }
    }

    [Test]
    public void APreviewsSeason_IsntDecided()
    {
        var preview = HistoricalImportJob.Preview();
        preview.AddReview("a", null, stopped: false, default).HistoricalSeasonReviewID = 1;

        Assert.That(preview.DecideReview(1, approve: true, At).Decision, Is.EqualTo(HistoricalSeasonReviewDecision.NotAwaitingReview));
    }

    [Test]
    public void ApplyPreview_ImportsOnlyAFinishedPreview()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(HistoricalImportJob.Preview("1").ApplyPreview(), Is.Null, "Queued");
            Assert.That(HistoricalImportJob.ImportSnapshots("1").ApplyPreview(), Is.Null, "Not a preview");
        }
    }

    [Test]
    public void TheSchedulersRow_NotesTheFirstWait_AndCarriesAPauseOverOnce()
    {
        var settings = HistoricalImportSettings.Create();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.HistoricalImportSettingsID, Is.EqualTo(HistoricalImportSettings.SingletonID));
            Assert.That(settings.ScheduledRunWaited("first"), Is.True);
            Assert.That(settings.ScheduledRunWaited("second"), Is.False);
            Assert.That(settings.LastScheduledRunNote, Is.EqualTo("first"));
            Assert.That(() => settings.PauseCarriedOver(), Throws.InvalidOperationException, "It wasn't paused");
        }
        settings.ScheduledRunQueued(4, At);
        var paused = HistoricalImportSettings.PausedBeforeQuartz();
        paused.PauseCarriedOver();
        Assert.That((settings.LastScheduledRunAt, settings.LastScheduledJobID, settings.LastScheduledRunNote, paused.SchedulePaused),
            Is.EqualTo(((DateTime?)At, (int?)4, (string?)null, false)));
    }
}

/// <summary>HistoricalAggregateTests' helpers, shared with its integration tests.</summary>
internal static class HistoricalAggregateTestData
{
    internal static HistoricalSourceListing Listing(bool isDead = false) => new() { Category = "Test", Title = "T", Url = "https://example.org/", IsDead = isDead };
}
