using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Infrastructure.Historical.Parsing;
using static GrifballWebApp.Infrastructure.Test.HistoricalXlsx;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// How the archive's game sheets mark a game's phase, case by case: a block's title ("Week 3 - A over B (5-1)
/// 10/12/21"), with its date and whether it's a playoff game; a carnage sheet's marker rows ("Week 2", "Semifinals");
/// and a draft sheet's row labels (team name, captain, players by number or round). One small workbook each.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HistoricalPhaseCasesTests
{
    private static readonly object?[] BlockHeader = Row("Team", "Player", "Games Played", "Wins", "Minutes", "Goals", "Kills", "Assists", "Deaths", "Carrier Kills");
    private static readonly object?[] CarnageHeader = Row("Player", "Team", "GamesPlayed", "Wins", "Loses", "Goals", "Kills", "Assists", "Deaths", "TimeStamp");

    private static SheetTable Sheet(string name, params object?[][] rows) => SheetWorkbook.Read(Workbook((name, rows)))!.Sheet(name)!;

    private static RecordedGameInput BlockGame(string title)
    {
        var workbook = SheetWorkbook.Read(Workbook(("Game Results", [
            Row(title), BlockHeader,
            Row("Tuna", "imShad", 1, 1, 10, 2, 10, 0, 10, 0),
            Row("Damage", "godwit1ivy", 1, 0, 10, 1, 10, 0, 10, 0)])))!;
        return RecordedGameParser.Parse("wl", new GamesTab { Sheet = "Game Results", Season = "wl/Stats", Game = "halo3", Layout = GamesLayout.Blocks }, workbook, []).Single();
    }

    [TestCase("Week 3 - Tuna over Damage (5-1) 10/12/21", false, 2021, 10, 12)]
    [TestCase("Week 10 - 12/5/2021 - Tuna over Damage", false, 2021, 12, 5)]
    [TestCase("Round 2 - Tuna tied Damage 2-2", false, null, null, null)]
    [TestCase("Grand Finals - Tuna over Damage 5-4 3/7/2020", true, 2020, 3, 7)]
    [TestCase("Semifinal - Tuna over Damage", true, null, null, null)]
    [TestCase("Quarterfinals Game 2 - Tuna over Damage (3-0)", true, null, null, null)]
    [TestCase("Championship - Tuna over Damage", true, null, null, null)]
    [TestCase("Playoff Round 1 - Tuna over Damage", true, null, null, null)]
    [TestCase("Final - 1/2/22 - Tuna over Damage (2-1)", true, 2022, 1, 2)]
    public void BlockTitle_GivesTheGamesPhaseAndDate(string title, bool playoffs, int? year, int? month, int? day)
    {
        var game = BlockGame(title);

        Assert.That((game.IsPlayoffs, game.PlayedAt),
            Is.EqualTo((playoffs, year is null ? (DateTime?)null : new DateTime(year.Value, month!.Value, day!.Value, 0, 0, 0, DateTimeKind.Utc))));
    }

    [TestCase("Tuna over Damage (5-1)", 5, 1)]
    [TestCase("Week 1 - Tuna over Damage 12-11", 12, 11)]
    [TestCase("Week 1 - Tuna over Damage", null, null)]
    public void BlockTitle_GivesTheWinnersAndLosersScore(string title, int? winner, int? loser)
    {
        var games = RecordedGameParser.Parse("wl", new GamesTab { Sheet = "G", Season = "wl/Stats", Game = "halo3", Layout = GamesLayout.Blocks },
            SheetWorkbook.Read(Workbook(("G", [Row("Template"), Row(title), BlockHeader, Row("Tuna", "a", 1, 1, 10, 2, 10, 0, 10, 0), Row("Damage", "b", 1, 0, 10, 1, 10, 0, 10, 0)])))!, []);

        // A title without a phase before its dash is no block title
        Assert.That(games.SelectMany(g => g.Teams).Select(t => t.Score), Is.EqualTo(title.Contains(" - ") ? new[] { winner, loser } : []));
    }

    private static bool CarnagePhase(string marker, string before = "Week 1")
    {
        var workbook = SheetWorkbook.Read(Workbook(("History", [
            CarnageHeader,
            Row(null, before),
            Row("A", 1, 1, 1, 0, 1, 5, 0, 1, "Mon Jul 11 18:02:15 2022"), Row("B", 0, 1, 0, 1, 0, 4, 0, 2, "Mon Jul 11 18:02:15 2022"),
            Row(marker, 0, 0, 0, 0, 0, 0),
            Row("A", 1, 1, 1, 0, 1, 5, 0, 1, "Tue Jul 12 18:02:15 2022"), Row("B", 0, 1, 0, 1, 0, 4, 0, 2, "Tue Jul 12 18:02:15 2022")])))!;
        var games = RecordedGameParser.Parse("h", new GamesTab { Sheet = "History", Season = "h/Stats", Game = "halo3" }, workbook, []);
        Assert.That(games, Has.Count.EqualTo(2));
        return games[1].IsPlayoffs;
    }

    [TestCase("Playoffs", true)]
    [TestCase("Playoff Round 1", true)]
    [TestCase("Semifinals", true)]
    [TestCase("Quarterfinal", true)]
    [TestCase("Final", true)]
    [TestCase("Championship", true)]
    [TestCase("Week 2", false)]
    [TestCase("Weel 2", false)]
    [TestCase("Round 3", false)]
    public void CarnageMarker_SetsThePhaseOfTheGamesAfterIt(string marker, bool playoffs)
        => Assert.That(CarnagePhase(marker), Is.EqualTo(playoffs));

    [TestCase("Week 1", false)]
    [TestCase("Playoffs", true)]
    public void CarnageMarker_ThatMarksNoPhase_KeepsThePhaseBeforeIt(string before, bool playoffs)
        => Assert.That(CarnagePhase("Notes", before), Is.EqualTo(playoffs));

    [Test]
    public void DraftSheet_LabelsInTheirSpellings_GiveEachPlayerTheirTeam()
    {
        var sheet = Sheet("Draft",
            Row("TEAMNAME", "Bears", "Beer"),
            Row(" captain : ", "Cap A", "Cap B"),
            Row("Player 2:", "P2 A", "P2 B"),
            Row("player3", "P3 A", "P3 B"),
            Row("2nd Round", "R2 A", "R2 B"),
            Row("3rd round:", "R3 A", "R3 B"),
            Row("4th Round", "R4 A", null),
            Row("21st round", "R21 A", null),
            Row("Coach", "Not A Player", "Nobody"),
            Row("Captains", "Not Either", "Nope"));

        var teams = RosterParser.Parse(sheet);

        Assert.That(teams, Is.EquivalentTo(new Dictionary<string, string>
        {
            [HistoricalKeys.Normalize("Cap A")] = "Bears", [HistoricalKeys.Normalize("Cap B")] = "Beer",
            [HistoricalKeys.Normalize("P2 A")] = "Bears", [HistoricalKeys.Normalize("P2 B")] = "Beer",
            [HistoricalKeys.Normalize("P3 A")] = "Bears", [HistoricalKeys.Normalize("P3 B")] = "Beer",
            [HistoricalKeys.Normalize("R2 A")] = "Bears", [HistoricalKeys.Normalize("R2 B")] = "Beer",
            [HistoricalKeys.Normalize("R3 A")] = "Bears", [HistoricalKeys.Normalize("R3 B")] = "Beer",
            [HistoricalKeys.Normalize("R4 A")] = "Bears", [HistoricalKeys.Normalize("R21 A")] = "Bears",
        }));
    }

    [Test]
    public void DraftSheet_MoreLabelSpellings_AndRowsThatNameNoPlayers()
    {
        var sheet = Sheet("Draft",
            Row("TEAM NAME :", "Wolves"),
            Row("Player 12 :", "P12"),
            Row("1st round", "R1"),
            Row("22nd Round:", "R22"),
            Row("5 th round", "R5"),
            Row("Player", "Not Numbered"),
            Row("Round 3", "Wrong Way Round"),
            Row("Captain Notes", "Not A Captain"));

        var teams = RosterParser.Parse(sheet);

        Assert.That(teams.Keys, Is.EquivalentTo(NumberedPlayers.Select(HistoricalKeys.Normalize)));
    }

    [Test]
    public void DraftSheet_RowsBeforeAnyTeamNameLabel_AreNoTeams()
    {
        var sheet = Sheet("Draft",
            Row("Captain", "Too Early"),
            Row("team name", "Bears"),
            Row("Captain", "In Time"));

        Assert.That(RosterParser.Parse(sheet), Is.EquivalentTo(new Dictionary<string, string> { [HistoricalKeys.Normalize("In Time")] = "Bears" }));
    }

    private static readonly string[] NumberedPlayers = ["P12", "R1", "R22", "R5"];
    private static readonly string[] OnlyThePlayers = ["Galvann", "Invayda Zim"];

    [Test]
    public void StatsTab_TotalsAndAveragesRows_AreNoPlayers()
    {
        var config = new HistoricalSourceConfig { Season = new SeasonDefaults { Name = "S" }, Tabs = [new() { Name = "Stats" }] };
        var workbook = SheetWorkbook.Read(Workbook(("Stats", [
            Row("Player", "Kills"), Row("Galvann", 50), Row("TOTAL", 90), Row("Invayda Zim", 40), Row("Team Average", 45), Row("subtotal (week 1)", 10)])))!;

        var season = SeasonStatsParser.Parse("s", config, workbook, []).Single();

        Assert.That(season.Players.Select(p => p.Gamertag), Is.EqualTo(OnlyThePlayers));
    }

    private const string Link = "https://www.halowaypoint.com/halo-infinite/players/x/matches/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

    /// <summary>Whether a Waypoint sheet's game below a row starting <paramref name="marker"/> is a playoff game.</summary>
    private static bool WaypointPlayoffs(string marker)
    {
        var (regular, playoffs, _) = WaypointMatchParser.Parse(Sheet("Games", Row(marker), Row(null, Link)), []);
        Assert.That(regular.Count + playoffs.Count, Is.EqualTo(1), "the one game, on one side or the other");
        return playoffs.Count == 1;
    }

    [TestCase("Playoffs", true)]
    [TestCase("PLAYOFF GAMES", true)]
    [TestCase("Grand Final", true)]
    [TestCase("Semis", true)]
    [TestCase("Quarterfinals", true)]
    [TestCase("Winners Bracket", true)]
    [TestCase("Week 4", false)]
    [TestCase("Regular season", false)]
    [TestCase("Round Robin", false)]
    public void WaypointMarker_PutsTheGamesBelowItInThePhase(string marker, bool playoffs)
        => Assert.That(WaypointPlayoffs(marker), Is.EqualTo(playoffs));
}
