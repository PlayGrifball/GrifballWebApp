using GrifballWebApp.Application.Historical.Sources;
using GrifballWebApp.Domain.Archive;

namespace GrifballWebApp.Application.Test;

[TestFixture]
internal sealed class HistoricalSourceCatalogAndConfigTests
{
    [Test]
    public void Sources_HasEntriesWithUniqueIDs()
    {
        var sources = HistoricalSourceCatalog.Sources;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sources, Is.Not.Empty);
            Assert.That(sources.Select(s => s.HistoricalSourceID).Distinct().Count(), Is.EqualTo(sources.Count));
        }
    }

    [Test]
    public void Sources_EveryEntry_HasATitleAndUrl()
    {
        var sources = HistoricalSourceCatalog.Sources;

        var blank = sources.Where(s => string.IsNullOrWhiteSpace(s.Title) || string.IsNullOrWhiteSpace(s.Url)).ToList();

        Assert.That(blank, Is.Empty);
    }

    [Test]
    public void Entry_ToListing_CopiesCatalogFields()
    {
        var entry = HistoricalSourceCatalog.Sources.First(s => s.Kind == HistoricalSourceKind.GoogleSheet);

        var listing = entry.ToListing();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(listing.Category, Is.EqualTo(entry.Category));
            Assert.That(listing.Title, Is.EqualTo(entry.Title));
            Assert.That(listing.Url, Is.EqualTo(entry.Url));
            Assert.That(listing.Kind, Is.EqualTo(entry.Kind));
            Assert.That(listing.IsDead, Is.EqualTo(entry.IsDead));
        }
    }

    [Test]
    public void FromJson_Null_ReturnsNull()
    {
        Assert.That(HistoricalSourceConfig.FromJson(null), Is.Null);
    }

    [Test]
    public void FromJson_Blank_ReturnsNull()
    {
        Assert.That(HistoricalSourceConfig.FromJson("   "), Is.Null);
    }

    [Test]
    public void ToJson_ThenFromJson_RoundTrips()
    {
        var config = new HistoricalSourceConfig
        {
            Season = new SeasonDefaults { Name = "Summer League 2025", League = "AGLA", Game = "infinite" },
            Tabs = [new StatsTab { Name = "Stats", HeaderRow = 1, IsPlayoffs = false }],
            ChampionsSheet = "Champions",
            PlayersSheet = "Players",
            RosterSheets = ["Rosters"],
        };

        var json = config.ToJson();
        var roundTripped = HistoricalSourceConfig.FromJson(json);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(roundTripped, Is.Not.Null);
            Assert.That(roundTripped!.Season!.Name, Is.EqualTo("Summer League 2025"));
            Assert.That(roundTripped.Tabs, Has.Count.EqualTo(1));
            Assert.That(roundTripped.Tabs[0].Name, Is.EqualTo("Stats"));
            Assert.That(roundTripped.ChampionsSheet, Is.EqualTo("Champions"));
            Assert.That(roundTripped.PlayersSheet, Is.EqualTo("Players"));
            Assert.That(roundTripped.RosterSheets, Is.EqualTo(RosterSheetNames));
        }
    }

    private static readonly List<string> RosterSheetNames = ["Rosters"];

    [Test]
    public void SheetNames_CollectsEveryKindOfSheetNameTrimmed()
    {
        var config = new HistoricalSourceConfig
        {
            Tabs = [new StatsTab { Name = " Stats ", DatesSheet = "Dates" }],
            Tables = [new StatsTable { Sheet = "Table", Game = "infinite", Players = new TableLookup { Sheet = "PlayerLookup", Key = "id", Column = "name" } }],
            Waypoint = [new WaypointTab { Sheet = "Waypoint", Season = "src/1" }],
            Games = [new GamesTab { Sheet = "Games", Season = "src/1", Game = "infinite" }],
            RosterSheets = ["Rosters"],
            Rosters = [new RosterTab { Sheet = "DraftSheet", Season = "src/1" }],
            ChampionsSheet = "Champions",
            PlayersSheet = "Players",
        };

        var names = config.SheetNames();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(names, Has.Count.EqualTo(10));
            Assert.That(names, Does.Contain("Stats"));
            Assert.That(names, Does.Contain("Dates"));
            Assert.That(names, Does.Contain("Table"));
            Assert.That(names, Does.Contain("PlayerLookup"));
            Assert.That(names, Does.Contain("Waypoint"));
            Assert.That(names, Does.Contain("Games"));
            Assert.That(names, Does.Contain("Rosters"));
            Assert.That(names, Does.Contain("DraftSheet"));
            Assert.That(names, Does.Contain("Champions"));
            Assert.That(names, Does.Contain("Players"));
            // Case-insensitive: "stats" (any case) is the same entry as "Stats" - names' own comparer decides
            // this, not NUnit's Does.Contain (which wouldn't consult it), so the check goes through Contains directly.
            var hasStatsAnyCase = names.Contains("STATS");
            Assert.That(hasStatsAnyCase, Is.True);
        }
    }

    [Test]
    public void SheetNames_IgnoresNullEntries()
    {
        var config = new HistoricalSourceConfig
        {
            Tabs = [new StatsTab { Name = "Stats" }],
        };

        var names = config.SheetNames();

        Assert.That(names, Is.EqualTo(JustStats));
    }

    private static readonly HashSet<string> JustStats = new(StringComparer.OrdinalIgnoreCase) { "Stats" };
}
