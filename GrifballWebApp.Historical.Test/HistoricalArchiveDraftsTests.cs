using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Parsing;
using GrifballWebApp.Historical.Sources;
using static GrifballWebApp.Infrastructure.Test.HistoricalXlsx;

namespace GrifballWebApp.Historical.Test;

/// <summary>The archive's builder putting the draft tabs (DraftParser) on their seasons, against small workbooks built here.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HistoricalArchiveDraftsTests
{
    private static readonly string[] StatsAndPlayoffStats = ["stats/Stats", "stats/Playoff Stats"];
    private static readonly string[] DraftsNotRead =
    [
        "schedule: draft 'No Captains' not read (no such sheet, or no board or table)",
        "schedule: draft 'Board' not read (no season from nothing)",
        "other: draft 'Missing' not read (no such sheet, or no board or table)",
    ];

    private static HistoricalSourceContent Source(string id, HistoricalSourceConfig config, byte[] content)
        => new(HistoricalTestData.Source(id, $"https://example.org/{id}", config.ToJson()), 1, content);

    private static readonly object?[] StatsHeader = Row("Player", "Team", "Games", "Kills");

    /// <summary>A season's stats workbook ("stats": its Stats and Playoff Stats tabs, its Alpha, Bravo and Charlie) and a
    /// schedule workbook ("schedule") with the given draft tabs of the season, each sheet named as its tab.</summary>
    private static List<HistoricalSourceContent> Season(params (DraftTab Tab, object?[][] Rows)[] drafts) =>
    [
        Source("stats", new HistoricalSourceConfig
            {
                Season = new SeasonDefaults { Name = "Winter League 2023", Game = "infinite" },
                Tabs = [new() { Name = "Stats" }, new() { Name = "Playoff Stats", IsPlayoffs = true }],
            },
            Workbook(("Stats", [StatsHeader, Row("Ace", "Alpha", 4, 1), Row("Dee", "Alpha", 4, 1), Row("Bee", "Bravo", 4, 1), Row("NecessaryEvil10", "Bravo", 4, 1),
                    Row("Mr_Epic_Beast", "Bravo", 4, 1), Row("Cee", "Charlie", 4, 1), Row("OREOTHECAT#5618", "Charlie", 4, 1)]),
                ("Playoff Stats", [StatsHeader, Row("Ace", "Alpha", 2, 1)]))),
        Source("schedule", new HistoricalSourceConfig { Drafts = [.. drafts.Select(d => d.Tab)] },
            Workbook([.. drafts.Select(d => (d.Tab.Sheet, d.Rows)).DistinctBy(d => d.Sheet)])),
    ];

    private static ArchiveSeason Regular(HistoricalArchiveBuild build) => build.Input.Seasons.Seasons.Single(s => !s.IsPlayoffs);

    [Test]
    public void ADraftBoard_GoesOnItsSeasonsRegularSeason_AsTheSeasonNamesItsTeamsAndPlayers()
    {
        var build = HistoricalArchiveBuilder.Build(Season((new DraftTab { Sheet = "Draft Sheet", Season = "stats" },
        [
            Row("Team Name:", "alpha!", "", "Charlie (Season Winner)", "Notes"),
            Row("Captain:", "ace", "NecessaryEvil", "Cee", "48"),
            Row("1st Round:", "Dee", "Mr Epic Beast", "OREOTHECAT", "Psyduck"),
            Row("2nd Round:", "Psyduck", "", "", ""),
        ])), new HashSet<string> { "schedule" });

        var regular = Regular(build);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(regular.Teams.Select(t => (t.TeamName, t.Captain, t.DraftOrder)), Is.EqualTo(new (string, string?, int?)[]
            {
                ("Alpha", "Ace", 1), ("Bravo", "NecessaryEvil10", 2), ("Charlie", "Cee", 3),
            }), "The stats' names: a few letters off, the name with no note, the unnamed team its captain's; a column of no team the season has is none");
            Assert.That(regular.Teams.SelectMany(t => t.Picks.Select(p => (t.TeamName, p.Gamertag, p.Round))), Is.EqualTo(new[]
            {
                ("Alpha", "Dee", 1), ("Bravo", "Mr_Epic_Beast", 1), ("Charlie", "OREOTHECAT#5618", 1),
            }), "Each pick as the stats spell the player; a nickname no player has is left out");
            Assert.That(build.Input.Seasons.Seasons.Single(s => s.IsPlayoffs).Teams, Is.Empty);
            Assert.That(build.Seasons, Is.EquivalentTo(StatsAndPlayoffStats), "The draft's season is the changed source's");
        }
    }

    [Test]
    public void ADraftTabNotRead_IsAWarning()
    {
        object?[][] board = [Row("Team Name:", "Alpha"), Row("Captain:", "Ace")];
        var sources = Season((new DraftTab { Sheet = "No Captains", Season = "stats" }, [Row("Team Name:", "Alpha"), Row("1st Round:", "Dee")]),
            (new DraftTab { Sheet = "Board", Season = "nothing" }, board));
        sources.Add(Source("other", new HistoricalSourceConfig { Drafts = [new() { Sheet = "Missing", Season = "stats" }] }, Workbook(("Board", board))));

        var build = HistoricalArchiveBuilder.Build(sources);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(build.Warnings, Is.EquivalentTo(DraftsNotRead));
            Assert.That(Regular(build).Teams, Is.Empty);
        }
    }

    [Test]
    public void ADraftTableInTheStatsWorkbook_GoesOnItsOwnSeason()
    {
        List<HistoricalSourceContent> sources =
        [
            Source("fl24", new HistoricalSourceConfig
                {
                    Season = new SeasonDefaults { Name = "Fall League 2024", Game = "infinite" },
                    Tabs = [new() { Name = "Stats" }],
                    Drafts = [new() { Sheet = "TeamInfo", Layout = DraftLayout.Table }],
                },
                Workbook(("Stats", [StatsHeader, Row("Ace", "Alpha", 4, 1), Row("Dee", "Alpha", 4, 1)]),
                    ("TeamInfo", [Row("Team Name", "Player Name", "Draft Pick"), Row("Alpha", "Ace", "C2"), Row("Alpha", "Dee", "R1P2")]))),
        ];

        var team = HistoricalArchiveBuilder.Build(sources).Input.Seasons.Seasons.Single().Teams.Single();

        Assert.That((team.TeamName, team.Captain, team.DraftOrder, team.Picks.Single()), Is.EqualTo(("Alpha", (string?)"Ace", (int?)2, new ArchiveDraftPick("Dee", 1))));
    }

    [TestCase("Ace", "Ace")]
    [TestCase("ACE ", "Ace")]
    [TestCase("Mr Epic Beast", "Mr_Epic_Beast")]
    [TestCase("OREOTHECAT", "OREOTHECAT#5618")]
    [TestCase("NecessaryEvil", "NecessaryEvil10")]
    [TestCase("Solace VII", "Solace VIII")]
    [TestCase("Bea", null)]
    [TestCase("Psyduck", null)]
    [TestCase("Cosmos5", null)]
    [TestCase("", null)]
    [TestCase(null, null)]
    public void SeasonPlayer_IsThePlayerTheCellNames_OnlyWhenOneIsThatClose(string? cell, string? player)
    {
        // "Bea": a short name a letter off is as likely another's; "Cosmos": as close to two players
        List<string> players = ["Ace", "Bee", "Mr_Epic_Beast", "OREOTHECAT#5618", "NecessaryEvil10", "Solace VIII", "Cosmos50", "Cosmos51"];

        Assert.That(HistoricalArchiveBuilder.SeasonPlayer(cell, players), Is.EqualTo(player));
    }

    [Test]
    public void SeasonPlayer_TwoPlayersOfOneLooseName_IsNeither()
    {
        Assert.That(HistoricalArchiveBuilder.SeasonPlayer("Mr Epic Beast", ["Mr_Epic_Beast", "Mr-Epic-Beast"]), Is.Null);
    }
}
