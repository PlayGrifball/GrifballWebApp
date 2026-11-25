using static GrifballWebApp.Infrastructure.Test.HistoricalXlsx;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Historical.Parsing;
using GrifballWebApp.Historical.Import;

namespace GrifballWebApp.Historical.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonStatsParserTests
{
    private static HistoricalSourceConfig Fl20Config()
        => HistoricalSourceCatalog.Sources.Single(s => s.HistoricalSourceID == "2020-fl20-pc-schedule-stats").Config!;

    private static readonly (string, decimal?, decimal?, decimal?, decimal?)[] DjBlueLines = [("Mr Epic Beast", 3, 1, 2, 3360), ("oDhmon", 2, 2, 0, 600)];

    [Test]
    public void DjBluesTeamStatistics_LossesAreTheGamesNotWon_TheMedalSectionsMinutesIgnored_AndTheHostsListNotRead()
    {
        // Games and Wins but no Losses; a second Minutes column of only the games with medals; a Neutral Host list below
        var workbook = SheetWorkbook.Read(Workbook(
            ("Team Statistics",
            [
                Row(null, null, "Statistics for the Halo 3 Event"),
                Row("Team", "Player", "Games", "Wins", "Minutes", "Kills", "Deaths", "Medals Recorded?", "Minutes"),
                Row("Twitch Prime", "Mr Epic Beast", 3, 1, 56, 233, 323, 2, 14),
                Row("Twitch Prime", "oDhmon", 2, 2, 10, 291, 225, 2, 4),
                Row("Totals:", null, 5, 3, 66, 524, 548),
                Row("Neutral Host:", 24),
                Row("The Alpha Turtle", 1),
            ])))!;
        var config = new HistoricalSourceConfig
        {
            Season = new SeasonDefaults { Name = "DJBluePDX Summer Series 2021 (Halo 3)", League = "Community/Side", Game = "halo3" },
            Tabs = [new() { Name = "Team Statistics", HeaderRow = 1, StopAt = "Neutral Host", IgnoreColumns = [8], LossesAreGamesNotWon = true }],
        };

        var season = SeasonStatsParser.Parse("djblue", config, workbook, []).Single();

        Assert.That(season.Players.Select(p => (p.Gamertag, p.GamesPlayed, p.Wins, p.Losses, p.TotalSecondsPlayed)), Is.EqualTo(DjBlueLines));
    }

    [Test]
    public void ATabOfGamesAndWinsWithNoLossesColumn_HasNoLosses_UnlessItSaysAGameNotWonIsLost()
    {
        // Games not won may be ties or unfinished games: only a tab that says so counts them as lost
        var workbook = SheetWorkbook.Read(Workbook(("Stats", [Row("Player", "Games", "Wins"), Row("A", 6, 3)])))!;
        var config = new HistoricalSourceConfig { Season = new SeasonDefaults { Name = "Summer League 2021", Game = "halo3" }, Tabs = [new() { Name = "Stats" }] };

        var player = SeasonStatsParser.Parse("s", config, workbook, []).Single().Players.Single();

        Assert.That(player.Losses, Is.Zero);
    }

    [Test]
    public void AWorkbookOfStandingsWithNoStats_WhoseStandingsTabIsMissing_MakesNoSeason()
    {
        var workbook = SheetWorkbook.Read(Workbook(("Schedule", [Row("Team", "Wins"), Row("Alpha", 1)])))!;
        var config = new HistoricalSourceConfig { Season = new SeasonDefaults { Name = "Spring League 2020 (Halo 5 Console)", Game = "halo5" }, Standings = [new() { Sheet = "Rosters" }] };

        Assert.That(SeasonStatsParser.Parse("h5", config, workbook, []), Is.Empty);
    }

    [Test]
    public void AWorkbookOfStandingsWithNoStats_NamingOnlyItsSeason_IsAnOpenAglaSeason()
    {
        var workbook = SheetWorkbook.Read(Workbook(("Rosters", [Row("Team", "Captain"), Row("Alpha", "Ace")])))!;
        var config = new HistoricalSourceConfig { Season = new SeasonDefaults { Name = "Spring League 2020" }, Standings = [new() { Sheet = "Rosters" }] };

        var season = SeasonStatsParser.Parse("s", config, workbook, []).Single();

        Assert.That((season.GameVersion, season.League, season.Division, season.SourceSheet), Is.EqualTo(("", "AGLA", "Open", "Rosters")));
    }

