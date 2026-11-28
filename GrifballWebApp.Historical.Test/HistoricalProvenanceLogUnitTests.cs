using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Historical.Import;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Historical.Test;

/// <summary>HistoricalProvenanceLog: one run's notes of where its rows came from, written once the rows are saved. Against
/// the fake SQLite context, with import jobs as the rows (any entity will do).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
internal sealed class HistoricalProvenanceLogUnitTests : ApplicationDbContextTests
{
    private static readonly ArchiveOrigin Cell = new("wl23", "Stats", "C4", "Church");

    private HistoricalProvenanceLog Log(int? jobID = null) => new(Context, TimeProvider.System, jobID, new Dictionary<string, int> { ["WL23"] = 5 });

    private async Task<HistoricalImportJob> Saved()
    {
        var job = HistoricalImportJob.Refresh();
        Context.HistoricalImportJobs.Add(job);
        await Context.SaveChangesAsync();
        return job;
    }

    private async Task<List<HistoricalProvenance>> Rows() => await Context.HistoricalProvenance.AsNoTracking().OrderBy(p => p.HistoricalProvenanceID).ToListAsync();

    [Test]
    public async Task Read_ARowNotSavedYet_IsCreated_ASavedOne_IsRead()
    {
        var saved = await Saved();
        var added = HistoricalImportJob.Refresh();
        Context.HistoricalImportJobs.Add(added);
        var log = Log(jobID: 9);

        log.Read(saved, Cell, "read");
        log.Read(added, Cell with { Cells = "C5" }, "made");
        await Context.SaveChangesAsync();
        await log.FlushAsync(CancellationToken.None);

        var rows = await Rows();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows.Select(r => (r.Entity, r.EntityKey, r.Kind, r.Cells)), Is.EqualTo(new[]
            {
                (nameof(HistoricalImportJob), saved.HistoricalImportJobID.ToString(System.Globalization.CultureInfo.InvariantCulture), HistoricalProvenanceKind.Read, (string?)"C4"),
                (nameof(HistoricalImportJob), added.HistoricalImportJobID.ToString(System.Globalization.CultureInfo.InvariantCulture), HistoricalProvenanceKind.Created, "C5"),
            }));
            Assert.That(rows.Select(r => (r.HistoricalImportJobID, r.HistoricalSourceSnapshotID, r.RunID)).Distinct(), Is.EqualTo(new[] { ((int?)9, (int?)5, log.RunID) }),
                "the job, the source's snapshot (its ID compared without case) and the run on every row");
            Assert.That(log.Pending, Is.Zero, "flushed");
        }
    }

    [Test]
    public async Task Read_WithoutAnOrigin_NotesNothing_AndARowNeverSaved_IsDropped()
    {
        var saved = await Saved();
        var log = Log();

        log.Read(saved, null);
        log.Changed(saved, null, "nothing");
        log.Created(saved, null);
        log.Read("Match", "abc", null);
        log.Read(HistoricalImportJob.Refresh(), Cell, "never added");
        await log.FlushAsync(CancellationToken.None);

        Assert.That(await Rows(), Is.Empty);
    }

    [Test]
    public async Task Flush_ANewRun_ReplacesTheReadRowsOfWhatItReads_AndKeepsTheRest()
    {
        var job = await Saved();
        var other = await Saved();
        var first = Log();
        first.Created(job, Cell, "made");
        first.Read(job, Cell with { Cells = "C6" }, "read");
        first.Read(other, Cell with { Cells = "D1" }, "read");
        first.Changed(job, Cell with { Cells = "E1" }, "linked");
        await first.FlushAsync(CancellationToken.None);

        var second = Log();
        second.Read(job, Cell with { Cells = "C7" }, "read again");
        // The same cells noted twice are one row
        second.Read(job, Cell with { Cells = "C7" }, "read again");
        await second.FlushAsync(CancellationToken.None);

        var rows = await Rows();
        Assert.That(rows.Select(r => (r.EntityKey == job.HistoricalImportJobID.ToString(System.Globalization.CultureInfo.InvariantCulture), r.Kind, r.Cells)), Is.EquivalentTo(new[]
        {
            (true, HistoricalProvenanceKind.Created, (string?)"C4"),
            (true, HistoricalProvenanceKind.Changed, "E1"),
            (true, HistoricalProvenanceKind.Read, "C7"),
            (false, HistoricalProvenanceKind.Read, "D1"),
        }), "the job's first Read row goes; what made it, what changed it and the other job's stay");
    }

    [Test]
    public async Task Read_ARowItDoesntTrack_IsKeyedByItsTypeAndKey()
    {
        var log = Log();

        log.Read("Match", "6f1c55aa-0000-0000-0000-000000000001", Cell, "listed");
        await log.FlushAsync(CancellationToken.None);

        var row = (await Rows()).Single();
        Assert.That((row.Entity, row.EntityKey, row.Kind, row.Detail), Is.EqualTo(("Match", "6f1c55aa-0000-0000-0000-000000000001", HistoricalProvenanceKind.Read, (string?)"listed")));
    }

    [Test]
    public async Task Discard_ForgetsTheNotes()
    {
        var job = await Saved();
        var log = Log();
        log.Read(job, Cell);

        log.Discard();
        await log.FlushAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(log.Pending, Is.Zero);
            Assert.That(await Rows(), Is.Empty);
        }
    }
}
