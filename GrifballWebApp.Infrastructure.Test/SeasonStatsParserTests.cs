using static GrifballWebApp.Infrastructure.Test.HistoricalXlsx;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Infrastructure.Historical.Parsing;
using GrifballWebApp.Infrastructure.Historical.Sources;

namespace GrifballWebApp.Infrastructure.Test;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SeasonStatsParserTests
{
    private static HistoricalSourceConfig Fl20Config()
        => HistoricalSourceCatalog.Sources.Single(s => s.HistoricalSourceID == "2020-fl20-pc-schedule-stats").Config!;

    [Test]
    public void CarryTeam_FillsTeamDownBlock_AndResetsOnTotalsAndExcludedTeams()
    {
        var workbook = SheetWorkbook.Read(Workbook(
            ("Stats",
            [
                Row("Team", "Player", "Games", "Wins", "Losses", "Goals", "Kills", "Assists", "Deaths"),
                Row("Ascended", "Regresser", 20, 11, 9, 32, 1667, 23, 1714),
                Row(null, "Sabasauros Rex", 20, 11, 9, 32, 1633, 14, 1436),
                Row("44144.0", "Totals:", 20, 11, 9, 80, 6443, 58, 6422),
                Row("Subs", "Tommy/Sam", 14, 4, 10, 2, 327, 5, 389),
                Row(null, "FreeAgent", 1, 0, 1, 0, 10, 0, 12),
            ])))!;

        var warnings = new List<string>();
        var seasons = SeasonStatsParser.Parse("2020-fl20-pc-schedule-stats", Fl20Config(), workbook, warnings);
        var reg = seasons.Single(s => !s.IsPlayoffs);

        Assert.That(reg.Players.Select(p => (p.Gamertag, p.TeamName, p.GamesPlayed)), Is.EqualTo(new[]
        {
            ("Regresser", (string?)"Ascended", (decimal?)20),
            ("Sabasauros Rex", (string?)"Ascended", (decimal?)20),
            ("Tommy/Sam", (string?)null, (decimal?)14),
            ("FreeAgent", (string?)null, (decimal?)1),
        }));
    }