    [TestCase("Games")]
    [TestCase("Wins")]
    public void ATabWhoseGamesNotWonAreLost_WithoutItsGamesOrWins_HasNoLosses(string column)
    {
        var workbook = SheetWorkbook.Read(Workbook(("Stats", [Row("Player", column), Row("A", 3)])))!;
        var config = new HistoricalSourceConfig { Season = new SeasonDefaults { Name = "Summer League 2021", Game = "halo3" }, Tabs = [new() { Name = "Stats", LossesAreGamesNotWon = true }] };

        Assert.That(SeasonStatsParser.Parse("s", config, workbook, []).Single().Players.Single().Losses, Is.Zero);
    }

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

    [Test]
    public void PerGame_ARowThatDidNotPlay_StillNamesTheTeamOfTheRowsBelow()
    {
        var playoffs = Playoffs(
            PlayoffHeader,
            Row("MAB", "ixJaga", 1),
            Row(null, "godwit1vy", 1, null, 1, null, 74, null, 125));

        Assert.That(playoffs.Players.Select(p => (p.Gamertag, p.TeamName)), Is.EqualTo(new[] { ("godwit1vy", (string?)"MAB") }));
    }

    [Test]
    public void SideBySideGamesStart_EndsTheMainTable_WithoutAStopAt()
    {
        var json = Fl20Config().ToJson();
        var config = HistoricalSourceConfig.FromJson(json.Replace("\"stopAt\":\"GAME 1\",", ""))!;
        Assert.That(config.Tabs.Single(t => t.PerGame).StopAt, Is.Null, "the test's own config");
        var workbook = SheetWorkbook.Read(Workbook(("Playoff Game Stats", [
            PlayoffHeader,
            Row("Ascended", "Regresser", 1, 1, null, 2, 30, null, 10),
            Row("GAME 1"),
            Row("W vs ApoPo (5-1) host", "Goals", "Kills", "Deaths"),
            Row("Regresser", 3, 25, 5),
            Row("L vs Ascended (1-5) host", "Goals", "Kills", "Deaths"),
            Row("TheAlphaTurtle", 1, 32, 36),
        ])))!;

        var playoffs = SeasonStatsParser.Parse("2020-fl20-pc-schedule-stats", config, workbook, []).Single(s => s.IsPlayoffs);

        Assert.That(playoffs.Players.Select(p => (p.Gamertag, p.TeamName, p.GamesPlayed)), Is.EquivalentTo(new[]
        {
            ("Regresser", (string?)"Ascended", (decimal?)2),
            ("TheAlphaTurtle", (string?)"ApoPo", (decimal?)1),
        }), "the finals' rows read once, as games");
    }

    private static HistoricalSourceConfig RoundsConfig(string? gamesSheet = "Season Match History")
        => new() { Season = new SeasonDefaults { Name = "Spring League 2025", Game = "infinite" }, Tabs = [new() { Name = "Stats", GamesSheet = gamesSheet }] };

    private static readonly string[] BenchWarmerWarning = ["2025-octane-sl25-bid-stats: no games of 'Bench Warmer' in 'Season Match History'"];
    private static readonly string[] TwoLinesWarning = ["2025-octane-sl25-bid-stats: 'Rayzyr' has 2 lines: their games in 'Season Match History' can't be split"];

    private static readonly object?[] RoundsHeader = Row("Gamertag", "Time Played (Seconds)", "Rounds Won", "Rounds Lost", "Rounds Tied", "goals", "Average Life", "Kills", "Deaths");

