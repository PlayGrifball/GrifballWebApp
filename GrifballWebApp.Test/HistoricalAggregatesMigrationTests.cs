using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>
/// The HistoricalAggregates migration (#320) settles the rows its constraints would refuse before adding them: an ended
/// job gets its finish time and loses its archive, a job still going loses a finish time, a decided review gets its time
/// and only an approved one keeps its import, and the scheduler keeps its one row; alike on whichever database the suite
/// runs on. A row that fits is left as it was.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalAggregatesMigrationTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task TheRowsTheConstraintsWouldRefuse_AreSettledFirst()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_HistoricalAggregates"))) - 1]);

        var modified = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var finished = modified.AddHours(1);
        // The columns are as they were: only the constraints are new. Past the private setters, as rows written before them could be.
        var endedWithoutTime = HistoricalImportJob.Upload("{}");
        var queuedButFinished = HistoricalImportJob.Refresh();
        var refreshWithArchive = HistoricalImportJob.Refresh();
        var fine = HistoricalImportJob.Seed(guarded: true);
        var approvedWithoutTime = fine.AddReview("a", null, stopped: true, default);
        var skippedWithImport = fine.AddReview("b", null, stopped: true, default);
        var pendingButDecided = fine.AddReview("c", null, stopped: true, default);
        var stillPending = fine.AddReview("d", null, stopped: true, default);
        var extraSettings = HistoricalImportSettings.Create();
        extraSettings.HistoricalImportSettingsID = 2;
        _context.HistoricalImportJobs.AddRange(endedWithoutTime, queuedButFinished, refreshWithArchive, fine);
        _context.HistoricalImportSettings.AddRange(HistoricalImportSettings.Create(), extraSettings);
        _context.Entry(endedWithoutTime).Property(j => j.Status).CurrentValue = HistoricalImportJobStatus.Succeeded;
        _context.Entry(queuedButFinished).Property(j => j.FinishedAt).CurrentValue = finished;
        _context.Entry(refreshWithArchive).Property(j => j.Payload).CurrentValue = "{}";
        _context.Entry(approvedWithoutTime).Property(r => r.Status).CurrentValue = HistoricalSeasonReviewStatus.Approved;
        _context.Entry(skippedWithImport).Property(r => r.Status).CurrentValue = HistoricalSeasonReviewStatus.Skipped;
        _context.Entry(skippedWithImport).Property(r => r.DecidedAt).CurrentValue = finished;
        _context.Entry(skippedWithImport).Property(r => r.ApprovalJobID).CurrentValue = 99;
        _context.Entry(pendingButDecided).Property(r => r.DecidedAt).CurrentValue = finished;
        // The audit columns as the interceptor would have written them
        foreach (var entry in _context.ChangeTracker.Entries<AuditableEntity>())
            entry.Entity.ModifiedAt = modified;
        await _context.SaveChangesAsync();

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var jobs = await _context.HistoricalImportJobs.AsNoTracking().ToDictionaryAsync(j => j.HistoricalImportJobID, j => (j.Status, j.FinishedAt, j.Payload));
        var reviews = await _context.HistoricalSeasonReviews.AsNoTracking().ToDictionaryAsync(r => r.SeasonKey, r => (r.Status, r.DecidedAt, r.ApprovalJobID));
        var settings = await _context.HistoricalImportSettings.AsNoTracking().Select(s => s.HistoricalImportSettingsID).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(jobs[endedWithoutTime.HistoricalImportJobID], Is.EqualTo((HistoricalImportJobStatus.Succeeded, (DateTime?)modified, (string?)null)),
                "Ended at its last write, its archive let go of");
            Assert.That(jobs[queuedButFinished.HistoricalImportJobID], Is.EqualTo((HistoricalImportJobStatus.Queued, (DateTime?)null, (string?)null)));
            Assert.That(jobs[refreshWithArchive.HistoricalImportJobID], Is.EqualTo((HistoricalImportJobStatus.Queued, (DateTime?)null, (string?)null)), "Only an import has one");
            Assert.That(jobs[fine.HistoricalImportJobID], Is.EqualTo((HistoricalImportJobStatus.Queued, (DateTime?)null, (string?)null)));
            Assert.That(reviews["a"], Is.EqualTo((HistoricalSeasonReviewStatus.Approved, (DateTime?)modified, (int?)null)));
            Assert.That(reviews["b"], Is.EqualTo((HistoricalSeasonReviewStatus.Skipped, (DateTime?)finished, (int?)null)), "Only an approval queues an import");
            Assert.That(reviews["c"], Is.EqualTo((HistoricalSeasonReviewStatus.NeedsReview, (DateTime?)null, (int?)null)));
            Assert.That(reviews["d"], Is.EqualTo((HistoricalSeasonReviewStatus.NeedsReview, (DateTime?)null, (int?)null)));
            Assert.That(settings, Is.EqualTo(new[] { HistoricalImportSettings.SingletonID }));
        });
    }
}
