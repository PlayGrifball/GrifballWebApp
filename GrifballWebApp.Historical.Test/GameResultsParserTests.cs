using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Parsing;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Historical.Test;

/// <summary>GameResultsParser on the archive's result list layouts (the sheets' own, trimmed to a few games).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class GameResultsParserTests
{
    private static readonly string[] TableSeries = ["fl20/Game Results#2", "fl20/Game Results#2", "fl20/Game Results#5", "fl20/Game Results#8"];
    private static readonly string[] TableKeys = ["fl20/Game Results#2", "fl20/Game Results#3", "fl20/Game Results#5", "fl20/Game Results#8"];
    private static readonly bool[] ThirdIsForfeit = [false, false, true, false];
    private static readonly string[] ThreeOneSided = ["fl20/Game Results: 3 results naming one team only left out"];
    private static readonly bool[] EighthIsForfeit = [false, false, false, false, false, false, false, true, false, false, false, false];
    private static readonly string[] GameOneNotRead = ["fl20/Game Results: results not read: Tuna over Damage (Game 1)"];
    private static readonly string[] NoScoreNotRead = ["fl20/Game Results: results not read: A over B"];
    private static readonly string[] NoWinnerNotRead = ["fl20/Game History: results not read: Smurf v Mungo: FF, no winner"];
    private static readonly string[] NoHeaderNotRead = ["fl20/Game History: results not read: no Home and Away header"];
    private static readonly string[] SeriesCellKeys = ["fl20/Game Results#1:0", "fl20/Game Results#2:0", "fl20/Game Results#3:3", "fl20/Game Results#4:3", "fl20/Game Results#5:1", "fl20/Game Results#5:2"];
    private static readonly string[] HistorySeries = ["fl20/Game History#1", "fl20/Game History#4", "fl20/Game History#4", "fl20/Game History#6", "fl20/Game History#7"];

    private static SheetTable Sheet(params string[][] rows) => new() { Name = "Game Results", Rows = [.. rows] };

    private static List<RecordedGameInput> Parse(ResultsTab tab, SheetTable sheet, List<string>? warnings = null)
        => GameResultsParser.Parse("fl20", tab, sheet, warnings ?? []);

    private static (string? First, Outcomes FirstOutcome, int? FirstScore, string? Second, Outcomes SecondOutcome, int? SecondScore) Sides(RecordedGameInput game)
        => (game.Teams[0].Name, game.Teams[0].Outcome, game.Teams[0].Score, game.Teams[1].Name, game.Teams[1].Outcome, game.Teams[1].Score);

    [Test]
    public void Table_ReadsEachGamesWinnerScoreLoserHostAndVod_TheSameTwoTeamsInARowBeingASeries()
    {
        var sheet = Sheet(
            ["Week 1", "", "", "", "", "", "Total Games Played"],
            ["Winner", "", "Loser", "Host", "VOD", "Streamer"],
            ["A&tS", "5-0", "Final Boss", "Rage", "https://www.twitch.tv/videos/1", "Ace"],
            ["A&tS", "5-2", "Final Boss", "AJ"],
            ["", "", "", "", "", "", "Hosts", "11"],
            ["Final Boss", "5-4", "Team Rocket", "Alpha", "N/a"],
            ["Week 2"],
            ["Winner", "", "Loser", "Host", "VOD"],
            ["Final Boss", "4-5", "Team Rocket", "Sam", "watch https://youtu.be/x later"]);

        var games = Parse(new ResultsTab { Sheet = "Game Results" }, sheet);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(Sides), Is.EqualTo(new[]
            {
                ((string?)"A&tS", Outcomes.Won, (int?)5, (string?)"Final Boss", Outcomes.Lost, (int?)0),
                ("A&tS", Outcomes.Won, 5, "Final Boss", Outcomes.Lost, 2),
                ("Final Boss", Outcomes.Won, 5, "Team Rocket", Outcomes.Lost, 4),
                ("Final Boss", Outcomes.Won, 5, "Team Rocket", Outcomes.Lost, 4),
            }), "The winner's score is the higher, however the cell writes it; a team named Final Boss is no playoffs marker");
            Assert.That(games.Select(g => g.Series), Is.EqualTo(TableSeries),
                "A host count row between is no other series; a new week is");
            Assert.That(games.Select(g => g.SourceKey), Is.EqualTo(TableKeys));
            Assert.That(games.Select(g => (g.Host, g.Vod)), Is.EqualTo(new[]
            {
                ((string?)"Rage", (string?)"https://www.twitch.tv/videos/1"), ("AJ", null), ("Alpha", null), ("Sam", "https://youtu.be/x"),
            }), "A VOD is a link: N/a is none");
            Assert.That(games.Select(g => (g.IsPlayoffs, g.IsForfeit, g.GameVersion, g.SeasonSourceId)), Is.All.EqualTo((false, false, "", "")),
                "The season and its game are the builder's to set");
        }
    }

    [Test]
    public void Table_APlayoffsRow_MakesTheGamesBelowPlayoffGames_AndAHeaderAgainKeepsTheColumnsItDoesNotName()
    {
        var sheet = Sheet(
            ["Week 1"],
            ["Winner", "", "Loser", "Host", "Game", "VOD", "Streamer"],
            ["Natural Wonders", "5-2", "Nets Dimension", "Doer", "H4"],
            ["Playoffs"],
            ["Winner", "", "Loser", "Host", "", "VOD"],
            ["Prodigies", "", "Natural Wonders", "West", "Reach", "https://clips.twitch.tv/x"],
            ["Prodigies", "FF", "Natural Wonders"],
            ["Prodigies", "5-5", "Natural Wonders", "", "Halo 3"]);

        var games = Parse(new ResultsTab { Sheet = "Game Results", Game = "halo3" }, sheet);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(g => (g.IsPlayoffs, g.GameVersion, g.Vod)), Is.EqualTo(new[]
            {
                (false, "halo4", (string?)null), (true, "reach", "https://clips.twitch.tv/x"), (true, "halo3", null), (true, "halo3", null),
            }), "The game of the row, else the tab's");
            Assert.That(games.Skip(1).Select(Sides), Is.EqualTo(new[]
            {
                ((string?)"Prodigies", Outcomes.Won, (int?)null, (string?)"Natural Wonders", Outcomes.Lost, (int?)null),
                ("Prodigies", Outcomes.Won, null, "Natural Wonders", Outcomes.Lost, null),
                ("Prodigies", Outcomes.Tie, 5, "Natural Wonders", Outcomes.Tie, 5),
            }), "No score still names the winner");
            Assert.That(games.Select(g => g.IsForfeit), Is.EqualTo(ThirdIsForfeit));
        }
    }

    [Test]
    public void Table_AResultNamingOneTeam_IsLeftOutAndReported_AndRowsBeforeAHeaderAreNoGames()
    {
        var warnings = new List<string>();
        var sheet = Sheet(
            ["Zaps MD", "5-0", "The Boys"],
            ["Winner", "", "Loser"],
            ["", "5-1", "Limitless"],
            ["Limitless", "5-1", ""],
            ["TBD", "5-1", "Limitless"]);

        var games = Parse(new ResultsTab { Sheet = "Game Results" }, sheet, warnings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games, Is.Empty);
            Assert.That(warnings, Is.EqualTo(ThreeOneSided));
        }
    }

    [Test]
    public void Series_ReadsEachLayoutOfASeriesCell()
    {
        var warnings = new List<string>();
        var sheet = Sheet(
            ["Week 1"],
            ["Slytherin over Kill The Carrier 2-0 (5-1, 5-4)", "", "", "", "", "Projected Playoff Bracket"],
            ["E-Dogs tied Redemption Runners (5-3, 5-3)"],
            ["Fortnight 1", "", "", "Notorious 2-1 Ghosts Of Grif", "5-4", "2-5", "5-2", "Team 1:"],
            ["", "", "", "Xbox Record That 2-0 Ghosts Of Grif", "FF", "5-1"],
            ["Playoffs", "Mon over Junk 5-3", "Hype over Mon 2-0", "Tuna over Damage (Game 1)"]);

        var games = Parse(new ResultsTab { Sheet = "Game Results", Layout = ResultsLayout.Series }, sheet, warnings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(Sides), Is.EqualTo(new[]
            {
                ((string?)"Slytherin", Outcomes.Won, (int?)5, (string?)"Kill The Carrier", Outcomes.Lost, (int?)1),
                ("Slytherin", Outcomes.Won, 5, "Kill The Carrier", Outcomes.Lost, 4),
                ("E-Dogs", Outcomes.Won, 5, "Redemption Runners", Outcomes.Lost, 3),
                ("E-Dogs", Outcomes.Lost, 3, "Redemption Runners", Outcomes.Won, 5),
                ("Notorious", Outcomes.Won, 5, "Ghosts Of Grif", Outcomes.Lost, 4),
                ("Notorious", Outcomes.Lost, 2, "Ghosts Of Grif", Outcomes.Won, 5),
                ("Notorious", Outcomes.Won, 5, "Ghosts Of Grif", Outcomes.Lost, 2),
                ("Xbox Record That", Outcomes.Won, null, "Ghosts Of Grif", Outcomes.Lost, null),
                ("Xbox Record That", Outcomes.Won, 5, "Ghosts Of Grif", Outcomes.Lost, 1),
                ("Mon", Outcomes.Won, 5, "Junk", Outcomes.Lost, 3),
                ("Hype", Outcomes.Won, null, "Mon", Outcomes.Lost, null),
                ("Hype", Outcomes.Won, null, "Mon", Outcomes.Lost, null),
            }), "A tie's game scores written winner first split the series; FF is a forfeit the series score gives a winner; 5-3 is a game, 2-0 a series");
            Assert.That(games.Select(g => g.IsForfeit), Is.EqualTo(EighthIsForfeit));
            Assert.That(games.Select(g => g.Series).Distinct(), Is.EqualTo(SeriesCellKeys));
            Assert.That(games[0].SourceKey, Is.EqualTo("fl20/Game Results#1:0/1"));
            Assert.That(games.Select(g => g.IsPlayoffs), Is.EqualTo(Enumerable.Repeat(false, 9).Concat(Enumerable.Repeat(true, 3))));
            Assert.That(warnings, Is.EqualTo(GameOneNotRead));
        }
    }

    [Test]
    public void Series_GameScoresThatDoNotAddUpToTheSeriesScore_AreTakenAsWrittenWinnerFirst()
    {
        var games = Parse(new ResultsTab { Sheet = "Game Results", Layout = ResultsLayout.Series }, Sheet(["A over B 2-1 (5-1, 5-4, 5-3)"]));

        Assert.That(games.Select(Sides), Is.EqualTo(new[]
        {
            ((string?)"A", Outcomes.Won, (int?)5, (string?)"B", Outcomes.Lost, (int?)1),
            ("A", Outcomes.Won, 5, "B", Outcomes.Lost, 4),
            ("A", Outcomes.Lost, 3, "B", Outcomes.Won, 5),
        }));
    }

    [Test]
    public void Series_ACellWithNoScore_IsReported()
    {
        var warnings = new List<string>();

        var games = Parse(new ResultsTab { Sheet = "Game Results", Layout = ResultsLayout.Series }, Sheet(["A over B"]), warnings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games, Is.Empty);
            Assert.That(warnings, Is.EqualTo(NoScoreNotRead));
        }
    }

    [Test]
    public void Series_ForfeitsWithoutASeriesScore_GoToTheTeamNamedFirst()
    {
        var games = Parse(new ResultsTab { Sheet = "Game Results", Layout = ResultsLayout.Series }, Sheet(["A over B (FF, 5-1)"]));

        Assert.That(games.Select(g => (g.Teams[0].Outcome, g.IsForfeit)), Is.EqualTo(new[] { (Outcomes.Won, true), (Outcomes.Won, false) }));
    }

    private static readonly string[] HistoryHeader = ["", "Home", "Away", "Score", "Time", "Waypoint URL", "", "VOD"];

    [Test]
    public void GameHistory_ReadsTheRowsWithoutAMatchLink_ForfeitsAndGamesPlayedWithoutOne()
    {
        var warnings = new List<string>();
        var sheet = Sheet(
            HistoryHeader,
            ["Week 1", "Deathcrawller", "Oreo", "5-0", "0:00", "FF"],
            ["", "Holy Morals", "Amnatu", "5-1", "05:33", "https://www.halowaypoint.com/halo-infinite/players/x/matches/1fde4c2a-7935-4fb0-9706-e226f4d13683"],
            ["", "Amnatu", "Holy Morals", "1-5", "17:50", "https://www.halowaypoint.com/halo-infinite/players/x/matches/2fde4c2a-7935-4fb0-9706-e226f4d13683"],
            ["", "Squirel", "nkot", "FF (5-0)"],
            ["", "nkot", "Squirel", "FF (5-0)"],
            ["", "Avenger", "Ducky", "", "FF Ducky Win"],
            ["", "Deathcrawller", "SPANKATRON", "5-4", "16:01", "343 Deleted History", "", "https://youtu.be/v"],
            ["", "Smurf", "Mungo", "", "", "Needs to be played"],
            ["Playoffs", "Smurf", "Mungo", "", "FF"],
            ["", "TBD", "Mungo", "", "FF"]);

        var games = Parse(new ResultsTab { Sheet = "Game History", Layout = ResultsLayout.GameHistory }, sheet, warnings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(Sides), Is.EqualTo(new[]
            {
                ((string?)"Deathcrawller", Outcomes.Won, (int?)5, (string?)"Oreo", Outcomes.Lost, (int?)0),
                ("Squirel", Outcomes.Won, 5, "nkot", Outcomes.Lost, 0),
                ("nkot", Outcomes.Won, 5, "Squirel", Outcomes.Lost, 0),
                ("Avenger", Outcomes.Lost, null, "Ducky", Outcomes.Won, null),
                ("Deathcrawller", Outcomes.Won, 5, "SPANKATRON", Outcomes.Lost, 4),
            }));
            Assert.That(games.Select(g => (g.IsForfeit, g.Vod)), Is.EqualTo(new[] { (true, (string?)null), (true, null), (true, null), (true, null), (false, "https://youtu.be/v") }));
            Assert.That(games.Select(g => g.Series), Is.EqualTo(HistorySeries), "A Waypoint game between ends a series; the same two teams' rows in a row are one");
            Assert.That(warnings, Is.EqualTo(NoWinnerNotRead));
        }
    }

    [Test]
    public void GameHistory_WithoutAHomeAndAwayHeader_IsReported()
    {
        var warnings = new List<string>();

        var games = Parse(new ResultsTab { Sheet = "Game History", Layout = ResultsLayout.GameHistory }, Sheet(["Team", "Score"], ["A", "5-0"]), warnings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games, Is.Empty);
            Assert.That(warnings, Is.EqualTo(NoHeaderNotRead));
        }
    }

    [Test]
    public void GameHistory_AllPlayoffs_MakesEveryGameAPlayoffGame()
    {
        var games = Parse(new ResultsTab { Sheet = "Game History", Layout = ResultsLayout.GameHistory, IsPlayoffs = true },
            Sheet(HistoryHeader, ["Week 1", "A", "B", "5-0", "", "FF"]));

        Assert.That(games.Single().IsPlayoffs, Is.True);
    }

    [TestCase("G&tC", "Galvin & the Chipmunks", true)]
    [TestCase("A&tS", "A and the Spences", true)]
    [TestCase("MAB", "Mediocre At Best", true)]
    [TestCase("ApoPo", "Apocalypse Ponies", true)]
    [TestCase("ZMD", "Zaps MD", true)]
    [TestCase("TB", "The Boys", true)]
    [TestCase("DBAGS", "Don't Be A Gonk Scet", true)]
    [TestCase("AJYD", "AJunkyardDog", false)]
    [TestCase("Team", "Team Rocket", false)]
    [TestCase("Asc", "Ascended", false)]
    [TestCase("Team U.S.A.", "Team Rocket", false)]
    [TestCase("Team Rocket", "Team U.S.A.", false)]
    public void Abbreviates(string abbreviation, string name, bool abbreviates)
        => Assert.That(HistoricalArchiveBuilder.Abbreviates(abbreviation, name), Is.EqualTo(abbreviates));
}
