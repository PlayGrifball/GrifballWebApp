using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Parsing;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Infrastructure.Test;
using static GrifballWebApp.Infrastructure.Test.HistoricalXlsx;

namespace GrifballWebApp.Historical.Test;

/// <summary>
/// LadderParser on Late League's ladder layouts (#508, the sheets' own trimmed to a few players), and the builder putting
/// each ladder, with its past finishes, on its season.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class LadderParserTests
{
    private static SheetTable Sheet(params string[][] rows) => new() { Name = "Ladder", Rows = [.. rows] };
    private static readonly LadderTab Tab = new() { Sheet = "Ladder", Season = "Late League Season 7 (Halo 3)", Game = "halo3" };
    private static readonly string[] FinishHeadings = ["Pre Season 1: Halo 5", "Season 1: Halo 5"];
    private static readonly string[] LadderSeasonKeys = ["ll/S7 Stats", "ll/S2 Ladder"];
    private static readonly string[] LadderSheetNames = ["S8 Halo 3", "past seasons"];

    [Test]
    public void Parse_TheBotsLadder_ReadsEachPlayerInOrder_WithoutSkillAndTheBotsSignups()
    {
        var sheet = Sheet(
            ["Gamertag", "Division", "League", "Skill", "LP", "Streak", "MMR", "P/ Win", "P/ Loss", "Loc", "Discord"],
            ["DlGITAL PAIN", "260", "Masters 3", "0", "56", "-2", "850", "15", "9", "East", "DigitalPain"],
            ["", "0", "Bronze 3", "0", "0", "0", "100", "0", "0", "", ""],
            ["LateLeague#4522", "0", "Placements (1/3)", "0", "0", "0", "100", "0", "0", "", ""],
            ["Bear is Clutch", "0", "Iron", "", "70", "-3", "0", "0", "3", "", ""]);

        var ladder = LadderParser.Parse(sheet, Tab)!;

        Assert.That(ladder, Is.EqualTo(new[]
        {
            new LadderStanding { Gamertag = "DlGITAL PAIN", Position = 1, Division = "260", League = "Masters 3", LeaguePoints = 56, Streak = -2, Mmr = 850, Wins = 15, Losses = 9 },
            new LadderStanding { Gamertag = "Bear is Clutch", Position = 2, Division = "0", League = "Iron", LeaguePoints = 70, Streak = -3, Mmr = 0, Wins = 0, Losses = 3 },
        }));
    }

    [Test]
    public void Parse_TheEarlyLadders_ReadTheirRank_TheNumberedLeague_AndTheErrorsAsNothing()
    {
        // Pre Season: "(1) Silver" is division 1 of Silver; Season 5: #N/A and N/A are no value
        var preSeason = Sheet(
            ["Gamertag", "League", "Rank", "LP", "W/ Streak", "P/ Win", "P/ Loss"],
            ["RageMoreNerd", "(1) Silver", "2", "2", "-2", "", ""],
            ["Ace b Silent", "(3) Placements", "", "", "", "1", "-1"]);
        var season5 = Sheet(
            ["Players", "Division", "League", "Rank", "LP", "Streak", "MMR", "P/ Win", "P/ Loss", "Loc"],
            ["Galvann", "#N/A", "Gold 3", "3", "16", "5", "400", "N/A", "N/A", "West"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(LadderParser.Parse(preSeason, Tab), Is.EqualTo(new[]
            {
                new LadderStanding { Gamertag = "RageMoreNerd", Position = 1, Division = "1", League = "Silver", Rank = 2, LeaguePoints = 2, Streak = -2 },
                new LadderStanding { Gamertag = "Ace b Silent", Position = 2, Division = "3", League = "Placements", Wins = 1 },
            }));
            Assert.That(LadderParser.Parse(season5, Tab), Is.EqualTo(new[]
            {
                new LadderStanding { Gamertag = "Galvann", Position = 1, League = "Gold 3", Rank = 3, LeaguePoints = 16, Streak = 5, Mmr = 400 },
            }));
        }
    }

    [Test]
    public void Parse_ADivisionWithoutAHeader_IsTheTabsDivisionColumn()
    {
        var sheet = Sheet(["", "", "League", "Rank"], ["", "", "", ""], ["KDSkill", "1", "Gold", "4"]);

        var ladder = LadderParser.Parse(sheet, new LadderTab { Sheet = "S1", Season = "S1", Game = "halo5", DivisionColumn = 1 });

        Assert.That(ladder, Is.EqualTo(new[] { new LadderStanding { Gamertag = "KDSkill", Position = 1, Division = "1", League = "Gold", Rank = 4 } }));
    }

    [Test]
    public void Parse_ATabWithoutALeagueHeader_IsNoLadder()
        => Assert.That(LadderParser.Parse(Sheet(["Gamertag", "LP"], ["A", "3"]), Tab), Is.Null);

    [Test]
    public void ParseFinishes_ReadsEachSeasonsGroupOfColumns_ItsPlayersWhoFinishedIt()
    {
        var sheet = Sheet(
            ["Gamertag:", "Pre Season 1: Halo 5", "", "", "Season 1: Halo 5", "", ""],
            ["", "League", "Rank", "Pos", "League", "Rank", "Pos"],
            ["Ace B S1lentND1", "Bronze", "1", "6", "Silver", "5", "12"],
            ["AJ DaJuiceMayne", "", "", "", "Bronze", "1", "23"],
            ["", "Bronze", "1", "1", "", "", ""],
            ["Pos zero", "", "", "0", "", "", ""]);

        var finishes = LadderParser.ParseFinishes(sheet)!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(finishes.Keys, Is.EquivalentTo(FinishHeadings));
            Assert.That(finishes["pre season 1: halo 5"], Is.EqualTo(new[]
            {
                new LadderStanding { Gamertag = "Ace B S1lentND1", Position = 6, League = "Bronze", Rank = 1 },
                new LadderStanding { Gamertag = "Pos zero" },
            }), "Any case; a place below 1 is none");
            Assert.That(finishes["Season 1: Halo 5"].Select(f => (f.Gamertag, f.Position)), Is.EqualTo(new[] { ("Ace B S1lentND1", (int?)12), ("AJ DaJuiceMayne", (int?)23) }));
        }
    }

    [Test]
    public void ParseFinishes_ATabWithoutAPosColumn_IsNone()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(LadderParser.ParseFinishes(Sheet(["Gamertag", "League"], ["A", "Gold"])), Is.Null);
            Assert.That(LadderParser.ParseFinishes(Sheet(["Pos"], ["1"])), Is.Null, "Pos in the first row: no headings above it");
        }
    }

    [TestCase("", false)]
    [TestCase("LateLeague#9807", false)]
    [TestCase("lateleague#1", false)]
    [TestCase("LateLeague", true)]
    [TestCase("Galvann", true)]
    public void IsPlayer_IsAGamertag_NotTheBotsSignup(string gamertag, bool expected)
        => Assert.That(LadderParser.IsPlayer(gamertag), Is.EqualTo(expected));

    private static HistoricalSourceContent Source(string id, HistoricalSourceConfig config, byte[] content)
        => new(HistoricalTestData.Source(id, $"https://example.org/{id}", config.ToJson()), 1, content);

    private static readonly object?[] Header = Row("Gamertag", "Division", "League", "Rank", "LP", "Streak", "MMR");

    [Test]
    public void Build_PutsALadderOnItsSeason_OrOnASeasonOfItsOwn_WithItsPastFinishes()
    {
        var config = new HistoricalSourceConfig
        {
            Season = new SeasonDefaults { Name = "Late League Multi-Season", League = "Late League", Game = "multi", Division = "Open", Format = "LateLeague" },
            Tabs = [new() { Name = "S7 Stats", CustomSeason = "Late League Season 7 (Halo 3)", Game = "halo3" }],
            Ladders =
            [
                new() { Sheet = "S7 Ladder", Season = "Late League Season 7 (Halo 3)", Game = "halo3" },
                new() { Sheet = "S2 Ladder", Season = "Late League Season 2 (Halo 3)", Game = "halo3", Year = 2019, Finishes = "Season 2: Halo 3" },
                new() { Sheet = "Missing", Season = "Late League Season 1 (Halo 5)", Game = "halo5", Finishes = "Nobody's" },
            ],
            FinishesSheet = "Past Seasons",
        };
        var source = Source("ll", config, Workbook(
            ("S7 Stats", [Row("Player", "Team", "Games", "Kills"), Row("Galvann", null, 4, 10)]),
            ("S7 Ladder", [Header, Row("Galvann", 400, "Masters 3", null, 0, -1, 850)]),
            ("S2 Ladder", [Header, Row("Regresser", 2, "Diamond", 3, 5, -2, 550), Row("i robot9000", 3, "Gold", 3, 7, 3, 400)]),
            ("Past Seasons", [Row("Gamertag:", "Season 2: Halo 3", null, null), Row(null, "League", "Rank", "Pos"),
                Row("I Robot9000", "Gold", 3, 2), Row("Blootzy", "Gold", 3, 3)])));

        var build = HistoricalArchiveBuilder.Build([source], new HashSet<string> { "ll" });

        var seasons = build.Input.Seasons.Seasons;
        var s7 = seasons.Single(s => s.SeasonName == "Late League Season 7 (Halo 3)");
        var s2 = seasons.Single(s => s.SeasonName == "Late League Season 2 (Halo 3)");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(seasons, Has.Count.EqualTo(2), "The missing ladder makes no season");
            Assert.That((s7.SourceSheet, s7.Players.Count), Is.EqualTo(("S7 Stats", 1)), "The stats tab's season takes its ladder");
            Assert.That(s7.Ladder, Is.EqualTo(new[] { new LadderStanding { Gamertag = "Galvann", Position = 1, Division = "400", League = "Masters 3", LeaguePoints = 0, Streak = -1, Mmr = 850 } }));
            Assert.That((s2.SourceWorkbook, s2.SourceSheet, s2.GameVersion, s2.League, s2.Format, s2.Division, s2.SourceYear, s2.Players.Count),
                Is.EqualTo(("ll", "S2 Ladder", "halo3", "Late League", "LateLeague", "Open", (int?)2019, 0)), "A season of its own, keyed by the ladder");
            Assert.That(s2.Ladder.Select(r => (r.Gamertag, r.Position, r.League, r.Rank)), Is.EqualTo(new (string, int?, string?, int?)[]
            {
                ("Regresser", 1, "Diamond", 3), ("i robot9000", 2, "Gold", 3), ("Blootzy", 3, "Gold", 3),
            }), "A finish gives its place; a player only the finishes list is added");
            Assert.That(build.Seasons, Is.EquivalentTo(LadderSeasonKeys));
            Assert.That(build.Warnings, Does.Contain("ll: no past finishes under 'Nobody's' for ladder 'Missing'"));
            Assert.That(build.Warnings, Does.Contain("ll: ladder 'Missing' not read (no such sheet, or no League header)"));
        }
    }

    [Test]
    public void Build_AMissingFinishesTab_IsReported_AndTheLadderStillRead()
    {
        var config = new HistoricalSourceConfig
        {
            Ladders = [new() { Sheet = "S2 Ladder", Season = "Late League Season 2 (Halo 3)", Game = "halo3", Finishes = "Season 2: Halo 3" }],
            FinishesSheet = "Past Seasons",
        };
        var source = Source("ll", config, Workbook(("S2 Ladder", [Header, Row("Regresser", 2, "Diamond", 3, 5, -2, 550)])));

        var build = HistoricalArchiveBuilder.Build([source]);

        var season = build.Input.Seasons.Seasons.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(season.Ladder.Select(r => (r.Gamertag, r.Position)), Is.EqualTo(new[] { ("Regresser", (int?)1) }));
            Assert.That(season.League, Is.EqualTo("late-league"), "No season defaults: Late League all the same");
            Assert.That(build.Warnings, Does.Contain("ll: past finishes 'Past Seasons' not read (no such sheet, or no Pos column)"));
        }
    }

    [Test]
    public void Config_NamesItsLadderAndFinishesTabs()
    {
        var config = new HistoricalSourceConfig { Ladders = [new() { Sheet = " S8 Halo 3 ", Season = "S8", Game = "halo3" }], FinishesSheet = "Past Seasons" };

        var names = config.SheetNames();
        Assert.That(LadderSheetNames.All(names.Contains), Is.True, "Trimmed, any case");
    }
}
