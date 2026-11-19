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
    private static readonly string[] BravoAlphaThenCharlie = ["fl20/Game Results#2", "fl20/Game Results#2", "fl20/Game Results#5"];
    private static readonly string[] ZedNotRead = ["fl20/Game History: results not read: Alpha v Charlie: FF Zed Win, no winner"];
    private static readonly int[] SixFiveOne = [6, 5, 1];
    private static readonly bool[] RegularThenPlayoffs = [false, true, true];
    private static readonly string[] TableSeries = ["fl20/Game Results#2", "fl20/Game Results#2", "fl20/Game Results#5", "fl20/Game Results#8"];
    private static readonly string[] TableKeys = ["fl20/Game Results#2", "fl20/Game Results#3", "fl20/Game Results#5", "fl20/Game Results#8"];
    private static readonly bool[] ThirdIsForfeit = [false, false, true, false];
    private static readonly string[] ThreeOneSided = ["fl20/Game Results: 3 results naming one team only left out"];
    private static readonly bool[] EighthIsForfeit = [false, false, false, false, false, false, false, true, false, false, false, false];
    private static readonly string[] GameOneNotRead = ["fl20/Game Results: results not read: Tuna over Damage (Game 1)"];
    private static readonly string[] NoScoreNotRead = ["fl20/Game Results: results not read: A over B"];
    private static readonly string[] NoWinnerNotRead = ["fl20/Game History: results not read: Smurf v Mungo: FF, no winner"];
    private static readonly string[] NoHeaderNotRead = ["fl20/Game History: results not read: no Home and Away header"];
    private static readonly bool[] SideBySidePlayoffs = [false, false, false, true, true, true];
    private static readonly Outcomes[] WonThenLost = [Outcomes.Won, Outcomes.Lost];
    private static readonly Outcomes[] LostThenWon = [Outcomes.Lost, Outcomes.Won];
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
    public void Table_ANamedScoreColumn_LoserBesideTheWinner_AndAPlayoffsTab()
    {
        var sheet = Sheet(
            ["Winner", "Loser", "Score", "Map", "Link"],
            ["Week 1"],
            ["Bravo", "Alpha", "5 - 3", "Foundry", "https://x.test/1"],
            ["Alpha", "Bravo", "3-5", "Pit"],
            ["Winner", "Loser"],
            ["Alpha", "Charlie", "", "Narrows"]);

        var games = Parse(new ResultsTab { Sheet = "Game Results", IsPlayoffs = true }, sheet);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(g => (g.Teams[0].Name, g.Teams[0].Score, g.Teams[1].Score, g.Map, g.Vod, g.IsPlayoffs)), Is.EqualTo(new[]
            {
                ((string?)"Bravo", (int?)5, (int?)3, (string?)"Foundry", (string?)"https://x.test/1", true),
                ("Alpha", 5, 3, "Pit", null, true),
                ("Alpha", null, null, "Narrows", null, true),
            }), "A header without a score column has none beside the winner when the loser is there; a week of a playoffs tab is the playoffs");
            Assert.That(games.Select(g => g.Series), Is.EqualTo(BravoAlphaThenCharlie), "The same two teams either way round are one series");
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
    public void Series_ScoresWithoutGames_AreAGameOrTheSeriesGames_AndACellNamingNoTeamIsNone()
    {
        var games = Parse(new ResultsTab { Sheet = "Game Results", Layout = ResultsLayout.Series },
            Sheet(["TBD over Mon 5-3", "TBD 2-0 Mon", "Alpha over Bravo 3-5", "Alpha over Bravo 5-5", "Alpha over Bravo 1-2"],
                ["Charlie 1-1 Delta", "FF", "5-0"], ["Echo over Foxtrot 1-1 (5-1, 5-4, FF)"]));

        Assert.That(games.Select(g => (g.Teams[0].Outcome, g.Teams[0].Score, g.Teams[1].Score)), Is.EqualTo(new[]
        {
            (Outcomes.Lost, (int?)3, (int?)5), (Outcomes.Tie, 5, 5), (Outcomes.Won, null, null), (Outcomes.Lost, null, null), (Outcomes.Lost, null, null),
            (Outcomes.Lost, null, null), (Outcomes.Won, 5, 0),
            (Outcomes.Won, 5, 1), (Outcomes.Lost, 4, 5), (Outcomes.Lost, null, null),
        }), "A forfeit is the series score's win left, B's when A has its wins; scores that don't add up are winner first, a forfeit too");
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
    public void GameHistory_ATie_AForfeitNamingTheHomeSide_AndOneNamingNeither()
    {
        var warnings = new List<string>();
        var sheet = Sheet(
            ["", "Home Team", "Away Team", "Score"],
            ["Week 1", "Alpha", "Bravo", "5-5"],
            ["", "Alpha", "Bravo", "", "FF Alpha Win"],
            ["", "Alpha", "Bravo", "", "https://www.halowaypoint.com/halo-infinite/players/x"],
            ["", "Alpha", "Charlie", "", "FF Zed Win"]);

        var games = Parse(new ResultsTab { Sheet = "Game History", Layout = ResultsLayout.GameHistory }, sheet, warnings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(g => (g.Teams[0].Outcome, g.Teams[1].Outcome, g.IsForfeit, g.Vod)), Is.EqualTo(new[]
            {
                (Outcomes.Tie, Outcomes.Tie, false, (string?)null), (Outcomes.Won, Outcomes.Lost, true, null),
            }));
            Assert.That(warnings, Is.EqualTo(ZedNotRead));
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

    [Test]
    public void Table_ABracketRoundMarksThePlayoffs_AndADoubleForfeitHasNoWinner()
    {
        var warnings = new List<string>();
        var games = Parse(new ResultsTab { Sheet = "Game Results" }, Sheet(
            ["Winner", "", "Loser"],
            ["Losers Round 1"],
            ["Alpha", "Dbl FF", "Bravo"],
            ["Round 2"],
            ["Alpha", "5-1", "Bravo"]), warnings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(g => (g.IsPlayoffs, g.IsForfeit, g.Teams[0].Outcome, g.Teams[1].Outcome)), Is.EqualTo(new[]
            {
                (true, true, Outcomes.Lost, Outcomes.Lost), (false, false, Outcomes.Won, Outcomes.Lost),
            }));
            Assert.That(warnings, Is.Empty, "A round is no result naming one team");
            Assert.That(games[0].Series, Is.Not.EqualTo(games[1].Series));
        }
    }

    [Test]
    public void Series_AScoreOfASeriesUpToFourWins_IsItsGames_FiveIsAGame()
    {
        var games = Parse(new ResultsTab { Sheet = "Game Results", Layout = ResultsLayout.Series }, Sheet(["Alpha 4-2 Bravo", "Alpha over Bravo 4-1", "Alpha over Bravo 5-4"]));

        Assert.That(games.GroupBy(g => g.Series).Select(g => g.Count()), Is.EqualTo(SixFiveOne));
    }

    [Test]
    public void GameHistory_APlayoffsRow_EndsTheSeries_AndADoubleForfeitHasNoWinner()
    {
        var games = Parse(new ResultsTab { Sheet = "Game History", Layout = ResultsLayout.GameHistory }, Sheet(
            HistoryHeader,
            ["Week 1", "Alpha", "Bravo", "5-0", "", "FF"],
            ["Playoffs", "Alpha", "Bravo", "5-0", "", "FF"],
            ["", "Alpha", "Charlie", "", "", "Double Forfeit"]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(g => g.Series).Distinct().Count(), Is.EqualTo(3), "A regular-season and a playoff forfeit of the same teams are two series");
            Assert.That(games.Select(g => g.IsPlayoffs), Is.EqualTo(RegularThenPlayoffs));
            Assert.That((games[2].Teams[0].Outcome, games[2].Teams[1].Outcome, games[2].IsForfeit), Is.EqualTo((Outcomes.Lost, Outcomes.Lost, true)));
        }
    }

    [Test]
    public void Calendar_EachScoredGameOfACell_IsAGameOfItsOwn()
    {
        var games = Parse(new ResultsTab { Sheet = "Schedule", Layout = ResultsLayout.Calendar }, Sheet(
            ["SUN", "MON"],
            ["5", "\n\n9:30pm: Impulse (5) vs. High Voltage (1)\n\n10:00pm: Legacy (3) vs. Impulse (5)"],
            ["*8pm: 2 Hype Crew (1) @ \nZero Gravity (5)", "9:30 pm: Gaming So Hard @ Rule #76", "8pm: TBD (5) vs. Nobody (0)"]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(Sides), Is.EqualTo(new[]
            {
                ((string?)"Impulse", Outcomes.Won, (int?)5, (string?)"High Voltage", Outcomes.Lost, (int?)1),
                ("Legacy", Outcomes.Lost, 3, "Impulse", Outcomes.Won, 5),
                ("2 Hype Crew", Outcomes.Lost, 1, "Zero Gravity", Outcomes.Won, 5),
            }), "A game without scores, or naming no team, is none");
            Assert.That(games.Select(g => g.Series).Distinct().Count(), Is.EqualTo(3), "Each its own series");
        }
    }

    [TestCase("G&tC", "Galvin & the Chipmunks", true)]
    [TestCase("A&tS", "A and the Spences", true)]
    [TestCase("MAB", "Mediocre At Best", true)]
    [TestCase("ApoPo", "Apocalypse Ponies", true)]
    [TestCase("ZMD", "Zaps MD", true)]
    [TestCase("TB", "The Boys", true)]
    [TestCase("DBAGS", "Don't Be A Gonk Scet", true)]
    [TestCase("DBAGS", "Don\u2019t Be A Gonk Scet", true)]
    [TestCase("AJYD", "AJunkyardDog", false)]
    [TestCase("Team", "Team Rocket", false)]
    [TestCase("Asc", "Ascended", false)]
    [TestCase("Team U.S.A.", "Team Rocket", false)]
    [TestCase("Team Rocket", "Team U.S.A.", false)]
    public void Abbreviates(string abbreviation, string name, bool abbreviates)
        => Assert.That(HistoricalArchiveBuilder.Abbreviates(abbreviation, name), Is.EqualTo(abbreviates));

    [Test]
    public void Table_SideBySideFixturesAndVisitorsHomeSchedule_AreParsed()
    {
        var fixtures = Parse(new ResultsTab { Sheet = "Results", Layout = ResultsLayout.Table }, Sheet(
            ["Week 1", "", "", "", "", "", "Week 2"],
            ["Team 1:", "Score:", "Score:", "Team 2:", "Game Link:", "", "Team 1:", "Score:", "Score:", "Team 2:", "Game Link:"],
            ["Legendary", "5", "1", "Underrated", "Game Link", "", "Oblivion", "W", "L", "Eclipse", "FORFEIT"]));

        var visitors = Parse(new ResultsTab { Sheet = "Schedule", Layout = ResultsLayout.Table }, Sheet(
            ["", "W/L", "Goals", "Visitors", "Home", "Goals", "W/L", "NH", "Link"],
            ["", "W", "5", "WTF", "EDG", "0", "L", "Regresser", "https://www.twitch.tv/videos/1"],
            ["", "L", "", "FTN", "DYN", "", "W", "FF", "FF"]));

        var seriesForfeit = Parse(new ResultsTab { Sheet = "SR", Layout = ResultsLayout.Series }, Sheet(
            ["Reflex over Bad Intentions FF"]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fixtures.Select(Sides), Is.EqualTo(new[]
            {
                ((string?)"Legendary", Outcomes.Won, (int?)5, (string?)"Underrated", Outcomes.Lost, (int?)1),
                ("Oblivion", Outcomes.Won, (int?)null, "Eclipse", Outcomes.Lost, (int?)null),
            }));
            Assert.That(fixtures[1].IsForfeit, Is.True);
            Assert.That(visitors.Select(Sides), Is.EqualTo(new[]
            {
                ((string?)"WTF", Outcomes.Won, (int?)5, (string?)"EDG", Outcomes.Lost, (int?)0),
                ("FTN", Outcomes.Lost, (int?)null, "DYN", Outcomes.Won, (int?)null),
            }));
            Assert.That((visitors[0].Host, visitors[0].Vod, visitors[1].IsForfeit), Is.EqualTo(("Regresser", "https://www.twitch.tv/videos/1", true)));
            Assert.That((seriesForfeit.Single().Teams[0].Name, seriesForfeit.Single().IsForfeit), Is.EqualTo(("Reflex", true)));
        }
    }

    [Test]
    public void Table_SideBySideBlocksPreserveSeries_SetPerBlockPhase_GuardMissingScore_AndHandleZeroForfeitWithWl()
    {
        var sheet = Sheet(
            ["Week 1", "", "", "", "", "", "Semi Final"],
            ["Home Team:", "Score:", "Score:", "Away Team:", "Game Link:", "", "Team 1:", "Score:", "Score:", "Team 2:", "Game Link:"],
            ["Alpha", "5", "1", "Bravo", "", "", "Charlie", "5", "2", "Delta", ""],
            ["Alpha", "5", "2", "Bravo", "", "", "Charlie", "5", "3", "Delta", ""],
            ["Week 2", "", "", "", "", "", "Final"],
            ["Alpha", "5", "4", "Bravo", "", "", "Alpha", "5", "0", "Charlie", ""]);

        var games = Parse(new ResultsTab { Sheet = "Results", Layout = ResultsLayout.Table }, sheet);

        var visitorsNoScores = Parse(new ResultsTab { Sheet = "Schedule", Layout = ResultsLayout.Table }, Sheet(
            ["W/L", "Visitors", "Home", "Link"],
            ["W", "Alpha", "Bravo", "FF"]));

        var zeroForfeit = Parse(new ResultsTab { Sheet = "Forfeits", Layout = ResultsLayout.Table }, Sheet(
            ["W/L", "Goals", "Visitors", "Home", "Goals", "W/L"],
            ["L", "0", "Alpha", "Bravo", "0", "W"]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games, Has.Count.EqualTo(6));
            // Left block: rows 2 & 3 share Week 1 series; row 5 under Week 2 starts new series; right block rows 2 & 3 share Semi Final series
            Assert.That(games[0].Series, Is.EqualTo(games[1].Series), "Consecutive rows in Block 0 stay in one series");
            Assert.That(games[2].Series, Is.Not.EqualTo(games[0].Series), "Week 2 divider inside Block 0 ends the Week 1 series");
            Assert.That(games[3].Series, Is.EqualTo(games[4].Series), "Consecutive rows in Block 1 stay in one series");
            Assert.That(games.Select(g => g.IsPlayoffs), Is.EqualTo(SideBySidePlayoffs),
                "Semi Final / Final banners in Block 1 mark Block 1 games as playoffs without polluting Block 0");
            Assert.That(visitorsNoScores.Single().Teams.Select(t => t.Outcome), Is.EqualTo(WonThenLost));
            Assert.That(zeroForfeit.Single().Teams.Select(t => t.Outcome), Is.EqualTo(LostThenWon));
            Assert.That(zeroForfeit.Single().IsForfeit, Is.True);
        }
    }

    private static readonly ResultsTab FixturesTab = new() { Sheet = "Results", Layout = ResultsLayout.Table };
    private static readonly string[] OneOneSided = ["fl20/Results: 1 results naming one team only left out"];
    private static readonly bool[] ForfeitThenTieThenForfeit = [true, false, true];

    [Test]
    public void Table_TeamFixtureHeadersByTheirOtherNames_AreRead_AndRowsNamingNoGameArePhasesOrOneSided()
    {
        var warnings = new List<string>();
        var games = Parse(FixturesTab, Sheet(
            ["Team 1", "Goals", "Team 2", "Vods"],
            ["Alpha", "W", "Bravo", "https://v/1"],
            ["Week 2"],
            ["Alpha", "", "", ""],
            ["", "", "", ""],
            ["Home Team", "Away Team", "Video"],
            ["Alpha", "Bravo", ""],
            ["Team 1", "Score", "Score", "Team 2", "Link"],
            ["Charlie", "", "", "Delta", ""],
            ["Team 1", "Score", "Score", "Team 2", "VOD"],
            ["Charlie", "", "", "Delta", ""]), warnings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(Sides), Is.EqualTo(new[] { ((string?)"Alpha", Outcomes.Won, (int?)null, (string?)"Bravo", Outcomes.Lost, (int?)null) }),
                "a W in the only score column is a win; a row with no score or W/L is no game");
            Assert.That((games[0].IsForfeit, games[0].Vod), Is.EqualTo((true, "https://v/1")));
            Assert.That(warnings, Is.EqualTo(OneOneSided));
        }
    }

    [Test]
    public void Table_VisitorsHomeHeadersByTheirOtherNames_AreRead_AndAW_LDecidesATie()
    {
        var games = Parse(FixturesTab, Sheet(
            ["Score", "Visitors", "Home", "Score", "WL", "Host", "VOD"],
            ["5", "Alpha", "Bravo", "5", "W", "FF", ""],
            ["3", "Alpha", "Bravo", "3", "", "Regresser", "https://v/2"],
            ["WL", "Visitors", "Home", "W/L", "Vods"],
            ["L", "Charlie", "Delta", "W", ""],
            ["Away", "Home", "Video"],
            ["Echo", "Foxtrot", "https://v/3"],
            ["Notes", "Visitors", "Home"],
            ["", "Golf", "Hotel"],
            ["Visitors", "Home"],
            ["Echo", "Foxtrot"]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(Sides), Is.EqualTo(new[]
            {
                ((string?)"Alpha", Outcomes.Lost, (int?)5, (string?)"Bravo", Outcomes.Won, (int?)5),
                ("Alpha", Outcomes.Tie, 3, "Bravo", Outcomes.Tie, 3),
                ("Charlie", Outcomes.Lost, null, "Delta", Outcomes.Won, null),
            }));
            Assert.That(games.Select(g => g.IsForfeit), Is.EqualTo(ForfeitThenTieThenForfeit));
            Assert.That(games.Select(g => (g.Host, g.Vod)), Is.EqualTo(new[] { ((string?)null, (string?)null), ("Regresser", "https://v/2"), (null, null) }),
                "a forfeit's mark in the host column is no host");
        }
    }

    [Test]
    public void Table_ABannerOverTheFirstBlockOnly_IsEveryBlocksPhase_AndHeadersWithoutAPairOfTeamsAreNone()
    {
        var games = Parse(FixturesTab, Sheet(
            ["Semi Finals", "", "", "", "", ""],
            ["Team 1", "Score", "Score", "Team 2", "", "Team 1", "Score", "Score", "Team 2"],
            ["Alpha", "5", "1", "Bravo", "", "Charlie", "2", "5", "Delta"]));

        var none = Parse(FixturesTab, Sheet(
            ["Team 1", "a", "b", "c", "d", "e", "f", "Team 2"],
            ["Visitors", "Score", "Away"],
            ["Alpha", "5", "1", "Bravo"]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(Sides), Is.EqualTo(new[]
            {
                ((string?)"Alpha", Outcomes.Won, (int?)5, (string?)"Bravo", Outcomes.Lost, (int?)1),
                ("Charlie", Outcomes.Lost, 2, "Delta", Outcomes.Won, 5),
            }));
            Assert.That(games.Select(g => g.IsPlayoffs), Has.All.True, "the second block's games are the banner's playoffs too");
            Assert.That(none, Is.Empty);
        }
    }
}
