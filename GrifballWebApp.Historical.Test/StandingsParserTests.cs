using GrifballWebApp.Historical.Parsing;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Historical.Test;

/// <summary>StandingsParser on the archive's standings layouts (the sheets' own, trimmed to a few teams).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class StandingsParserTests
{
    private static readonly string[] Str8GriffinOnly = ["Str8 Griffin"];
    private static readonly string[] AlphaAndBravo = ["Alpha", "Bravo"];
    private static readonly (string, string?)[] NamesAndCaptains = [("Alpha", "Ace"), ("Bravo", "Bee")];
    private static readonly (string, int?, int?)[] RecordsOfAlphaAndBravo = [("Alpha", 5, 3), ("Bravo", 4, 4)];

    private static readonly (string, int?, int?)[] SecondBlockOwnRanks = [("Charlie", 2, null), ("Delta", 1, null)];
    private static readonly (string, int?, int?)[] LegacyWithoutProSeeds = [("Legacy A", null, 7), ("Legacy B", null, 3)];

    private static SheetTable Sheet(params string[][] rows) => new() { Name = "Standings", Rows = [.. rows] };

    [Test]
    public void Rows_ATableBesideAnother_TakesNoColumnOfTheTableLeftOfIt()
    {
        // Pro (Seed, Team, Wins) beside Legacy (Team, Wins): Legacy's teams have no seed of the Pro team on their row
        var sheet = Sheet(
            ["Seed", "Team", "Wins", "", "Team", "Wins"],
            ["1", "Pro A", "9", "", "Legacy A", "7"],
            ["2", "Pro B", "5", "", "Legacy B", "3"]);

        var legacy = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "S", Anchor = "Team", AnchorNumber = 2 });

        Assert.That(legacy!.Select(t => (t.TeamName, t.Rank, t.Wins)), Is.EqualTo(LegacyWithoutProSeeds));
    }

    [Test]
    public void Columns_TwoBlocksStacked_EachReadsItsOwnLabelledRows()
    {
        var sheet = Sheet(
            ["Team Name:", "Alpha", "Bravo"],
            ["Season Standings", "1", "2"],
            ["Team Name:", "Charlie", "Delta"],
            ["Season Standings", "2", "1"]);

        var second = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "Chart", Layout = StandingsLayout.Columns, Anchor = "Team Name:", AnchorNumber = 2 });

        Assert.That(second!.Select(t => (t.TeamName, t.Rank, t.Finish)), Is.EqualTo(SecondBlockOwnRanks), "Its own block's ranks, not the first block's rows");
    }

    [Test]
    public void ABarePlaceHeader_IsTheStandingsPlace_NotTheFinalOne()
    {
        var sheet = Sheet(["Place", "Team", "Wins", "Losses"], ["1", "Alpha", "9", "1"]);

        var team = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "Standings" })!.Single();

        Assert.That((team.Rank, team.Finish), Is.EqualTo(((int?)1, (int?)null)));
    }

    private static (string Name, int? Rank, int? Finish, int? Wins, int? Losses, string? Captain)[] Rows(List<GrifballWebApp.Historical.Import.ArchiveTeam>? teams)
        => [.. teams!.Select(t => (t.TeamName, t.Rank, t.Finish, t.Wins, t.Losses, t.Captain))];

    [Test]
    public void Rows_ATeamAndRecordTable_InStandingsOrder_RanksTeamsByTheirRow()
    {
        // SL20 Console H5's Standings: a title row, then the table
        var sheet = Sheet(
            ["AGLA Console Circuit: Halo 5 Series Standings"],
            ["Team", "Wins", "Losses", "Gaols For", "Goals Against", "Goal Efficiency"],
            ["Junkyard Dogs", "8", "0", "30", "4", "0.88"],
            ["High Voltage", "5", "3", "27", "16", "0.62"],
            ["Impulse", "3", "5", "12", "23", "0.34"]);

        var teams = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "Standings", Ranked = true });

        Assert.That(Rows(teams), Is.EqualTo(new (string, int?, int?, int?, int?, string?)[]
        {
            ("Junkyard Dogs", 1, null, 8, 0, null), ("High Voltage", 2, null, 5, 3, null), ("Impulse", 3, null, 3, 5, null),
        }));
    }

    [Test]
    public void Rows_NotRanked_GivesNoRank()
    {
        var sheet = Sheet(["Team", "Wins", "Losses"], ["Alpha", "1", "2"]);

        Assert.That(StandingsParser.Parse(sheet, new StandingsTab { Sheet = "Standings" })!.Single().Rank, Is.Null);
    }

    [Test]
    public void Rows_ATableBesideOthers_FromItsAnchor_WithTheCaptainInTheTeamsName_StopsAtItsFirstEmptyTeam()
    {
        // WL23's Schedule ListStandings: the standings in G-J beside the schedule, "Team (captain)"
        var sheet = Sheet(
            ["Nov 26 | Sun", "All Times"],
            ["", "week 2", "", "Reschedules", "", "", "Team Standings", "Wins", "Loss", "Games", "", "", "", "OREOTHECAT"],
            ["Dec 9 | Sat", "8:30pm | Mungo vs Oreo", "", "", "Sunday", "", "Mystery Heroes (Holy Morals)", "16", "4", "0", "", "", "Nov 26"],
            ["", "week 3", "", "week 3", "Monday", "", "Glory 2 God (ASpence501)", "14", "4", "2"],
            ["Dec 10 | Sun", "8pm | Oreo vs Death", "", "", "Tuesday", "", "", "", "", "", "", "", "Dec 2"]);

        var teams = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "Schedule ListStandings", Anchor = "Team Standings", Ranked = true, CaptainInName = true });

        Assert.That(Rows(teams), Is.EqualTo(new (string, int?, int?, int?, int?, string?)[]
        {
            ("Mystery Heroes", 1, null, 16, 4, "Holy Morals"), ("Glory 2 God", 2, null, 14, 4, "ASpence501"),
        }));
    }

    [Test]
    public void Rows_SeedAndPlaceColumns_ReadAsOrdinals()
    {
        // A TeamInfo tab's "Seed #" and "Place Finished" (respawn, GWL)
        var sheet = Sheet(
            ["Team Name", "Captain", "Seed #", "Place Finished"],
            ["Alpha", "Ace", "2", "1st"],
            ["Bravo", "Bee", "1", "3rd (Semi-Finals)"]);

        var teams = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "TeamInfo", Ranked = true });

        Assert.That(Rows(teams), Is.EqualTo(new (string, int?, int?, int?, int?, string?)[]
        {
            ("Alpha", 2, 1, null, null, "Ace"), ("Bravo", 1, 3, null, null, "Bee"),
        }), "A seed column wins over the row's order");
    }

    [Test]
    public void Columns_ATeamPerColumn_TakesTheLabelledRows()
    {
        // SL25's Team Chart: a team per column, rows labelled in B
        var sheet = Sheet(
            ["7"],
            ["", "HALO INFINITE Spring Bid League 2025"],
            ["", "Season Standings", "5th", "3rd", "1st"],
            ["", "Playoff Ending", "3rd (Semi-Finals)", "4th (Quarter Finals)", "1st (Season Winner)"],
            ["", "Team Name:", "First Strike", "Clip Hunters", "Chemical Imbalance"],
            ["", "Captain:", "NecessaryEvil", "ASpence501", "Chemical"],
            ["", "Bid 1", "Mavbrid", "Fanciest Pants", "PoisonxPoptart"]);

        var teams = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "Team Chart", Layout = StandingsLayout.Columns, Anchor = "Team Name:" });

        Assert.That(Rows(teams), Is.EqualTo(new (string, int?, int?, int?, int?, string?)[]
        {
            ("First Strike", 5, 3, null, null, "NecessaryEvil"), ("Clip Hunters", 3, 4, null, null, "ASpence501"), ("Chemical Imbalance", 1, 1, null, null, "Chemical"),
        }));
    }

    [Test]
    public void Columns_ARecordRow_IsTheRegularSeasonsWinsAndLosses()
    {
        // H3 Action 2022's Teams: "12-8 (7-2)", the regular season and then the playoffs
        var sheet = Sheet(
            ["Team Name", "Junkyard Dogs (Winners)", "Alpha"],
            ["Record", "12-8 (7-2)", "14-6"]);

        var teams = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "Teams", Layout = StandingsLayout.Columns });

        Assert.That(Rows(teams), Is.EqualTo(new (string, int?, int?, int?, int?, string?)[]
        {
            ("Junkyard Dogs (Winners)", null, null, 12, 8, null), ("Alpha", null, null, 14, 6, null),
        }));
    }

    [Test]
    public void List_ACellPerTeam_UnderItsAnchor_WithItsCaptainAndRecordInBrackets()
    {
        // FL20 PC's Full Schedule: the standings down R beside the calendar
        var sheet = Sheet(
            ["", "", "", "Sunday", "", "Monday", "", "", "", "", "", "", "", "", "", "", "", "Standings"],
            ["", "Week", "", "11", "", "12", "", "", "", "", "", "", "", "", "", "", "", "ZMD (Zeg) (17-3) (95-46)"],
            ["", "1", "", "ALL GAME TIMES ARE EASTERN", "", "", "", "", "", "", "", "", "", "", "", "", "", "MAB (Zim) (15-5) (85-57)"],
            ["", "", "", "", "", "No Games"],
            ["", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "Slytherin (15-1)"]);

        var teams = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "Full Schedule", Layout = StandingsLayout.List, Anchor = "Standings", Ranked = true, CaptainInName = true });

        Assert.That(Rows(teams), Is.EqualTo(new (string, int?, int?, int?, int?, string?)[]
        {
            ("ZMD", 1, null, 17, 3, "Zeg"), ("MAB", 2, null, 15, 5, "Zim"), ("Slytherin", 3, null, 15, 1, null),
        }), "A bracket of a record is no captain");
    }

    [Test]
    public void Rows_APlayerPerRow_IsOneTeamEach_AtItsFirstRow()
    {
        // A TeamInfo tab: a row per player, the team's seed and place on each
        var sheet = Sheet(
            ["Seed #", "Team Name", "Player Name", "Location", "Place Finished"],
            ["Seed 5", "Emerald Isle Imperials", "Imperator Romeo", "Europe", "7th Place"],
            ["Seed 5", "Emerald Isle Imperials", "Ace B S1lentND1", "Central", ""],
            ["Seed 1", "Bravo", "Bee", "West", "1st Place"]);

        var teams = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "TeamInfo" });

        Assert.That(Rows(teams), Is.EqualTo(new (string, int?, int?, int?, int?, string?)[]
        {
            ("Emerald Isle Imperials", 5, 7, null, null, null), ("Bravo", 1, 1, null, null, null),
        }));
    }

    [Test]
    public void Rows_AStandingThatIsARecord_AndADraftSpot_AreNoRank()
    {
        // GWL SL23's TeamInfo: "Seed #" holds the draft spot, "Standing" the record
        var sheet = Sheet(
            ["Seed #", "Team Name", "Player Name", "Place Finished", "Standing", "Wins", "Losses"],
            ["Draft Spot 1", "Rated R", "Mavbrid", "", "15-14", "15", "14"]);

        var teams = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "TeamInfo" });

        Assert.That(Rows(teams), Is.EqualTo(new (string, int?, int?, int?, int?, string?)[] { ("Rated R", null, null, 15, 14, null) }));
    }

    [Test]
    public void Columns_ADraftSheetsStandingsAndPlayoffsRows_AreTheRankAndTheFinish()
    {
        // SL24's Draft Sheet: plain numbers, no place for a team out before the playoffs
        var sheet = Sheet(
            ["", "Season Standings", "7", "4", "1"],
            ["", "Playoffs", "3", "", "1"],
            ["", "Team Name:", "Buff Oily Men (& Women)", "Post Grif Depression", "Crazy Guys"]);

        var teams = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "Draft Sheet", Layout = StandingsLayout.Columns, Anchor = "Team Name:" });

        Assert.That(Rows(teams), Is.EqualTo(new (string, int?, int?, int?, int?, string?)[]
        {
            ("Buff Oily Men (& Women)", 7, 3, null, null, null), ("Post Grif Depression", 4, null, null, null, null), ("Crazy Guys", 1, 1, null, null, null),
        }), "A name's own brackets stay when the tab says nothing of them");
    }

    [Test]
    public void Rows_ANoteUnderTheTable_WithoutANumber_IsNoTeam()
    {
        // WL19's Schedule: a colour key and a heading in the team column, text beside them
        var sheet = Sheet(
            ["Team Name", "Abb.", "Wins", "Losses"],
            ["Str8 Griffin", "Str8", "21", "1"],
            ["Color=Rescheduled", "", "(yellow)"],
            ["Wild Card Standings", "", "", "see below"]);

        var teams = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "Schedule", Ranked = true });

        Assert.That(teams!.Select(t => t.TeamName), Is.EqualTo(Str8GriffinOnly));
    }

    [Test]
    public void TwoTablesSideBySide_TheAnchorNumberPicksOne()
    {
        // SML25's Schedule ListStandings: Pro in B, Legacy in G
        var sheet = Sheet(
            ["", "Pro", "", "", "", "", "Legacy"],
            ["", "Team Standings", "Wins", "Loss", "Games Left", "", "Team Standings", "Wins", "Loss"],
            ["", "Legends Never Die (Snipez)", "10", "5", "0", "", "Fantastic Fourskins (Daedric)", "17", "4"]);

        var pro = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "S", Anchor = "Team Standings", CaptainInName = true });
        var legacy = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "S", Anchor = "Team Standings", AnchorNumber = 2, CaptainInName = true });

        Assert.That((Rows(pro).Single(), Rows(legacy).Single()), Is.EqualTo((("Legends Never Die", (int?)null, (int?)null, (int?)10, (int?)5, "Snipez"),
            ("Fantastic Fourskins", (int?)null, (int?)null, (int?)17, (int?)4, "Daedric"))));
    }

    [Test]
    public void Rows_AHeaderAgainBelow_StartsAnotherDivisionsBlock_RankedFromFirst()
    {
        // WL19's Schedule: a division's standings under the other's
        var sheet = Sheet(
            ["Team Name", "Abb.", "Wins", "Losses"],
            ["Str8 Griffin", "Str8", "21", "1"],
            ["We Respect Hoez", "WRH", "18", "4"],
            ["Team Name", "Abb.", "Wins", "Losses"],
            ["Rooster Bux to Blow", "RBB", "16", "0"]);

        var teams = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "Schedule", Ranked = true });

        Assert.That(teams!.Select(t => (t.TeamName, t.Rank)), Is.EqualTo(new (string, int?)[] { ("Str8 Griffin", 1), ("We Respect Hoez", 2), ("Rooster Bux to Blow", 1) }));
    }

    [Test]
    public void Rows_AHeaderWithNoTeamUnderIt_IsNoTable()
    {
        Assert.That(StandingsParser.Parse(Sheet(["Team", "Wins", "Losses"], ["", "3", "1"]), new StandingsTab { Sheet = "Standings" }), Is.Null);
    }

    [Test]
    public void Rows_ATableOfNamesAndCaptains_TakesTheRowsWithACaptain()
    {
        var sheet = Sheet(["Team", "Captain"], ["Alpha", "Ace"], ["Note", ""], ["Bravo", "Bee"]);

        var teams = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "Rosters" });

        Assert.That(teams!.Select(t => (t.TeamName, t.Captain)), Is.EqualTo(NamesAndCaptains));
    }

    [Test]
    public void Rows_ACaptainColumn_WinsOverTheNamesBracket_AndANegativeCountIsNone()
    {
        var sheet = Sheet(["Team", "Captain", "Wins", "Losses"], ["Mystery Heroes (Holy)", "Holy Morals", "-1", "4"]);

        var teams = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "Standings", CaptainInName = true });

        Assert.That(Rows(teams).Single(), Is.EqualTo(("Mystery Heroes", (int?)null, (int?)null, (int?)null, (int?)4, "Holy Morals")));
    }

    [Test]
    public void Rows_ARecordColumn_OrTheNamesRecord_AndANoteUnderIt()
    {
        var sheet = Sheet(["Team", "Seed", "Record"], ["Alpha (5-3)", "1", "n/a"], ["Bravo", "", "4-4"], ["See the rules", "", "tiebreaker"]);

        var teams = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "Standings" });

        Assert.That(teams!.Select(t => (t.TeamName, t.Wins, t.Losses)), Is.EqualTo(RecordsOfAlphaAndBravo),
            "The Record column's own when it is one, else the name's bracket");
    }

    [Test]
    public void Columns_ABlankBetweenTeams_IsSkipped_AndAnotherTeamNamesRow_OrCell_EndsIt()
    {
        var sheet = Sheet(
            ["Team Name:", "Alpha", "", "Bravo", "Team Name", "Charlie"],
            ["Wins", "3", "", "2", "", "1"],
            ["Team Name:", "Delta"]);

        var teams = StandingsParser.Parse(sheet, new StandingsTab { Sheet = "Chart", Layout = StandingsLayout.Columns });

        Assert.That(teams!.Select(t => t.TeamName), Is.EqualTo(AlphaAndBravo));
    }

    [Test]
    public void NoTable_IsNull()
    {
        Assert.That(StandingsParser.Parse(Sheet(["Schedule"], ["week 1"]), new StandingsTab { Sheet = "Standings" }), Is.Null);
    }

    [TestCase("1st", 1)]
    [TestCase("3rd (Semi-Finals)", 3)]
    [TestCase("12", 12)]
    [TestCase(" 2nd ", 2)]
    [TestCase("T-3", 3)]
    [TestCase("Seed 5", 5)]
    [TestCase("7th Place", 7)]
    [TestCase("Draft Spot 1", null)]
    [TestCase("15-14", null)]
    [TestCase("#N/A", null)]
    [TestCase("Champion", null)]
    [TestCase("", null)]
    [TestCase(null, null)]
    [TestCase("0", null)]
    public void Place_ReadsTheLeadingNumber(string? cell, int? place)
    {
        Assert.That(StandingsParser.Place(cell), Is.EqualTo(place));
    }
}
