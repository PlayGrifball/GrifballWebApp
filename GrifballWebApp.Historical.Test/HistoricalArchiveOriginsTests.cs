using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Parsing;
using GrifballWebApp.Historical.Sources;
using static GrifballWebApp.Infrastructure.Test.HistoricalXlsx;

namespace GrifballWebApp.Historical.Test;

/// <summary>Where the builder says each record came from (<see cref="ArchiveOrigin"/>): the source, the tab, the cells as the
/// sheet names them and their text, kept for the import's provenance.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HistoricalArchiveOriginsTests
{
    private static HistoricalSourceContent Source(string id, HistoricalSourceConfig config, byte[] content, int snapshotID = 1)
        => new(HistoricalTestData.Source(id, $"https://example.org/{id}", config.ToJson()), snapshotID, content);

    private static readonly string[] PlayerRows = ["A2:D2", "A3:D3"];
    private static readonly string[] RowsPastEmptyOnes = ["A3:C3", "A5:C5"];

    private const string MatchId = "6f1c55aa-1b2c-4d3e-8f90-a1b2c3d4e5f6";

    [Test]
    public void Build_EachRecord_SaysWhichCellsItWasReadFrom()
    {
        var source = Source("sl17", new HistoricalSourceConfig
            {
                Season = new SeasonDefaults { Name = "Spring League 2017 (Pro)", Game = "halo3" },
                Tabs = [new() { Name = "Stats" }],
                Results = [new() { Sheet = "Game Results" }],
                Schedules = [new() { Sheet = "Schedule" }],
                Standings = [new() { Sheet = "Standings" }],
                Waypoint = [new() { Sheet = "Links", Season = "sl17" }],
            },
            Workbook(("Stats", [Row("Player", "Team", "Games", "Kills"), Row("Ace", "Alpha", 4, 1), Row("Bee", "Bravo", 4, 1)]),
                ("Game Results", [Row("Winner", null, "Loser"), Row("Alpha", "5-3", "Bravo")]),
                ("Schedule", [Row("Date", "6/1/2020"), Row(null, "Alpha 5-3 Bravo")]),
                ("Standings", [Row("Team", "Wins", "Losses"), Row("Alpha", 3, 1), Row("Bravo", 1, 3)]),
                ("Links", [Row("Game", "Link"), Row("1", $"https://www.halowaypoint.com/halo-infinite/players/ace/matches/{MatchId}")])),
            snapshotID: 31);

        var build = HistoricalArchiveBuilder.Build([source]);

        var season = build.Input.Seasons.Seasons.Single();
        var game = build.Input.RecordedGames!.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(season.Origin, Is.EqualTo(new ArchiveOrigin("sl17", "Stats", "A1:D1", "Player | Team | Games | Kills")), "the stats tab's header");
            Assert.That(season.Players.Select(p => p.Origin?.Cells), Is.EqualTo(PlayerRows), "each player's row");
            Assert.That(season.Players[0].Origin!.Value, Is.EqualTo("Ace | Alpha | 4 | 1"));
            Assert.That(season.Teams.Select(t => (t.TeamName, t.Origin?.Sheet, t.Origin?.Cells)),
                Is.EqualTo(new[] { ("Alpha", (string?)"Standings", (string?)"A2:C2"), ("Bravo", "Standings", "A3:C3") }), "each team's standings row");
            Assert.That((game.Origin?.Sheet, game.Origin?.Cells), Is.EqualTo(((string?)"Game Results", (string?)"A2:C2")), "the result's row");
            Assert.That((game.DatedBy?.Sheet, game.DatedBy?.Cells, game.DatedBy?.Value), Is.EqualTo(((string?)"Schedule", (string?)"B2", (string?)"Alpha 5-3 Bravo")),
                "the schedule's cell its day was taken from");
            Assert.That(build.Input.WaypointMatches!.Single().Value.Origins.Values.Select(o => (o.Sheet, o.Cells)), Is.EqualTo(new[] { ((string?)"Links", (string?)"B2") }),
                "the match's link cell");
            Assert.That(build.Input.SnapshotIDs, Is.EqualTo(new Dictionary<string, int> { ["sl17"] = 31 }), "the snapshot each source was read from");
        }
    }

    [Test]
    public void Build_ATabsEmptyRows_AreCountedInItsCells()
    {
        // The sheet's rows 2 and 4 are empty: Ace is row 3, Bee row 5
        var source = Source("wl20", new HistoricalSourceConfig
            {
                Season = new SeasonDefaults { Name = "Winter League 2020", Game = "reach" },
                Tabs = [new() { Name = "Stats" }],
            },
            Workbook(("Stats", [Row("Player", "Team", "Kills"), Row(), Row("Ace", "Alpha", 3), Row(), Row("Bee", "Bravo", 1)])));

        var players = HistoricalArchiveBuilder.Build([source]).Input.Seasons.Seasons.Single().Players;

        Assert.That(players.Select(p => p.Origin?.Cells), Is.EqualTo(RowsPastEmptyOnes));
    }
}
