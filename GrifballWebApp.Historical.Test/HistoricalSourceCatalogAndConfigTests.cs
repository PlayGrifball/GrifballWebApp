using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Domain.Archive;

namespace GrifballWebApp.Historical.Test;

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
            Drafts = [new DraftTab { Sheet = "Draft Sheet", Season = "src", Layout = DraftLayout.Table, Order = DraftOrder.None }],
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
            Assert.That(roundTripped.Drafts.Select(d => (d.Sheet, d.Season, d.Layout, d.Order)), Is.EqualTo(new[] { ("Draft Sheet", (string?)"src", DraftLayout.Table, DraftOrder.None) }));
            Assert.That(json, Does.Contain("\"order\":\"None\""), "Enums by name");
        }
    }

    private static readonly List<string> RosterSheetNames = ["Rosters"];

    [Test]
    public void SheetNames_CollectsEveryKindOfSheetNameTrimmed()
    {
        var config = new HistoricalSourceConfig
        {
            Tabs = [new StatsTab { Name = " Stats ", DatesSheet = "Dates", GamesSheet = "Match History" }],
            Tables = [new StatsTable { Sheet = "Table", Game = "infinite", Players = new TableLookup { Sheet = "PlayerLookup", Key = "id", Column = "name" } }],
            Waypoint = [new WaypointTab { Sheet = "Waypoint", Season = "src/1" }],
            Games = [new GamesTab { Sheet = "Games", Season = "src/1", Game = "infinite" }],
            RosterSheets = ["Rosters"],
            Rosters = [new RosterTab { Sheet = "DraftSheet", Season = "src/1" }],
            Results = [new ResultsTab { Sheet = " Results " }],
            Drafts = [new DraftTab { Sheet = " Draft Board " }],
            ChampionsSheet = "Champions",
            PlayersSheet = "Players",
            Brackets = [new BracketTab { Sheet = "Playoffs", RecordsSheet = "Teams" }, new BracketTab { Rounds = [new BracketRoundTab { Sheet = "Round 1" }] }],
        };

        var names = config.SheetNames();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(names, Has.Count.EqualTo(16));
            Assert.That(names, Does.Contain("Playoffs"));
            Assert.That(names, Does.Contain("Teams"), "A bracket's records tab");
            Assert.That(names, Does.Contain("Round 1"));
            Assert.That(names, Does.Contain("Draft Board"));
            Assert.That(names, Does.Contain("Match History"));
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
            Assert.That(names, Does.Contain("Results"));
            // Case-insensitive: "stats" (any case) is the same entry as "Stats" - names' own comparer decides
            // this, not NUnit's Does.Contain (which wouldn't consult it), so the check goes through Contains directly.
            var hasStatsAnyCase = names.Contains("STATS");
            Assert.That(hasStatsAnyCase, Is.True);
        }
    }

    private static readonly int?[] GwlSeriesLengths = [5, 5, 5, 5, 7];

    [Test]
    public void BracketLayouts_TakeEachSeriesLengthFromItsSheetsHeader()
    {
        Assert.That(HistoricalBracketLayouts.GwlSpring2023().Select(s => s.BestOf), Is.EqualTo(GwlSeriesLengths), "Quarter- and semi-finals Bo5, the championship Bo7");
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

    private static HistoricalSourceConfig Config(string id) => HistoricalSourceCatalog.Sources.Single(s => s.HistoricalSourceID == id).Config!;

    /// <summary>Both SML21 workbooks carry a hidden copy of Winter League 2021 (Legacy)'s Playoff Stats tab (its teams, its
    /// #REF! cells): read, it put WL21 Legacy's five playoff teams in each SML21 season.</summary>
    [TestCase("2021-sml21-pro-stats")]
    [TestCase("2021-sml21-ammy-stats")]
    public void Sml21_DoesNotRead_ItsCopyOfWinterLeague2021LegacysPlayoffStats(string id)
    {
        var names = Config(id).SheetNames();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(names, Does.Not.Contain("Playoff Stats"));
            Assert.That(names, Does.Contain("FL21_PlayoffStats"), "Its own FL21 playoff tab stays");
        }
    }

    /// <summary>The champions record's "Summer League 2022" title (Junkyard Dogs: NKOTb4evah, ACE B S1lentND1, Rage More Nerd,
    /// AtoneMilitant, AwareMosquito) is the 2022 H3 Action League's: its JUNKYARD DOGS won the bracket. Without the name it was
    /// a titles-only season of its own.</summary>
    [Test]
    public void H3Action2022_TakesTheRecordsSummerLeague2022Title()
    {
        Assert.That(Config("2022-h3-action-stats").Season!.Titles, Is.EqualTo("Summer League 2022"));
    }
}
