using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Parsing;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Historical.Test;

/// <summary>DraftParser on the archive's draft layouts (the sheets' own, trimmed to a few teams).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class DraftParserTests
{
    private static SheetTable Sheet(params string[][] rows) => new() { Name = "Draft", Rows = [.. rows] };

    private static List<ArchiveTeam> Board(SheetTable sheet, DraftOrder order = DraftOrder.Columns)
        => DraftParser.Parse(sheet, new DraftTab { Sheet = "Draft", Order = order })!;

    private static IEnumerable<(string Team, string? Captain, int? Order)> Captains(List<ArchiveTeam> teams)
        => teams.Select(t => (t.TeamName, t.Captain, t.DraftOrder));

    private static IEnumerable<(string Team, string Gamertag, int Round, int? Pick)> Picks(List<ArchiveTeam> teams)
        => teams.SelectMany(t => t.Picks.Select(p => (t.TeamName, p.Gamertag, p.Round, p.Pick)));

    [Test]
    public void Board_ReadsEachTeamsCaptain_ItsColumnsPlace_AndItsPicksByRound()
    {
        // WL23's Draft Sheet: a title, the board, then the player pool (skipped: no Team Name row above it)
        var sheet = Sheet(
            ["", "HALO INFINITE WINTER LEAGUE 2023"],
            ["", "Team Name:", "Alpha", "", "Charlie"],
            ["", "Captain:", "Ace", "Bee", "Cee (c)"],
            ["", "1st Round:", "Dee", "Eee", "Gingy(R1P10)"],
            ["", "Grade", "A", "B", "C"],
            ["", "2nd Round:", "Fee", "", "Hee (to Alpha)"],
            ["", "Draft Pool"],
            ["", "Gamertag", "Discord"],
            ["", "Ace", "ace#1"]);

        var teams = Board(sheet);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Captains(teams), Is.EqualTo(new (string, string?, int?)[] { ("Alpha", "Ace", 1), ("", "Bee", 2), ("Charlie", "Cee", 3) }),
                "A team with no name is its captain's; the builder finds which");
            Assert.That(Picks(teams), Is.EqualTo(new (string, string, int, int?)[]
            {
                ("Alpha", "Dee", 1, null), ("Alpha", "Fee", 2, null), ("", "Eee", 1, null), ("Charlie", "Gingy", 1, null), ("Charlie", "Hee", 2, null),
            }), "Notes in brackets are not the player's; other rows are skipped");
        }
    }

    [Test]
    public void Board_ATabsNames_RenameThePlayersTheyMatch_AndLeaveTheRest()
    {
        var sheet = Sheet(
            ["Team Name", "Alpha", "Bravo"],
            ["Captain", "Ace", "Bee"],
            ["First Round", "Dee", "Eee"]);
        var tab = new DraftTab { Sheet = "Draft", Order = DraftOrder.None, Names = new() { ["ace"] = "Ace Renamed" } };

        var teams = DraftParser.Parse(sheet, tab)!;

        Assert.That(Captains(teams), Is.EqualTo(new (string, string?, int?)[] { ("Alpha", "Ace Renamed", null), ("Bravo", "Bee", null) }));
    }

    [Test]
    public void Board_ACaptainCellOfOnlySlashes_HasNoCaptain()
    {
        var sheet = Sheet(
            ["Team Name", "Alpha", "Bravo"],
            ["Captain", "/", "Bee / Cee"],
            ["First Round", "Dee", "Eee"]);

        var teams = Board(sheet, DraftOrder.None);

        Assert.That(Captains(teams), Is.EqualTo(new (string, string?, int?)[] { ("Alpha", null, null), ("Bravo", "Bee", null) }));
    }

    [Test]
    public void Board_ATeamThePlaceholderNames_TakesTheConfigsName_WithItsCaptainAndPicks()
    {
        // H3 Action 2022's Teams tab: RDG NHL's team was never named, its column says "Team Name"
        var sheet = Sheet(
            ["Team Name", "The Boys Szn 3", "Team Name", "The Smurfs"],
            ["Captain", "AJ DaJuiceMayne", "RDG NHL", "I robot9000"],
            ["First Round", "Silva", "DlGITAL PAIN", "GodSnipezX"],
            ["Second Round", "El Diablo X230X", "MysticalNovice", "ixjaga"]);
        var config = new HistoricalSourceConfig { TeamNames = new() { ["team  name"] = "RDG NHL" } };

        var teams = DraftParser.Parse(sheet, new DraftTab { Sheet = "Teams", Order = DraftOrder.None }, config)!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Captains(teams), Is.EqualTo(new (string, string?, int?)[] { ("The Boys Szn 3", "AJ DaJuiceMayne", null), ("RDG NHL", "RDG NHL", null), ("The Smurfs", "I robot9000", null) }));
            Assert.That(Picks(teams).Where(p => p.Team == "RDG NHL"), Is.EqualTo(new (string, string, int, int?)[] { ("RDG NHL", "DlGITAL PAIN", 1, null), ("RDG NHL", "MysticalNovice", 2, null) }));
        }
    }

    [Test]
    public void Table_ATeamThePlaceholderNames_TakesTheConfigsName()
    {
        // H3 Action 2022's TeamInfo: RDG NHL's team is "Team Name" on every row
        var sheet = Sheet(
            ["Draft Order", "Team Name", "Player Name"],
            ["10", "THE BOYS SZN 3", "AJ DaJuiceMayne"],
            ["5", "Team Name", "RDG NHL"],
            ["5", "Team Name", "DlGITAL PAIN"]);
        var config = new HistoricalSourceConfig { TeamNames = new() { ["Team Name"] = "RDG NHL" } };

        var teams = DraftParser.Parse(sheet, new DraftTab { Sheet = "TeamInfo", Layout = DraftLayout.Table }, config)!;

        Assert.That(Captains(teams), Is.EqualTo(new (string, string?, int?)[] { ("THE BOYS SZN 3", null, 10), ("RDG NHL", null, 5) }));
    }

    [TestCase("1st Round:", 1)]
    [TestCase("3rd Round", 3)]
    [TestCase("Round 2", 2)]
    [TestCase("Second Round", 2)]
    [TestCase("fifth round", 5)]
    [TestCase("Duo/1st Round:", 1)]
    [TestCase("Round One", 1)]
    [TestCase("Round Five", 5)]
    [TestCase("0th Round", null)]
    [TestCase("Player 2", null)]
    [TestCase("Round", null)]
    [TestCase("Bid 1", null)]
    public void Round_ReadsTheBoardsRoundLabels(string label, int? round)
    {
        Assert.That(DraftParser.Round(label), Is.EqualTo(round));
    }

    [Test]
    public void Board_ARowOfNumbersAboveARound_NumbersItsPicks()
    {
        // SML25 Pro's snake draft: the round's picks numbered above it, the next round's back the other way
        var sheet = Sheet(
            ["Team Name:", "Alpha", "Bravo"],
            ["Captain", "Ace", "Bee"],
            ["Player 1", "Dee", "Eee"],
            ["", "1", "2"],
            ["1st Round:", "Dee", "Eee"],
            ["", "4", "3"],
            ["2nd Round:", "Fee", "Gee"],
            ["3rd Round:", "Hee", "Jay"]);

        Assert.That(Picks(Board(sheet)), Is.EqualTo(new (string, string, int, int?)[]
        {
            ("Alpha", "Dee", 1, 1), ("Alpha", "Fee", 2, 4), ("Alpha", "Hee", 3, null), ("Bravo", "Eee", 1, 2), ("Bravo", "Gee", 2, 3), ("Bravo", "Jay", 3, null),
        }), "The Player rows are no round; a round with no numbers above it has none");
    }

    [Test]
    public void Board_ARowOfNumbersWithAGap_IsNoPicksNumbers()
    {
        var sheet = Sheet(
            ["Team Name:", "Alpha", "Bravo"],
            ["Captain", "Ace", "Bee"],
            ["", "1", ""],
            ["1st Round:", "Dee", "Eee"]);

        Assert.That(Picks(Board(sheet)).Select(p => p.Pick), Is.All.Null);
    }

    [Test]
    public void Board_ARowOfTheSameNumbers_IsNoPicksNumbers()
    {
        // An unlabelled row of anything else (a grade, a count) is no round's numbers: picks are numbered once each
        var sheet = Sheet(
            ["Team Name:", "Alpha", "Bravo"],
            ["Captain", "Ace", "Bee"],
            ["", "3", "3"],
            ["1st Round:", "Dee", "Eee"]);

        Assert.That(Picks(Board(sheet)).Select(p => p.Pick), Is.All.Null);
    }

    [Test]
    public void Table_ABlankTeamCell_IsTheTeamAboveIt()
    {
        // A team's name in a merged cell: its first row only
        var sheet = Sheet(
            ["Team Name", "Player Name", "Draft Pick"],
            ["", "Stray", "R1P1"],
            ["Alpha", "Ace", "C1"],
            ["", "Dee", "R1P1"]);

        var teams = DraftParser.Parse(sheet, new DraftTab { Sheet = "TeamInfo", Layout = DraftLayout.Table })!;

        Assert.That(Picks(teams), Is.EqualTo(new (string, string, int, int?)[] { ("Alpha", "Dee", 1, null) }), "A row above any team is none's");
    }

    [Test]
    public void Board_Linear_NumbersEachPickByItsRoundAndColumn()
    {
        var sheet = Sheet(
            ["Team Name:", "Alpha", "Bravo", "Charlie"],
            ["Captain:", "Ace", "Bee", "Cee"],
            ["1st Round:", "Dee", "Eee", "Fee"],
            ["2nd Round:", "Gee", "", "Jay"]);

        Assert.That(Picks(Board(sheet, DraftOrder.Linear)).Select(p => (p.Gamertag, p.Pick)), Is.EqualTo(new (string, int?)[]
        {
            ("Dee", 1), ("Gee", 4), ("Eee", 2), ("Fee", 3), ("Jay", 6),
        }));
    }

    [Test]
    public void Board_None_HasNoCaptainsOrder()
    {
        // A bid draft's teams: Captain and Player rows, the columns in no draft order
        var sheet = Sheet(
            ["", "SEASON COMPLETE", "Seed 1"],
            ["Team Name", "Bears", "Beer"],
            ["Captain", "a Bears Nutz", "Solace VIII"],
            ["Player 2", "TommyWstSide", "Myst the Cold"]);

        var teams = Board(sheet, DraftOrder.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Captains(teams), Is.EqualTo(new (string, string?, int?)[] { ("Bears", "a Bears Nutz", null), ("Beer", "Solace VIII", null) }));
            Assert.That(teams.SelectMany(t => t.Picks), Is.Empty);
        }
    }

    [Test]
    public void Board_ATeamCalledTeamName_IsNoOtherBoard_AndIsNoTeam()
    {
        // H3 Action's Teams: a team named "Team Name" among the others, its column no board's labels
        var sheet = Sheet(
            ["Team Name", "Alpha", "Team Name", "Bravo"],
            ["Record", "12-8", "12-8", "14-6"],
            ["Captain", "Ace", "RDG NHL", "Bee"],
            ["First Round", "Dee", "DlGITAL PAIN", "Eee"]);

        var teams = Board(sheet, DraftOrder.None);

        Assert.That(Picks(teams), Is.EqualTo(new (string, string, int, int?)[] { ("Alpha", "Dee", 1, null), ("Bravo", "Eee", 1, null) }));
    }

    [Test]
    public void Board_TwoBoardsSideBySide_AndStacked_EachReadsItsOwnRows()
    {
        var sheet = Sheet(
            ["Team Name:", "Alpha", "Bravo", "Team Name:", "Charlie"],
            ["Captain:", "Ace", "Bee", "Captain:", "Cee"],
            ["1st Round:", "Dee", "Eee", "1st Round:", "Fee"],
            ["Team Name:", "Delta"],
            ["Captain:", "Dan"],
            ["1st Round:", "Gee"]);

        var teams = Board(sheet);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Captains(teams), Is.EqualTo(new (string, string?, int?)[] { ("Alpha", "Ace", 1), ("Bravo", "Bee", 2), ("Charlie", "Cee", 1), ("Delta", "Dan", 1) }),
                "Each board's own order");
            Assert.That(Picks(teams).Select(p => (p.Team, p.Gamertag)), Is.EqualTo(new[] { ("Alpha", "Dee"), ("Bravo", "Eee"), ("Charlie", "Fee"), ("Delta", "Gee") }));
        }
    }

    [Test]
    public void Board_AColumnWithNeitherNameNorCaptain_EndsTheBoard()
    {
        // Respawn's Draft: the seed list beside the board, a column past it
        var sheet = Sheet(
            ["Bucket Number", "1", "2", "", "Seed", "Team Name"],
            ["Team Name:", "Alpha", "Bravo", "", "1", "Bravo"],
            ["Captain:", "Ace", "", "", "2", "Alpha"],
            ["1st Round:", "Dee", "Eee", "Stray", "3", "Charlie"]);

        Assert.That(Captains(Board(sheet)), Is.EqualTo(new (string, string?, int?)[] { ("Alpha", "Ace", 1), ("Bravo", null, 2) }));
    }

    [Test]
    public void Parse_ASheetWithNoBoardOrTable_IsNull()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DraftParser.Parse(Sheet(["Team Name:", "Alpha"], ["1st Round:", "Dee"]), new DraftTab { Sheet = "Draft" }), Is.Null, "No Captain row: no board");
            Assert.That(DraftParser.Parse(Sheet(["Gamertag", "Discord"]), new DraftTab { Sheet = "Draft", Layout = DraftLayout.Table }), Is.Null);
            Assert.That(DraftParser.Parse(Sheet(["Team Name", "Player Name"], ["Alpha", "Ace"]), new DraftTab { Sheet = "Draft", Layout = DraftLayout.Table }), Is.Null,
                "A table with no draft column says nothing of the draft");
        }
    }

    [Test]
    public void Table_DraftPickCodes_GiveTheCaptainTheOrderAndTheRounds()
    {
        // FL24's TeamInfo: "C1" the 1st captain, "R1P10" a 1st-round pick (traded from the 10th spot)
        var sheet = Sheet(
            ["Seed #", "Team Name", "Player Name", "Location", "Place Finished", "Draft Pick"],
            ["Draft Spot 1", "Bad Intentions", "NecessaryEvil10", "", "", "C1"],
            ["Draft Spot 1", "Bad Intentions", "PapaSnipezX", "", "", "R1P1"],
            ["Draft Spot 1", "Bad Intentions", "Fanciest Pants", "", "", "r2p10"],
            ["Draft Spot 2", "NeuraLink", "Solace VIII", "", "", "R1P2"],
            ["Draft Spot 2", "NeuraLink", "TcT AVENGER", "", "", "C2"],
            ["Draft Spot 2", "NeuraLink", "Second Captain", "", "", "C7"],
            ["Draft Spot 2", "NeuraLink", "Sub", "", "", "?"],
            ["Draft Spot 2", "NeuraLink", "", "", "", "C9"],
            ["Draft Spot 2", "NeuraLink", "Benched", "", "", "R0P2"],
            ["Draft Spot 99999999999", "Overflow", "Ace", "", "", ""],
            ["", "Subs", "Stray", "", "", "R1P3"]);

        var teams = DraftParser.Parse(sheet, new DraftTab { Sheet = "TeamInfo", Layout = DraftLayout.Table })!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Captains(teams), Is.EqualTo(new (string, string?, int?)[] { ("Bad Intentions", "NecessaryEvil10", 1), ("NeuraLink", "TcT AVENGER", 2) }));
            Assert.That(Picks(teams), Is.EqualTo(new (string, string, int, int?)[]
            {
                ("Bad Intentions", "PapaSnipezX", 1, null), ("Bad Intentions", "Fanciest Pants", 2, null), ("NeuraLink", "Solace VIII", 1, null),
            }), "A placeholder team's rows are none of the draft; a team's first captain code is its captain");
        }
    }

    [Test]
    public void Table_DraftOrderColumn_GivesTheTeamsOrder_TheCaptainsCodeOtherwise()
    {
        // H3 Action's TeamInfo: Draft Order on every row of the team, no captains
        var sheet = Sheet(
            ["Draft Order", "Team Name", "Player Name", "Location", "Draft Pick"],
            ["1", "JUNKYARD DOGS", "NKOTB4EVAH", "EAST"],
            ["1", "JUNKYARD DOGS", "Ace B S1lentND1", "CENTRAL"],
            ["x", "12 Inches of Tommy", "Priest XYZ", "", "C4"],
            ["", "12 Inches of Tommy", "Icey Tanker", "WEST"],
            ["", "Alpha", "x Zeg x", "", "C3"],
            ["0", "Bravo", "Bee", ""]);

        var teams = DraftParser.Parse(sheet, new DraftTab { Sheet = "TeamInfo", Layout = DraftLayout.Table })!;

        Assert.That(Captains(teams), Is.EqualTo(new (string, string?, int?)[] { ("JUNKYARD DOGS", null, 1), ("12 Inches of Tommy", "Priest XYZ", 4), ("Alpha", "x Zeg x", 3) }),
            "The column's place where it gives one, the captain's code's where it doesn't; a team with neither is none of the draft");
    }

    [Test]
    public void Board_WithNoTeamNameRow_StartsAtItsFirstCaptainRow_ItsTeamsTheCaptains()
    {
        // SML21 Ammy's draft: a title, two captains per team, then the rounds, and the player pool below
        var sheet = Sheet(
            ["", "AGLA Summer League 2021 Ammy Draft"],
            ["", "Captain 1", "AwareMosquito", "Conra4859", "lol Ninj0"],
            ["", "Captain 2", "Beam1n1", "TommyWestSide", "Nkot"],
            ["", "First Round:", "RDG NHL", "TheAlphaTurtle", "Atonemilitant"],
            ["", "Second Round:", "Nihilus", "MoldyVolts", ""],
            ["", "Player Pool"],
            ["", "Player", "H3"],
            ["", "Dabb", "Tank"]);

        var teams = Board(sheet);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Captains(teams), Is.EqualTo(new (string, string?, int?)[] { ("", "AwareMosquito", 1), ("", "Conra4859", 2), ("", "lol Ninj0", 3) }),
                "Each team is its first captain's, the builder finds which; the second captain is no pick");
            Assert.That(Picks(teams), Is.EqualTo(new (string, string, int, int?)[]
            {
                ("", "RDG NHL", 1, null), ("", "Nihilus", 2, null), ("", "TheAlphaTurtle", 1, null), ("", "MoldyVolts", 2, null), ("", "Atonemilitant", 1, null),
            }));
        }
    }

    [Test]
    public void Board_WithATeamNameRow_HasNoBoardAtACaptainRow()
    {
        // A board whose captains are in a "Captain 1" row is read from its Team Name row, once
        var sheet = Sheet(
            ["Team Name", "Alpha", "Bravo"],
            ["Captain 1", "Ace", "Bee"],
            ["1st Round", "Dee", "Eee"]);

        Assert.That(Captains(Board(sheet)), Is.EqualTo(new (string, string?, int?)[] { ("Alpha", "Ace", 1), ("Bravo", "Bee", 2) }));
    }

    [Test]
    public void Blocks_ReadEachCaptainsBlock_InReadingOrder_ARepeatedCaptainRowOnce()
    {
        // SML20 (Reach PC)'s draft: two blocks side by side per row of them, "Captain" then "Round One"... each beside its name
        var sheet = Sheet(
            ["", "Player Pool", "Captain", "Sam Adams", "Captain", "Zeg"],
            ["Captains", "oDhmon", "Round One", "Galvan", "Round One", "Digital Pain"],
            ["Sam Adams", "mstell96", "Round Two", "Jiggly", "Round Two", "Rage"],
            ["Zeg", "Conra4859", "Captain", "Grunt", "Captain", "Nkot"],
            ["", "", "Captain", "Grunt", "Captain", "Nkot"],
            ["", "", "Round One", "dhmon", "Round One", "ixLiono"],
            ["", "", "Round Two", "LightSaber", "Round Two", ""]);

        var teams = DraftParser.Parse(sheet, new DraftTab { Sheet = "Sheet1", Layout = DraftLayout.Blocks })!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Captains(teams), Is.EqualTo(new (string, string?, int?)[] { ("", "Sam Adams", 1), ("", "Zeg", 2), ("", "Grunt", 3), ("", "Nkot", 4) }),
                "Row by row, each row's blocks left to right");
            Assert.That(Picks(teams), Is.EqualTo(new (string, string, int, int?)[]
            {
                ("", "Galvan", 1, null), ("", "Jiggly", 2, null), ("", "Digital Pain", 1, null), ("", "Rage", 2, null),
                ("", "dhmon", 1, null), ("", "LightSaber", 2, null), ("", "ixLiono", 1, null),
            }), "The player pool beside the blocks is no team's");
        }
    }

    [Test]
    public void ACaptainRowWithNoRoundBelowIt_BeforeTheNextBoardsCaptains_IsNoBoard()
    {
        // A roster's "Captain" row then another's: no round between them
        var sheet = Sheet(
            ["Captain", "Ace", "Bee"],
            ["Player 1", "Dee", "Eee"],
            ["", ""],
            ["Captain", "Cee"],
            ["1st Round", "Fee"]);

        Assert.That(Captains(Board(sheet)), Is.EqualTo(new (string, string?, int?)[] { ("", "Cee", 1) }));
    }

    [Test]
    public void CaptainBoards_SideBySide_EachReadsItsOwnColumns_AndOneWithNoRoundIsNone()
    {
        var sheet = Sheet(
            ["Captain 1", "Ace", "", "Captain 1", "Bee"],
            ["1st Round", "Dee", "", "1st Round", "Eee"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Captains(Board(sheet)), Is.EqualTo(new (string, string?, int?)[] { ("", "Ace", 1), ("", "Bee", 1) }));
            Assert.That(DraftParser.Parse(Sheet(["Captain", "Ace"], ["Player 1", "Dee"]), new DraftTab { Sheet = "Draft" }), Is.Null, "No round below it");
        }
    }

    [TestCase(DraftOrder.Linear)]
    [TestCase(DraftOrder.None)]
    public void Blocks_NumberTheirPicksByTheOrder_SkippingNotes_AndACaptainStackedOnAnothersIsTheirOwn(DraftOrder order)
    {
        var sheet = Sheet(
            ["Captain", "Ace"],
            ["Notes", "x"],
            ["Round One", "Dee"],
            ["Captain", "Bee"],
            ["Round One", "Eee"],
            ["Round Two", "Fee"],
            ["Captain", "Cee"],
            ["Captain", "Gee"],
            ["Round One", "Hee"]);

        var teams = DraftParser.Parse(sheet, new DraftTab { Sheet = "Sheet1", Layout = DraftLayout.Blocks, Order = order })!;

        var linear = order == DraftOrder.Linear;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Captains(teams), Is.EqualTo(new (string, string?, int?)[] { ("", "Ace", linear ? 1 : null), ("", "Bee", linear ? 2 : null), ("", "Gee", linear ? 3 : null) }),
                "Cee's block ends at Gee's Captain cell before any round");
            Assert.That(Picks(teams), Is.EqualTo(new (string, string, int, int?)[]
            {
                ("", "Dee", 1, linear ? 1 : null), ("", "Eee", 1, linear ? 2 : null), ("", "Fee", 2, linear ? 5 : null), ("", "Hee", 1, linear ? 3 : null),
            }));
        }
    }

    [Test]
    public void CaptainBoards_Stacked_EachReadsOnlyItsOwnRounds()
    {
        var sheet = Sheet(
            ["Captain 1", "Ace", "Bee"],
            ["Captain 2", "Abe", "Ben"],
            ["1st Round", "Dee", "Eee"],
            ["Captain 1", "Cee", "Fee"],
            ["1st Round", "Gee", "Hee"]);

        Assert.That(Picks(Board(sheet)).Select(p => p.Gamertag), Is.EqualTo(StackedPicks), "The lower board's rounds are none of the upper's");
    }

    private static readonly string[] StackedPicks = ["Dee", "Eee", "Gee", "Hee"];
}
