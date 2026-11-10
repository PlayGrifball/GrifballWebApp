using GrifballWebApp.Application.Historical;

namespace GrifballWebApp.Integration.Test;

/// <summary>The compiled historical archive the tests read from disk (Fixtures/HistoricalArchive).</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class CompiledHistoricalArchiveTests
{
    /// <summary>The compiled Waypoint matches (a few seasons of the real archive, Fixtures/HistoricalArchive) are keyed
    /// by Halo Infinite seasons' HistoricalSourceIDs and hold match IDs only, so the import uses all of it.</summary>
    [Test]
    public async Task TheCompiledWaypointMatches_KeyHaloInfiniteSeasons_AndAreMatchIds()
    {
        var compiled = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "HistoricalArchive");
        await using var seasonsFile = File.OpenRead(Path.Combine(compiled, "unified-historical-seasons.json"));
        await using var matchesFile = File.OpenRead(Path.Combine(compiled, "waypoint-matches.json"));
        var seasons = await HistoricalArchiveInput.ReadAsync<UnifiedArchive>(seasonsFile);
        var matches = await HistoricalArchiveInput.ReadAsync<Dictionary<string, WaypointSeasonMatches>>(matchesFile);

        var infinite = seasons.Seasons.Where(s => s.GameVersion == "infinite" && !s.IsPlayoffs)
            .Select(s => HistoricalKeys.SeasonSourceId(s.SourceWorkbook!, s.SourceSheet!)).ToHashSet();
        var ids = matches.Values.SelectMany(m => m.Regular.Concat(m.Playoffs)).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(matches.Keys, Is.SubsetOf(infinite));
            Assert.That(matches, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(ids.Select(HistoricalKeys.MatchId), Has.None.Null);
            Assert.That(ids, Is.Unique, "No match in two seasons, or twice in one");
        }
    }
}
