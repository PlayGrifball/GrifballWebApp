using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Application.Test;

/// <summary>HistoricalProvenanceService: where the historical import got a row from, for sysadmins tracking bad data back to
/// its sheet. Against the fake SQLite context.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
internal sealed class HistoricalProvenanceServiceUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime At = new(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Run = Guid.Parse("0b4a7c1e-91f3-4d6a-8e25-5c3f0d2a7b19");

    private HistoricalProvenanceService _service = null!;

    [SetUp]
    public void SetUp() => _service = new HistoricalProvenanceService(Context);

    private void Row(string entity, string key, HistoricalProvenanceKind kind, string source = "wl23", string? sheet = "Stats", string? cells = "A5:K5", string? detail = null)
        => Context.HistoricalProvenance.Add(HistoricalProvenance.Record(entity, key, kind, Run, At, source, sheet, cells, "text", detail, 3, 4));

    private UserMerge Merge(int source, int target)
    {
        var merge = UserMerge.Record(At, MergeTrigger.Manual, null, null, source, $"historical:{source}", target, $"user{target}", "{}", 1, 0, 0);
        Context.UserMerges.Add(merge);
        return merge;
    }

    [Test]
    public async Task ForEntity_TheCellsThatMadeIt_ThenTheLatestRead_ThenTheChanges()
    {
        Row("Team", "7", HistoricalProvenanceKind.Changed, detail: "renamed");
        Row("Team", "7", HistoricalProvenanceKind.Read, cells: "A9:K9");
        Row("Team", "7", HistoricalProvenanceKind.Created);
        Row("Team", "8", HistoricalProvenanceKind.Created);
        Row("User", "7", HistoricalProvenanceKind.Created);
        Context.HistoricalSources.Add(HistoricalSource.FromCatalog("wl23", new HistoricalSourceListing
        {
            Category = "AGLA-2023", Title = "Winter League 2023", Url = "https://docs.google.com/spreadsheets/d/abc/edit", SheetKey = "abc",
        }, enabled: true));
        await Context.SaveChangesAsync();

        var rows = await _service.ForEntity("Team", "7", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows.Select(r => (r.Kind, r.Cells)), Is.EqualTo(new[]
            {
                (HistoricalProvenanceKind.Created, (string?)"A5:K5"),
                (HistoricalProvenanceKind.Read, "A9:K9"),
                (HistoricalProvenanceKind.Changed, "A5:K5"),
            }), "that team's only, in that order");
            Assert.That((rows[0].SourceTitle, rows[0].SourceUrl), Is.EqualTo(((string?)"Winter League 2023", (string?)"https://docs.google.com/spreadsheets/d/abc/edit")));
            Assert.That(rows[0].CellsUrl, Is.Null, "a whole workbook's tab has no gid to link");
            Assert.That((rows[0].HistoricalImportJobID, rows[0].HistoricalSourceSnapshotID, rows[0].Value), Is.EqualTo(((int?)3, (int?)4, (string?)"text")));
        }
    }

    [Test]
    public async Task ForEntity_AUser_HasTheRowsOfThePlaceholdersMergedIntoThem()
    {
        // Placeholder 11 merged into 12, then 12 into the real user 20; 30 into someone else; 40 into 20, then undone
        Merge(11, 12);
        Merge(12, 20);
        Merge(30, 31);
        Merge(40, 20).Undo(At, null, "wrong person");
        Row("User", "40", HistoricalProvenanceKind.Created, cells: "B7");
        Row("User", "11", HistoricalProvenanceKind.Created, cells: "B3");
        Row("User", "12", HistoricalProvenanceKind.Created, cells: "B4");
        Row("User", "20", HistoricalProvenanceKind.Changed, cells: "B5");
        Row("User", "30", HistoricalProvenanceKind.Created, cells: "B6");
        await Context.SaveChangesAsync();

        var rows = await _service.ForEntity("User", "20", CancellationToken.None);
        var spaced = await _service.ForEntity(" User ", " 20 ", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows.Select(r => (r.EntityKey, r.Cells)), Is.EquivalentTo(new[] { ("11", (string?)"B3"), ("12", "B4"), ("20", "B5") }), "an undone merge's placeholder is its own again");
            Assert.That(spaced.Select(r => r.EntityKey), Is.EquivalentTo(rows.Select(r => r.EntityKey)), "a lookup with spaces around it is the same lookup");
        }
    }

    [Test]
    public async Task ForSource_WhatATabMade_APageAtATime()
    {
        Row("User", "1", HistoricalProvenanceKind.Created, cells: "A2");
        Row("User", "2", HistoricalProvenanceKind.Created, cells: "A3");
        Row("Team", "1", HistoricalProvenanceKind.Created, cells: "B2");
        Row("User", "3", HistoricalProvenanceKind.Created, sheet: "Playoffs", cells: "A2");
        Row("User", "4", HistoricalProvenanceKind.Created, source: "other");
        await Context.SaveChangesAsync();

        var stats = await _service.ForSource("wl23", "Stats", 0, 100, CancellationToken.None);
        var second = await _service.ForSource("wl23", null, 1, 2, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stats.Select(r => (r.Entity, r.EntityKey)), Is.EqualTo(new[] { ("User", "1"), ("User", "2"), ("Team", "1") }), "by cells");
            Assert.That(second.Select(r => (r.Sheet, r.Cells)), Is.EqualTo(new[] { ((string?)"Stats", (string?)"A2"), ("Stats", "A3") }),
                "every tab, sorted by tab: Playoffs first, then the page");
        }
    }

    [TestCase("abc", "123", null, "Stats", "A5:K5", "https://docs.google.com/spreadsheets/d/abc/edit#gid=123&range=A5%3AK5")]
    [TestCase("abc", "123", "Stats", "stats", "C4", "https://docs.google.com/spreadsheets/d/abc/edit#gid=123&range=C4")]
    [TestCase("abc", "123", "Schedule", "Stats", "C4", null)]
    [TestCase("abc", null, null, "Stats", "C4", null)]
    [TestCase(null, "123", null, "Stats", "C4", null)]
    [TestCase("abc", "123", null, "Stats", null, null)]
    public void CellsUrl_OnlyForOneTabOfGoogleSheets(string? sheetKey, string? gid, string? csvSheetName, string? sheet, string? cells, string? expected)
        => Assert.That(HistoricalProvenanceService.CellsUrl(sheetKey, gid, csvSheetName, sheet, cells), Is.EqualTo(expected));
}