    [Test]
    public void PerGameWithStopAtAndSideBySide_AggregatesPlayoffGames_IncludesFinals_AndIgnoresMvpTable()
    {
        var workbook = SheetWorkbook.Read(Workbook(
            ("Playoff Game Stats",
            [
                Row("Team", "Player", "Games", "Wins", "Losses", "Goals", "Kills", "Assists", "Deaths", "Stops", "Sprees"),
                Row("A&tS", "oDhmon", 1, 1, null, null, 51, null, 51, null, 4),
                Row("ASP vs ASC Game 1"),
                Row("Ascended", "Regresser", 1, null, 1, null, 44, 2, 55, null, 4),
                Row(null, "Sabasauros Rex", 1, null, 1, 1, 55, 1, 48, null, 6),
                Row("ASP 5-1"),
                Row("Team", "Player", "Games", "Wins", "Losses", "Goals", "Kills", "Assists", "Deaths", "Stops", "Sprees"),
                Row("MAB", "ixJaga", 1), // DNP row (Games=1 but no other stats)
                Row(null, "godwit1vy", 1, null, 1, null, 74, null, 125, 1, null),
                Row("MAB vs ASC Game 1"),
                Row("Ascended", "Regresser", 1, 1, null, 4, 64, null, 71, 4, 1),
                Row(null, "Sabasauros Rex", 1, 1, null, null, 137, 1, 100, 3, 7),
                Row("GAME 1", null, null, null, null, null, null, null, null, null, null, "Game 2"),
                Row("W vs ApoPo (5-1) Doer host", null, "Goals", "Kills", "Assist", "Deaths", "Stops", "Sprees", null, null, null, "W vs ApoPo (5-3) Rage host", null, "Goals", "Kills", "Assist", "Deaths", "Stops", "Sprees"),
                Row("Regresser", null, 5, 29, 1, 29, 2, 1, null, null, null, "Regresser", null, 4, 29, null, 40, 1, null),
                Row("Sabasauros Rex", null, null, 45, null, 37, 3, 2, null, null, null, "Sabasauros Rex", null, null, 76, null, 48, 3, 5),
                Row("Credit Clown"),
                Row("L vs ASC (1-5) Doer host", null, "Goals", "Kills", "Assist", "Deaths", "Stops", "Sprees", null, null, null, "L vs Ascended (3-5) Rage host", null, "Goals", "Kills", "Assist", "Deaths", "Stops", "Sprees"),
                Row("TheAlphaTurtle", null, 1, 32, 1, 36, 1, 3, null, null, null, "TheAlphaTurtle", null, 2, 46, null, 53, 3, 1),
                Row(null, null, "Tank", "Hybrid", "Runner", "MVP (Tank)", "MVP (Hybrid)", "MVP (Runner)"),
                Row(null, "Regresser", 35, 94.5, 195, 52.5, 141.75, 292.5),
                Row(null, "Sab", 89.5, 71.25, 30, 134.25, 106.875, 45),
            ])))!;

        var warnings = new List<string>();
        var seasons = SeasonStatsParser.Parse("2020-fl20-pc-schedule-stats", Fl20Config(), workbook, warnings);
        var playoffs = seasons.Single(s => s.IsPlayoffs);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(playoffs.Players.Any(p => p.Gamertag == "Sab"), Is.False, "MVP table must not be parsed");
            Assert.That(playoffs.Players.Any(p => p.Gamertag == "ixJaga"), Is.False, "DNP row must be skipped");
            Assert.That(playoffs.Players.Any(p => p.Gamertag == "Credit Clown"), Is.False, "DNP finals row must be skipped");

            var sab = playoffs.Players.Single(p => p.Gamertag == "Sabasauros Rex");
            Assert.That((sab.TeamName, sab.GamesPlayed, sab.Wins, sab.Losses, sab.Goals, sab.Kills, sab.Assists, sab.Deaths, sab.CarrierKills, sab.Sprees),
                Is.EqualTo(("Ascended", (decimal?)4, (decimal?)3, (decimal?)1, (decimal?)1, (decimal?)313, (decimal?)2, (decimal?)233, (decimal?)9, (decimal?)20)));

            var reg = playoffs.Players.Single(p => p.Gamertag == "Regresser");
            Assert.That((reg.TeamName, reg.GamesPlayed, reg.Wins, reg.Losses, reg.Goals, reg.Kills, reg.Assists, reg.Deaths),
                Is.EqualTo(("Ascended", (decimal?)4, (decimal?)3, (decimal?)1, (decimal?)13, (decimal?)166, (decimal?)3, (decimal?)195)));

            var alpha = playoffs.Players.Single(p => p.Gamertag == "TheAlphaTurtle");
            Assert.That((alpha.TeamName, alpha.GamesPlayed, alpha.Wins, alpha.Losses, alpha.Goals, alpha.Kills, alpha.Assists, alpha.Deaths),
                Is.EqualTo(("ApoPo", (decimal?)2, (decimal?)0, (decimal?)2, (decimal?)3, (decimal?)78, (decimal?)1, (decimal?)89)));
        }
    }

    private static readonly object?[] PlayoffHeader = Row("Team", "Player", "Games", "Wins", "Losses", "Goals", "Kills", "Assists", "Deaths");

    private static ArchiveSeason Playoffs(params object?[][] rows)
    {
        var workbook = SheetWorkbook.Read(Workbook(("Playoff Game Stats", rows)))!;
        return SeasonStatsParser.Parse("2020-fl20-pc-schedule-stats", Fl20Config(), workbook, []).Single(s => s.IsPlayoffs);
    }

    [Test]
    public void PerGameWithoutSideBySideGames_AggregatesTheRows_KdRatioNullWithoutDeaths()
    {
        var playoffs = Playoffs(
            PlayoffHeader,
            Row("Ascended", "Regresser", 1, 1, null, 2, 30, null, 10),
            Row(null, "Regresser", 1, null, 1, 1, 20, null, 0),
            Row("Ascended", "Sabasauros Rex", 1, 1, null, null, 15, null, 0));

        var regresser = playoffs.Players.Single(p => p.Gamertag == "Regresser");
        var sab = playoffs.Players.Single(p => p.Gamertag == "Sabasauros Rex");
        using (Assert.EnterMultipleScope())
        {
            Assert.That((regresser.TeamName, regresser.GamesPlayed, regresser.Wins, regresser.Losses, regresser.Goals, regresser.Kills, regresser.Deaths, regresser.KdRatio),
                Is.EqualTo(("Ascended", (decimal?)2, (decimal?)1, (decimal?)1, (decimal?)3, (decimal?)50, (decimal?)10, (decimal?)5)));
            Assert.That((sab.Deaths, sab.KdRatio), Is.EqualTo(((decimal?)0, (decimal?)null)), "no deaths: no ratio");
        }
    }

    [Test]
    public void PerGameOneSideBySideSection_TeamFromNoOpponent_ReadsSumOfHeaders_AndSkipsBlankRowsAndStatlessBlocks()
    {
        var playoffs = Playoffs(
            PlayoffHeader,
            Row(" "),
            Row("GAME 1"),
            Row("Played on a Sunday"),
            Row("W vs Ascended (5-1) host", "Goals", "Sum of Kills", "Deaths", "Score", null, "L vs Nobody (0-5)", "Notes"),
            Row("Lone Wolf", 3, 25, 0, 900),
            Row(" "),
            Row("Pack Rat", null, 12, 4));

        var wolf = playoffs.Players.Single(p => p.Gamertag == "Lone Wolf");
        var rat = playoffs.Players.Single(p => p.Gamertag == "Pack Rat");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(playoffs.Players, Has.Count.EqualTo(2), "the Notes block has no stat columns: no players of its own");
            Assert.That((wolf.TeamName, wolf.GamesPlayed, wolf.Wins, wolf.Losses, wolf.Goals, wolf.Kills, wolf.Deaths, wolf.KdRatio, wolf.Score),
                Is.EqualTo(((string?)null, (decimal?)1, (decimal?)1, (decimal?)0, (decimal?)3, (decimal?)25, (decimal?)0, (decimal?)null, (decimal?)900)),
                "one section: no other side to name the team after, and no roster");
            Assert.That((rat.Kills, rat.Deaths, rat.KdRatio, rat.Score), Is.EqualTo(((decimal?)12, (decimal?)4, (decimal?)3, (decimal?)null)));
        }
    }

    [Test]
    public void PerGameSideBySideStartLast_AddsNoGames()
    {
        var playoffs = Playoffs(
            PlayoffHeader,
            Row("Ascended", "Regresser", 1, 1, null, 2, 30, null, 10),
            Row("GAME 1"));

        Assert.That(playoffs.Players.Select(p => (p.Gamertag, p.GamesPlayed)), Is.EqualTo(new[] { ("Regresser", (decimal?)1) }));
    }

    [Test]
    public void SideBySideFinals_PlayerOnATeamAlready_KeepsIt_OverTheOpponentsShortName()
    {
        var playoffs = Playoffs(
            PlayoffHeader,
            Row("Ascended", "Regresser", 1, 1, null, 2, 30, null, 10),
            Row("GAME 1"),
            Row("W vs ApoPo (5-1) host", "Goals", "Kills", "Deaths"),
            Row("Regresser", 3, 25, 5),
            Row("L vs ASC (1-5) host", "Goals", "Kills", "Deaths"),
            Row("TheAlphaTurtle", 1, 32, 36));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(playoffs.Players.Select(p => (p.Gamertag, p.TeamName, p.GamesPlayed)), Is.EquivalentTo(new[]
            {
                ("Regresser", (string?)"Ascended", (decimal?)2),
                ("TheAlphaTurtle", (string?)"ApoPo", (decimal?)1),
            }), "Regresser's team from the main table, not 'ASC' from the other side's header: one stint, no made-up team");
        }
    }
}
