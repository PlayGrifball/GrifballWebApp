using GrifballWebApp.Domain.League;
using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Parsing;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;
using SeriesRow = (GrifballWebApp.Domain.League.Bracket Bracket, int Round, int Number, string Home, string Away, int? HomeSeed, int? AwaySeed, int? HomeWins, int? AwayWins, GrifballWebApp.Domain.League.SeasonMatchSide? Winner);

namespace GrifballWebApp.Historical.Test;

/// <summary>BracketParser on the archive's bracket layouts (the sheets' own, made-up teams): a drawn bracket cell by cell,
/// and a tab per round of game rows.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class BracketParserTests
{
    private static readonly string[] NoPlayoffsSheet = ["wb: no bracket sheet 'Playoffs'"];
    private static readonly string[] OddTeamAndMissingTab =
    [
        "wb: bracket round 'Round 1' has a team without an opponent (Charlie); left out",
        "wb: no bracket round sheet 'Round 3'",
    ];

    private readonly List<string> _warnings = [];

    /// <summary>A sheet of these cells by reference, its empty rows left out as a workbook's are.</summary>
    private static SheetTable Cells(string name, params (string Reference, string Value)[] cells)
    {
        static (int Row, int Column) Parse(string reference)
        {
            var letters = new string([.. reference.TakeWhile(char.IsLetter)]);
            return (int.Parse(reference[letters.Length..]), letters.Aggregate(0, (n, c) => n * 26 + c - 'A' + 1) - 1);
        }
        var parsed = cells.Select(c => (Place: Parse(c.Reference), c.Value)).ToList();
        var numbers = parsed.Select(c => c.Place.Row).Distinct().Order().ToList();
        var rows = numbers.Select(n =>
        {
            var row = new string[parsed.Where(c => c.Place.Row == n).Max(c => c.Place.Column) + 1];
            Array.Fill(row, "");
            foreach (var (place, value) in parsed.Where(c => c.Place.Row == n))
                row[place.Column] = value;
            return row;
        }).ToList();
        return new SheetTable { Name = name, Rows = rows, RowNumbers = numbers };
    }

    private static SheetWorkbook Workbook(params SheetTable[] sheets) => new() { Sheets = [.. sheets] };

    private ArchiveBracket Parse(BracketTab tab, params SheetTable[] sheets) => BracketParser.Parse("wb", tab, Workbook(sheets), _warnings)!;

    private static SeriesRow Row(ArchiveBracketSeries s)
        => (s.Bracket, s.Round, s.Number, s.Home, s.Away, s.HomeSeed, s.AwaySeed, s.HomeWins, s.AwayWins, s.Winner);

    [Test]
    public void Cells_TheInfiniteTemplate_ReadsSeedsWinnersAndTheWinnersScoreFirst_ByesLeftOut()
    {
        // Seed 1 had a BYE (its quarter-final's cells are empty); 4 beat 5 2-1 though its score cell reads winner first
        var sheet = Cells("Playoffs",
            ("C4", "vs (0-0)"), ("G5", "Alpha"),
            ("B8", "S4"), ("C8", "Delta"), ("C10", "vs (2-1)"), ("B11", "S5"), ("C11", "Echo"), ("G8", "Echo"), ("G7", "vs (2-0)"),
            ("J11", "Echo (Cap)"), ("L13", "Echo"));
        var tab = new BracketTab
        {
            Sheet = "Playoffs",
            Slots =
            [
                new() { Round = 1, Home = "C2", Away = "C5", Score = "C4", Winner = "G5", HomeSeed = "B2", AwaySeed = "B5" },
                new() { Round = 1, Home = "C8", Away = "C11", Score = "C10", Winner = "G8", HomeSeed = "B8", AwaySeed = "B11" },
                new() { Round = 2, Home = "G5", Away = "G8", Score = "G7", Winner = "J11" },
                new() { Round = 3, Home = "J11", Away = "J14", Winner = "L13" },
            ],
        };

        var series = Parse(tab, sheet).Series.Select(Row);

        Assert.That(series, Is.EqualTo(new SeriesRow[]
        {
            (Bracket.Winner, 1, 1, "Delta", "Echo", 4, 5, 1, 2, SeasonMatchSide.Away),
            (Bracket.Winner, 2, 2, "Alpha", "Echo", null, null, 0, 2, SeasonMatchSide.Away),
        }), "The final's other side is empty: not played");
    }

    [Test]
    public void Cells_SeedsInNamesScoresNamingTheWinnerAndEachSidesOwnWins()
    {
        var sheet = Cells("Bracket",
            ("A1", "Rated R (4)"), ("A3", "Hammers (5)"), ("A2", "3-1 Rated R"),
            ("B1", "Beer - Seed 1"), ("B3", "Zeg - seed 5/6"), ("C1", "2"), ("C3", "0"),
            ("D1", "Team Name"), ("D3", "Match 9 (If Needed)"),
            ("E1", "Alpha"), ("E3", "Bravo"), ("F1", "1"), ("F3", "1"), ("G1", "Bravo"));
        var tab = new BracketTab
        {
            Sheet = "Bracket",
            SeedsInNames = true,
            Slots =
            [
                new() { Round = 1, Home = "A1", Away = "A3", Score = "A2" },
                new() { Round = 1, Home = "B1", Away = "B3", HomeScore = "C1", AwayScore = "C3" },
                new() { Round = 1, Home = "D1", Away = "D3" },
                new() { Round = 2, Home = "E1", Away = "E3", HomeScore = "F1", AwayScore = "F3", Winner = "G1" },
            ],
        };

        var series = Parse(tab, sheet).Series.Select(Row);

        Assert.That(series, Is.EqualTo(new SeriesRow[]
        {
            (Bracket.Winner, 1, 1, "Rated R", "Hammers", 4, 5, 3, 1, SeasonMatchSide.Home),
            (Bracket.Winner, 1, 2, "Beer", "Zeg", 1, null, 2, 0, SeasonMatchSide.Home),
            (Bracket.Winner, 2, 3, "Alpha", "Bravo", null, null, 1, 1, SeasonMatchSide.Away),
        }), "Placeholders are no team; a seed shared by two is none");
    }

    [Test]
    public void Cells_AScoreNothingNamesTheWinnerOf_IsLeftOut_AndTheWinnerIsTheTeamThatPlaysOn()
    {
        var sheet = Cells("Bracket", ("A1", "Alpha"), ("A3", "Bravo"), ("A2", "vs (2-0)"), ("B1", "Bravo"), ("B3", "Charlie"), ("C1", "Delta"), ("C3", "Echo"), ("C2", "2-1"));
        var tab = new BracketTab
        {
            Sheet = "Bracket",
            Slots =
            [
                new() { Round = 1, Home = "A1", Away = "A3", Score = "A2" },
                new() { Round = 2, Home = "B1", Away = "B3" },
                new() { Round = 3, Home = "C1", Away = "C3", Score = "C2" },
            ],
        };

        var series = Parse(tab, sheet).Series.Select(s => (s.Home, s.HomeWins, s.AwayWins, s.Winner));

        Assert.That(series, Is.EqualTo(new (string, int?, int?, SeasonMatchSide?)[]
        {
            ("Alpha", 0, 2, SeasonMatchSide.Away),
            ("Bravo", null, null, null),
            ("Delta", null, null, null),
        }), "Bravo plays on; nothing says who won the last two");
    }

    [Test]
    public void Cells_AGrandFinalWithASuddenDeath_WasWonByTheLosersBracketsTeam_TheLosersBracketNumberedFromOne()
    {
        var sheet = Cells("Playoffs", ("A1", "Alpha"), ("A2", "Bravo"), ("B1", "Charlie"), ("B2", "Bravo"), ("C1", "Alpha"), ("C2", "Charlie"), ("D1", "Charlie"));
        var tab = new BracketTab
        {
            Sheet = "Playoffs",
            Slots =
            [
                new() { Round = 1, Home = "A1", Away = "A2", Winner = "C1" },
                new() { Bracket = Bracket.Loser, Round = 1, Home = "B1", Away = "B2", Winner = "C2" },
                new() { Bracket = Bracket.GrandFinal, Round = 2, Home = "C1", Away = "C2" },
                new() { Bracket = Bracket.GrandFinalSuddenDeath, Round = 3, Home = "C1", Away = "C2", Winner = "D1" },
            ],
        };

        var series = Parse(tab, sheet).Series.Select(s => (s.Bracket, s.Number, s.Winner));

        Assert.That(series, Is.EqualTo(new (Bracket, int, SeasonMatchSide?)[]
        {
            (Bracket.Winner, 1, SeasonMatchSide.Home),
            (Bracket.Loser, 1, SeasonMatchSide.Home),
            (Bracket.GrandFinal, 2, SeasonMatchSide.Away),
            (Bracket.GrandFinalSuddenDeath, 3, SeasonMatchSide.Away),
        }));
    }

    [Test]
    public void Cells_NoSuchSheet_IsNoBracket_Warned()
    {
        var bracket = BracketParser.Parse("wb", new BracketTab { Sheet = "Playoffs" }, Workbook(), _warnings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bracket, Is.Null);
            Assert.That(_warnings, Is.EqualTo(NoPlayoffsSheet));
        }
    }

    private static SheetTable Round(string name, params string[][] rows) => new() { Name = name, Rows = [["Seed", "Team Name", "Player", "Wins"], .. rows] };

    private static string[] Line(string seed, string team, string player, string wins) => [seed, team, player, wins];

    [Test]
    public void Rounds_PairTheTeamsRuns_ReadTheLosersBracket_AndTheGrandFinalsSuddenDeath()
    {
        var round1 = Round("Round 1",
            Line("1", "Alpha", "a1", "1"), Line("1", "Alpha", "a2", "1"), Line("4", "Delta", "d1", "0"), Line("", "", "d2", "0"),
            // A forfeit: no wins on either side, Bravo plays on
            Line("Seed 2", "Bravo", "b1", ""), Line("Seed 2", "Bravo", "b2", ""), Line("Seed 3", "Charlie", "c1", ""), Line("Seed 3", "Charlie", "c2", ""),
            ["Losers Bracket: Round 1"],
            // Charlie's game: one of Delta's players listed under Echo in the middle of it
            Line("4", "Delta", "d1", "1"), Line("5", "Echo", "e1", "1"), Line("4", "Delta", "d2", "1"), Line("3", "Charlie", "c1", "0"), Line("3", "Charlie", "c2", "0"));
        var finals = Round("Finals",
            Line("1", "Alpha", "a1", "0"), Line("1", "Alpha", "a2", "0"), Line("2", "Bravo", "b1", "1"), Line("2", "Bravo", "b2", "1"),
            Line("1", "Alpha", "a1", "1"), Line("1", "Alpha", "a2", "1"), Line("2", "Bravo", "b1", "0"), Line("2", "Bravo", "b2", "0"),
            ["Losers Bracket: Round 2"],
            Line("3", "Charlie", "c1", "1"), Line("3", "Charlie", "c2", "1"), Line("4", "Delta", "d1", "0"), Line("4", "Delta", "d2", "0"));
        var tab = new BracketTab { Rounds = [new() { Sheet = "Round 1", Round = 1 }, new() { Sheet = "Finals", Round = 2, Bracket = Bracket.GrandFinal }] };

        var series = Parse(tab, round1, finals).Series.Select(Row);

        Assert.That(series, Is.EqualTo(new SeriesRow[]
        {
            (Bracket.Winner, 1, 1, "Alpha", "Delta", 1, 4, 1, 0, SeasonMatchSide.Home),
            (Bracket.Winner, 1, 2, "Bravo", "Charlie", 2, 3, null, null, SeasonMatchSide.Home),
            (Bracket.Loser, 1, 1, "Delta", "Charlie", 4, 3, 1, 0, SeasonMatchSide.Home),
            (Bracket.Loser, 2, 2, "Charlie", "Delta", 3, 4, 1, 0, SeasonMatchSide.Home),
            (Bracket.GrandFinal, 2, 3, "Alpha", "Bravo", 1, 2, 0, 1, SeasonMatchSide.Away),
            (Bracket.GrandFinalSuddenDeath, 3, 4, "Alpha", "Bravo", 1, 2, 1, 0, SeasonMatchSide.Home),
        }), "The grand final's tab: its losers' round first; a second game of its teams is the sudden death");
    }

    [Test]
    public void Rounds_ATeamWithoutAnOpponent_IsLeftOut_Warned_AndAMissingTabIsNoBracket()
    {
        var round1 = Round("Round 1", Line("1", "Alpha", "a1", "1"), Line("2", "Bravo", "b1", "0"), Line("3", "Charlie", "c1", "0"));
        var noHeader = new SheetTable { Name = "Round 2", Rows = [["nothing here"]] };

        var bracket = Parse(new BracketTab { Rounds = [new() { Sheet = "Round 1", Round = 1 }, new() { Sheet = "Round 2", Round = 2 }] }, round1, noHeader);
        var missing = BracketParser.Parse("wb", new BracketTab { Rounds = [new() { Sheet = "Round 3", Round = 3 }] }, Workbook(round1), _warnings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bracket.Series.Select(s => (s.Home, s.Away)), Is.EqualTo(new[] { ("Alpha", "Bravo") }));
            Assert.That(missing, Is.Null);
            Assert.That(_warnings, Is.EqualTo(OddTeamAndMissingTab));
        }
    }

    [TestCase("S3", 3)]
    [TestCase("Seed 6", 6)]
    [TestCase("1st", 1)]
    [TestCase("4", 4)]
    [TestCase("2.0", 2)]
    [TestCase("S1/2", null)]
    [TestCase("", null)]
    [TestCase("0", null)]
    [TestCase("1.5", null)]
    public void Seed_ReadsTheSheetsWaysOfWritingOne(string cell, int? seed)
    {
        Assert.That(BracketParser.Seed(cell), Is.EqualTo(seed));
    }
}
