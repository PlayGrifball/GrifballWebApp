using GrifballWebApp.Domain.Archive;

namespace GrifballWebApp.Domain.Test;

/// <summary>HistoricalProvenance.Record: where the historical import got a row from, its text cut to its columns.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalProvenanceUnitTests
{
    private static readonly DateTime At = new(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Run = Guid.Parse("8d6f1c55-2e4b-4a8e-9a63-2a1b7c0d9e11");

    [Test]
    public void Record_KeepsWhereTheRowCameFrom()
    {
        var row = HistoricalProvenance.Record("User", "4182", HistoricalProvenanceKind.Created, Run, At, "2023-da-wl23-stats", " Stats ", "A5:K5",
            "Rayzyr (c) | Tuna", "Placeholder for Rayzyr", historicalImportJobID: 12, historicalSourceSnapshotID: 34);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((row.Entity, row.EntityKey, row.Kind, row.RunID, row.RecordedAt), Is.EqualTo(("User", "4182", HistoricalProvenanceKind.Created, Run, At)));
            Assert.That((row.SourceID, row.Sheet, row.Cells, row.Value, row.Detail),
                Is.EqualTo(("2023-da-wl23-stats", (string?)"Stats", (string?)"A5:K5", (string?)"Rayzyr (c) | Tuna", (string?)"Placeholder for Rayzyr")), "trimmed");
            Assert.That((row.HistoricalImportJobID, row.HistoricalSourceSnapshotID), Is.EqualTo(((int?)12, (int?)34)));
        }
    }

    [Test]
    public void Record_CutsTextLongerThanItsColumn_AndBlankTextIsNone()
    {
        var row = HistoricalProvenance.Record(new string('e', 150), "1", HistoricalProvenanceKind.Read, Run, At, "source", sheet: "  ",
            cells: new string('c', 80), value: new string('v', 1500), detail: new string('d', 700));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.Entity, Has.Length.EqualTo(HistoricalProvenance.MaxEntityLength));
            Assert.That(row.Sheet, Is.Null, "blank is none");
            Assert.That(row.Cells, Has.Length.EqualTo(HistoricalProvenance.MaxCellsLength));
            Assert.That(row.Value, Has.Length.EqualTo(HistoricalProvenance.MaxValueLength));
            Assert.That(row.Detail, Has.Length.EqualTo(HistoricalProvenance.MaxDetailLength));
        }
    }

    [Test]
    public void Record_RefusesARowOfNothing()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => HistoricalProvenance.Record(" ", "1", HistoricalProvenanceKind.Read, Run, At, "source"), Throws.ArgumentException, "no entity");
            Assert.That(() => HistoricalProvenance.Record("User", "", HistoricalProvenanceKind.Read, Run, At, "source"), Throws.ArgumentException, "no key");
            Assert.That(() => HistoricalProvenance.Record("User", "1", HistoricalProvenanceKind.Read, Run, At, " "), Throws.ArgumentException, "no source");
            Assert.That(() => HistoricalProvenance.Record("User", "1", (HistoricalProvenanceKind)9, Run, At, "source"), Throws.InstanceOf<ArgumentOutOfRangeException>(), "no such kind");
        }
    }
}