    [Test]
    public void RoundsTab_TakesItsGamesFromTheMatchHistory_AndKeepsTheRounds()
    {
        var workbook = SheetWorkbook.Read(Workbook(
            ("Stats",
            [
                RoundsHeader,
                Row("Almighty Tycoon", 1892.4, 9, 4, 1, 3, 15.075, 60, 40),
                Row("Bench Warmer", 0, 0, 0, 0, 0, null, 0, 0),
            ]),
            ("Season Match History",
            [
                Row("BotAttributes", "LastTeamId", "Outcome", "TimePlayed", "PlayerId"),
                Row(null, 1, "2.0", 946.2, "Almighty Tycoon"),
                Row(null, 0, 3, 946.2, "almighty tycoon"),
                Row(null, 1, 1, 946.2, "Almighty Tycoon"),
                Row(null, 1, 2, 946.2, null),
            ])))!;

        var warnings = new List<string>();
        var season = SeasonStatsParser.Parse("2025-octane-sl25-bid-stats", RoundsConfig(), workbook, warnings).Single();
        var tycoon = season.Players.Single(p => p.Gamertag == "Almighty Tycoon");
        var bench = season.Players.Single(p => p.Gamertag == "Bench Warmer");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(warnings, Is.EqualTo(BenchWarmerWarning));
            Assert.That((tycoon.GamesPlayed, tycoon.Wins, tycoon.Losses), Is.EqualTo(((decimal?)3, (decimal?)1, (decimal?)1)), "a tie is a game, neither won nor lost");
            Assert.That((tycoon.TotalSecondsPlayed, tycoon.AverageLifeSeconds, tycoon.Goals), Is.EqualTo(((decimal?)1892, (decimal?)15.075m, (decimal?)3)));
            Assert.That((tycoon.ExtendedMetrics!.Value.GetProperty("roundsWon").GetDecimal(), tycoon.ExtendedMetrics.Value.GetProperty("roundsLost").GetDecimal(),
                tycoon.ExtendedMetrics.Value.GetProperty("roundsTied").GetDecimal()), Is.EqualTo((9m, 4m, 1m)), "rounds are not games: kept as they are");
            Assert.That((bench.GamesPlayed, bench.Wins, bench.Losses), Is.EqualTo(((decimal?)0, (decimal?)0, (decimal?)0)), "no games in the history");
        }
    }

    [Test]
    public void GamesTabHeadedAsAStatsTab_CountsTheGames_ButTheTabsOwnWinsAndLossesStay()
    {
        var workbook = SheetWorkbook.Read(Workbook(
            ("Stats",
            [
                Row("Name", "Wins", "Losses", "Goals", "Goal Assists", "Kills"),
                Row("Solace VIII", 10, 5, 18, 2, 1204),
            ]),
            ("Hist",
            [
                Row("Name", "Wins", "Losses", "Goals"),
                Row("Solace VIII", 1, 0, 1),
                Row("Solace VIII", 0, 1, 0),
                Row("Solace VIII", 0, 0, 0),
            ])))!;

        var player = SeasonStatsParser.Parse("2024-igl-evolved-fl24-stats", RoundsConfig("Hist"), workbook, []).Single().Players.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((player.GamesPlayed, player.Wins, player.Losses), Is.EqualTo(((decimal?)3, (decimal?)10, (decimal?)5)));
            Assert.That(player.ExtendedMetrics!.Value.GetProperty("goalAssists").GetDecimal(), Is.EqualTo(2m));
        }
    }

    [TestCase(null, "no sheet 'Season Match History'")]
    [TestCase("Name", "'Season Match History' has no player and result columns")]
    [TestCase("Outcome", "'Season Match History' has no player and result columns")]
    public void AGamesTabThatIsMissingOrHasNoResults_IsAWarning_AndTheGamesStay0(string? header, string warning)
    {
        (string, object?[][])[] sheets = header is null
            ? [("Stats", [RoundsHeader, Row("Almighty Tycoon", 1892, 9, 4, 1)])]
            : [("Stats", [RoundsHeader, Row("Almighty Tycoon", 1892, 9, 4, 1)]), ("Season Match History", [Row(header), Row("Almighty Tycoon")])];
        var workbook = SheetWorkbook.Read(Workbook(sheets))!;

        var warnings = new List<string>();
        var player = SeasonStatsParser.Parse("2025-octane-sl25-bid-stats", RoundsConfig(), workbook, warnings).Single().Players.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(warnings, Is.EqualTo(new[] { $"2025-octane-sl25-bid-stats: {warning}" }));
            Assert.That((player.GamesPlayed, player.Wins, player.Losses), Is.EqualTo(((decimal?)0, (decimal?)0, (decimal?)0)));
        }
    }

    [Test]
    public void AveragesPerGame_AreTotalledOverTheGames_AndAverageLifeInSecondsIsRead()
    {
        var workbook = SheetWorkbook.Read(Workbook(
            ("Stats",
            [
                Row("Name", "SUM of Games Played", "AVG Score", "AVG Damage Dealt", "AVG Damage Taken", "Average Life (Seconds)"),
                Row("ACE B S1lentND1", 23, 6944.565217, 2504.521739, 1760.086957, 10.8),
                Row("No Games", 0, 5000, 2000, 1000, null),
            ])))!;

        var players = SeasonStatsParser.Parse("2024-da-sl24-stats", RoundsConfig(null), workbook, []).Single().Players;

        using (Assert.EnterMultipleScope())
        {
            Assert.That((players[0].Score, players[0].DamageDealt, players[0].DamageTaken, players[0].AverageLifeSeconds),
                Is.EqualTo(((decimal?)159725, (decimal?)57604, (decimal?)40482, (decimal?)10.8m)));
            Assert.That((players[1].Score, players[1].DamageDealt, players[1].DamageTaken), Is.EqualTo(((decimal?)null, (decimal?)null, (decimal?)null)),
                "an average over no games is no total");
            Assert.That(players[0].ExtendedMetrics, Is.Null);
        }
    }

    [Test]
    public void ATotalColumn_WinsOverItsAverage()
    {
        var columns = SeasonStatsParser.ColumnsOf(["Name", "Games", "Score", "AVG Score"]);

        var player = SeasonStatsParser.Player(["Ace", "2", "100", "70"], columns, "Ace", null, null, null, 1);

        Assert.That(player.Score, Is.EqualTo(100m));
    }

    [Test]
    public void ExtendedStats_JoinATablesExtendedMetrics()
    {
        var columns = SeasonStatsParser.ColumnsOf(["Name", "Rounds Won"]);
        var table = System.Text.Json.JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["battingAverage"] = 45.6m, ["roundsWon"] = 1m });

        var player = SeasonStatsParser.Player(["Ace", "7"], columns, "Ace", null, null, null, 1, table);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(player.ExtendedMetrics!.Value.GetProperty("roundsWon").GetDecimal(), Is.EqualTo(7m), "the sheet's column");
            Assert.That(player.ExtendedMetrics!.Value.GetProperty("battingAverage").GetDecimal(), Is.EqualTo(45.6m));
        }
    }

    [Test]
    public void NoExtendedStats_KeepATablesExtendedMetrics()
    {
        var columns = SeasonStatsParser.ColumnsOf(["Name", "Kills"]);
        var table = System.Text.Json.JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["grabs"] = 3m });

        var player = SeasonStatsParser.Player(["Ace", "7"], columns, "Ace", null, null, null, 1, table);

        Assert.That(player.ExtendedMetrics!.Value.GetProperty("grabs").GetDecimal(), Is.EqualTo(3m));
    }

    [Test]
    public void APlayerWithTwoLines_GetsNoGames_TheGamesTabCantSplitThem()
    {
        var workbook = SheetWorkbook.Read(Workbook(
            ("Stats",
            [
                RoundsHeader,
                Row("Rayzyr (to Tuna)", 900, 4, 2, 0),
                Row("Rayzyr", 900, 3, 3, 0),
            ]),
            ("Season Match History",
            [
                Row("Outcome", "PlayerId"),
                Row(2, "Rayzyr"),
                Row(3, "Rayzyr"),
            ])))!;

        var warnings = new List<string>();
        var players = SeasonStatsParser.Parse("2025-octane-sl25-bid-stats", RoundsConfig(), workbook, warnings).Single().Players;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(players.Select(p => (p.StintIndex, p.GamesPlayed, p.Wins, p.Losses)), Is.EqualTo(new[] { (1, (decimal?)0, (decimal?)0, (decimal?)0), (2, (decimal?)0, (decimal?)0, (decimal?)0) }));
            Assert.That(warnings, Is.EqualTo(TwoLinesWarning));
        }
    }

    [Test]
    public void LegacyHeaders_TimeMinutes_RecordCellsAndTeamSectionRows_AreHandled()
    {
        var workbook = SheetWorkbook.Read(Workbook(
            ("Rosters",
            [
                Row(null, "Teams", null, null, null, null, "Wins", null, "Losses"),
                Row(null, "ThunderCats", null, "ixLiono", "ixGingy", null, 9, null, 1),
                Row(null, null, null, "Captain"),
            ]),
            ("Stats",
            [
                Row("Pro Players", "Team", "Total Games", "Games Won", "Games Lost", "Time (m)", "Avg Life (S)", "Goals For", "Kills", "Ham Kills", "Ball    Kills"),
                Row("Jankyard Doogs"),
                Row("ixLiono", null, 3, 2, 1, 60, 14.0, 5, 200, 150, 8),
                Row("Pro Players", null),
                Row("ixGingy", "8-2", 5, 4, 1, 95, 11.2, 6, 300, 250, 12),
                Row("SubPlayer", "Various Teams", 1, 0, 1, 15, 9.0, 1, 40, 30, 1),
            ])))!;

        var config = new HistoricalSourceConfig
        {
            Season = new SeasonDefaults { Name = "Winter League 2020 (Reach PC)", League = "AGLA", Game = "reach" },
            RosterSheets = ["Rosters"],
            Tabs = [new() { Name = "Stats", HeaderRow = 0, CarryTeam = true, TeamSectionRows = true }],
            TeamNames = new() { ["Jankyard Doogs"] = "ThunderCats", ["Various Teams"] = "" },
        };

        var players = SeasonStatsParser.Parse("wl20", config, workbook, []).Single().Players;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(players.Select(p => (p.Gamertag, p.TeamName, p.TotalSecondsPlayed, p.BallKills, p.HammerKills)),
                Is.EqualTo(new[]
                {
                    ("ixLiono", (string?)"ThunderCats", (decimal?)3600m, (decimal?)8m, (decimal?)150m),
                    ("ixGingy", (string?)"ThunderCats", (decimal?)5700m, (decimal?)12m, (decimal?)250m),
                    ("SubPlayer", (string?)null, (decimal?)900m, (decimal?)1m, (decimal?)30m),
                }));
        }
    }

    [Test]
    public void TeamSectionRows_WithoutCarryTeam_LeaveTheTeamToTheRoster_AndANameForNoTeamIsALine()
    {
        var workbook = SheetWorkbook.Read(Workbook(
            ("Rosters",
            [
                Row(null, "Teams", null, null, null, null, "Wins", null, "Losses"),
                Row(null, "ThunderCats", null, "ixLiono", "ixGingy", null, 9, null, 1),
                Row(null, null, null, "Captain"),
            ]),
            ("Stats",
            [
                Row("Pro Players", "Team", "Total Games"),
                Row("ThunderCats"),
                Row("ixLiono", null, 3),
                Row("Subs", null, 2),
                Row("Gone", null, 1),
            ])))!;

        var config = new HistoricalSourceConfig
        {
            Season = new SeasonDefaults { Name = "Winter League 2020 (Reach PC)", League = "AGLA", Game = "reach" },
            RosterSheets = ["Rosters"],
            Tabs = [new() { Name = "Stats", HeaderRow = 0, TeamSectionRows = true }],
            TeamNames = new() { ["Gone"] = "" },
        };

        var players = SeasonStatsParser.Parse("wl20", config, workbook, []).Single().Players;

        Assert.That(players.Select(p => (p.Gamertag, p.TeamName)),
            Is.EqualTo(new[] { ("ixLiono", (string?)"ThunderCats"), ("Subs", null), ("Gone", null) }));
    }

    [Test]
    public void RosterParser_TeamStatBlocks_ReadsPlayersAboveGoalsRows()
    {
        var workbook = SheetWorkbook.Read(Workbook(
            ("Impulse",
            [
                Row("Impulse"),
                Row(),
                Row("iGalvanator", "Totals:"),
                Row("Goals:", 11),
                Row("KID TONGA 1"),
                Row("Goals:", 46),
            ]),
            ("Overall",
            [
                Row("Pro Players", "Total Games", "Kills", "Deaths"),
                Row("iGalvanator", 13, 784, 604),
                Row("KID TONGA 1", 15, 446, 609),
            ])))!;

        var config = new HistoricalSourceConfig
        {
            Season = new SeasonDefaults { Name = "Summer League 2016", League = "AGLA", Game = "halo5" },
            RosterSheets = ["Impulse"],
            Tabs = [new() { Name = "Overall", HeaderRow = 0 }],
        };

        var players = SeasonStatsParser.Parse("sml16", config, workbook, []).Single().Players;
        Assert.That(players.Select(p => (p.Gamertag, p.TeamName)), Is.EqualTo(new[] { ("iGalvanator", "Impulse"), ("KID TONGA 1", "Impulse") }));
    }

    [Test]
    public void Sml26Columns_AndCrossSourceGameRecordSheets_AreParsed()
    {
        var statsContent = Workbook(("Regular Season Stats Sheet",
        [
            Row("Gamertag", "Time Played (Minutes)", "Avg Life (Seconds)", "Rounds Won", "Rounds Lost", "Kills", "Deaths"),
            Row("Ace", 10, 12.5, 5, 2, 20, 10),
        ]));
        var historyContent = Workbook(("Season Match History",
        [
            Row("FirstJoinedTime", "PlayerId", "Outcome"),
            Row("2026-11-17T01:36:12.827Z", "Ace", 2),
            Row("2026-11-19T01:37:35.479Z", "Ace", 3),
        ]));

        var statsSource = new HistoricalSourceContent(HistoricalTestData.Source("stats", "https://example.org/stats",
            new HistoricalSourceConfig
            {
                Season = new SeasonDefaults { Name = "Winter League 2026 (Pro)", League = "AGLA", Game = "infinite", Division = "Pro" },
                Tabs = [new() { Name = "Regular Season Stats Sheet", GamesSheet = "history/Season Match History" }],
            }.ToJson()), 1, statsContent);
        var historySource = new HistoricalSourceContent(HistoricalTestData.Source("history", "https://example.org/history",
            new HistoricalSourceConfig { GameRecordSheets = ["Season Match History"] }.ToJson()), 2, historyContent);

        var build = HistoricalArchiveBuilder.Build([statsSource, historySource], new HashSet<string> { "history" });
        var season = build.Input.Seasons.Seasons.Single();
        var player = season.Players.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(build.Warnings, Is.Empty);
            Assert.That(build.Seasons, Is.EquivalentTo(ExpectedStatsSeasonKey));
            Assert.That((player.GamesPlayed, player.Wins, player.Losses, player.TotalSecondsPlayed, player.AverageLifeSeconds),
                Is.EqualTo(((int?)2, (int?)1, (int?)1, (decimal?)600m, (decimal?)12.5m)));
            Assert.That((season.FirstPlayedAt?.Date, season.LastPlayedAt?.Date),
                Is.EqualTo(((DateTime?)new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc), (DateTime?)new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc))));
        }
    }

    [Test]
    public void CrossSourceGameRecordSheet_NoSourceHasIt_IsWarnedAbout()
    {
        var statsContent = Workbook(("Regular Season Stats Sheet",
        [
            Row("Gamertag", "Kills", "Deaths"),
            Row("Ace", 20, 10),
        ]));
        var statsSource = new HistoricalSourceContent(HistoricalTestData.Source("stats", "https://example.org/stats",
            new HistoricalSourceConfig
            {
                Season = new SeasonDefaults { Name = "Winter League 2026 (Pro)", League = "AGLA", Game = "infinite", Division = "Pro" },
                Tabs = [new() { Name = "Regular Season Stats Sheet", GamesSheet = "history/Season Match History" }],
            }.ToJson()), 1, statsContent);

        var build = HistoricalArchiveBuilder.Build([statsSource]);

        Assert.That(build.Warnings, Has.Some.Contains("no game records source 'history/Season Match History'"));
    }

    [Test]
    public void ATabWithNoSeasonDefaults_IsNamedForItsSource_AndAGamesSheetOfAnotherSource_NeedsNoPendingList()
    {
        var workbook = SheetWorkbook.Read(Workbook(("Stats", [Row("Player", "Kills"), Row("A", 6)])))!;
        var config = new HistoricalSourceConfig { Tabs = [new() { Name = "Stats", GamesSheet = "history/Matches" }] };

        var season = SeasonStatsParser.Parse("src", config, workbook, []).Single();

        Assert.That((season.SeasonName, season.GameVersion, season.League, season.Division, season.Titles), Is.EqualTo(("src", "", "AGLA", "Open", (string?)null)));
    }

    [Test]
    public void ATabWithItsOwnSeason_DoesNotTakeTheDefaultsTitles()
    {
        var workbook = SheetWorkbook.Read(Workbook(("Stats", [Row("Player", "Kills"), Row("A", 6)])))!;
        var config = new HistoricalSourceConfig
        {
            Season = new SeasonDefaults { Name = "Default", Game = "infinite", Titles = "Champions" },
            Tabs = [new() { Name = "Stats", CustomSeason = "Other" }],
        };

        var season = SeasonStatsParser.Parse("src", config, workbook, []).Single();

        Assert.That((season.SeasonName, season.Titles), Is.EqualTo(("Other", (string?)null)));
    }

    private static readonly string[] ExpectedStatsSeasonKey = ["stats/Regular Season Stats Sheet"];
}
