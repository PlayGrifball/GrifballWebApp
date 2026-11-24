using static GrifballWebApp.Infrastructure.Test.HistoricalXlsx;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Historical.Parsing;
using GrifballWebApp.Historical.Import;

namespace GrifballWebApp.Historical.Test;

/// <summary>
/// The website databases' game tables (GamesTableParser) as recorded games and forfeits, read with the catalog's own
/// config of each: the grifball.info backup against a small workbook shaped like it (HistoricalXlsx.GrifballInfo), the
/// grifball.com archive against one built here; never the real ones.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GamesTableParserTests
{
    private const string InfoId = "history-grifball-info-db";
    private const string ComId = "history-grifball-com-db";

    private static readonly string[] ComSheets = ["Matches", "Match_Players"];
    private static readonly string[] InfoSheets = ["gb_teamstats", "gb_deletedgames", "h_games", "h4_pstats", "agla_playerstats", "agla_teamindex", "agla_gamesreported", "agla_deletedgames"];
    private static readonly string[] Sml11Rosters = ["Sarge, Simmons", "Church, Grif, Tucker"];
    private static readonly int[] NoLines = [0, 0];
    private static readonly string[] OnlyBee = ["Bee"];

    private static HistoricalSourceConfig CatalogConfig(string id)
        => HistoricalSourceCatalog.Sources.Single(s => s.HistoricalSourceID == id).Config!;

    private static HistoricalArchiveBuild Build(string id, byte[] workbook, HistoricalSourceConfig? config = null)
        => HistoricalArchiveBuilder.Build([new HistoricalSourceContent(
            HistoricalTestData.Source(id, "https://example.org/" + id, (config ?? CatalogConfig(id)).ToJson()), 1, workbook)]);

    private static List<RecordedGameInput> Games(HistoricalArchiveBuild build, string table)
        => build.Input.RecordedGames!.Where(g => g.SourceKey.Contains($"/{table}#", StringComparison.Ordinal)).ToList();

    private static List<ArchiveForfeitInput> Forfeits(HistoricalArchiveBuild build, string table)
        => build.Input.Forfeits!.Where(g => g.SourceKey.Contains($"/{table}#", StringComparison.Ordinal)).ToList();

    private static (int, string?, Outcomes, int?)[] Sides(RecordedGameInput game) => game.Teams.Select(t => (t.Side, t.Name, t.Outcome, t.Score)).ToArray();

    [Test]
    public void TheCatalogEntries_ReadTheirGameTables()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(CatalogConfig(ComId).SheetNames(), Is.SupersetOf(ComSheets));
            Assert.That(CatalogConfig(InfoId).SheetNames(), Is.SupersetOf(InfoSheets));
        }
    }

    [Test]
    public void TeamRows_AGameIsItsTwoRows_InGameOrder_AForfeitIsAForfeit_AndTheRestIsReported()
    {
        var build = Build(InfoId, GrifballInfo());
        var games = Games(build, "gb_teamstats");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(g => (g.SourceKey, g.SeasonSourceId, g.GameVersion, g.IsPlayoffs)), Is.EqualTo(new[]
            {
                ($"{InfoId}/gb_teamstats#100", $"{InfoId}/hr_pstats#s13", "reach", false),
                ($"{InfoId}/gb_teamstats#101", $"{InfoId}/hr_pstats#s13", "reach", true),
                ($"{InfoId}/gb_teamstats#104", $"{InfoId}/hr_pstats#s12_Pro", "reach", false),
            }), "The deleted game 103 is left out; a season split by division takes the first team's");
            Assert.That(Sides(games[0]), Is.EqualTo(new[] { (1, (string?)"Way to Fail", Outcomes.Won, (int?)6), (2, "Red Ray", Outcomes.Lost, 3) }));
            Assert.That(Sides(games[1]), Is.EqualTo(new[] { (1, (string?)"Way to Fail", Outcomes.Lost, (int?)2), (2, "Red Ray", Outcomes.Won, 5) }));
            Assert.That(games.SelectMany(g => g.Players), Is.Empty, "gb_teamstats has no player lines");
            Assert.That(Forfeits(build, "gb_teamstats").Select(f => (f.SourceKey, f.Winner, f.Loser, f.BothForfeited)), Is.EqualTo(new[]
            {
                ($"{InfoId}/gb_teamstats#102", "Way to Fail", "Red Ray", false),
            }));
            Assert.That(build.Warnings, Is.SupersetOf(new[]
            {
                $"{InfoId}: gb_teamstats: 1 game(s) of a team in none of its season's divisions, skipped",
                $"{InfoId}: gb_teamstats: 3 row(s) without both sides or a result, skipped",
            }));
        }
    }

    [Test]
    public void Schedule_PlayedGamesAndForfeits_TheirPlayersTheStatsRowsOfTheGamesLink()
    {
        var build = Build(InfoId, GrifballInfo());
        var game = Games(build, "h_games").Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((game.SourceKey, game.SeasonSourceId, game.GameVersion, game.PlayedAt), Is.EqualTo(
                ($"{InfoId}/h_games#1", $"{InfoId}/h4_pstats#s20", "halo4", (DateTime?)new DateTime(2013, 1, 26, 11, 43, 32, DateTimeKind.Utc))));
            Assert.That(game.PlayedAt!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(Sides(game), Is.EqualTo(new[] { (1, (string?)"Four Guys One Ball", Outcomes.Lost, (int?)2), (2, "Hybrid Theory", Outcomes.Won, 7) }),
                "The result is the winner's score first");
            Assert.That(game.Players.Select(p => (p.Gamertag, p.Side, p.Kills, p.Deaths, p.Goals)), Is.EqualTo(new[]
            {
                ("Crusader0102", 1, 30, 20, (int?)2), ("TrueFlyingCow", 1, 20, 22, (int?)1),
            }), "Gamertags from h_players, the side by the line's team");
            Assert.That(Forfeits(build, "h_games").Select(f => (f.SourceKey, f.Winner, f.Loser, f.BothForfeited, f.PlayedAt)), Is.EqualTo(new[]
            {
                ($"{InfoId}/h_games#2", "Four Guys One Ball", "Hybrid Theory", false, (DateTime?)new DateTime(2013, 1, 27, 10, 0, 0, DateTimeKind.Utc)),
                ($"{InfoId}/h_games#5", "Hybrid Theory", "Four Guys One Ball", true, null),
            }));
            Assert.That(build.Warnings, Is.SupersetOf(new[]
            {
                $"{InfoId}: h_games: 1 row(s) without both sides or a result, skipped",
                $"{InfoId}: h_games: 2 player line(s) of no listed game or of neither side, skipped",
            }), "An unplayed game is no game, and not reported");
        }
    }

    [Test]
    public void Reported_TheWinnersAndLosersGames_WithTheirPlayersAndDates_AndTheirForfeits()
    {
        var build = Build(InfoId, GrifballInfo());
        var games = Games(build, "agla_gamesreported");
        const string sml11 = $"{InfoId}/agla_playerstats#sSML11_Amateur";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(g => (g.SourceKey, g.SeasonSourceId, g.GameVersion, g.IsPlayoffs, g.PlayedAt)), Is.EqualTo(new[]
            {
                ($"{InfoId}/agla_gamesreported#1", sml11, "reach", false, (DateTime?)new DateTime(2011, 7, 26, 2, 25, 53, DateTimeKind.Utc)),
                ($"{InfoId}/agla_gamesreported#2", sml11, "reach", true, (DateTime?)new DateTime(2011, 7, 27, 2, 40, 35, DateTimeKind.Utc)),
            }), "The deleted game and the second report of a game are left out");
            Assert.That(Sides(games[0]), Is.EqualTo(new[] { (1, (string?)"Red Army", Outcomes.Won, (int?)null), (2, "Blue Base", Outcomes.Lost, null) }));
            var sarge = games[0].Players[0];
            Assert.That(games[0].Players.Select(p => (p.Gamertag, p.Side)), Is.EqualTo(new[] { ("Sarge", 1), ("Grif", 2) }),
                "A second line for Sarge, and a line of neither team, are left out");
            Assert.That((sarge.Goals, sarge.Kills, sarge.Deaths, sarge.Assists, sarge.Betrayals, sarge.CarrierKills, sarge.HammerKills, sarge.SwordKills, sarge.BallKills, sarge.Grabs),
                Is.EqualTo(((int?)3, 30, 20, 2, (int?)1, (int?)4, (int?)25, (int?)0, (int?)2, (int?)null)));
            Assert.That(Forfeits(build, "agla_gamesreported").Select(f => (f.SourceKey, f.SeasonSourceId, f.Winner, f.Loser, f.BothForfeited)), Is.EqualTo(new[]
            {
                ($"{InfoId}/agla_gamesreported#3", sml11, "Blue Base", "Red Army", false),
                ($"{InfoId}/agla_gamesreported#4", sml11, "Red Army", "Blue Base", true),
            }));
            Assert.That(build.Warnings, Is.SupersetOf(new[]
            {
                $"{InfoId}: agla_gamesreported: 1 game(s) of season (none), which agla_playerstats's config doesn't name, skipped",
                $"{InfoId}: agla_gamesreported: 1 game(s) listed twice, skipped",
                $"{InfoId}: agla_gamesreported: 1 second player line(s) for a player in one game, skipped",
                $"{InfoId}: agla_gamesreported: 2 player line(s) of no listed game or of neither side, skipped",
            }));
        }
    }

    [Test]
    public void Reported_TheLeaguesTeams_TheirCaptainsAndRosters_AreItsStandingsByDivision()
    {
        var season = Build(InfoId, GrifballInfo()).Input.Seasons.Seasons.Single(s => s.SourceSheet == "agla_playerstats#sSML11_Amateur" && !s.IsPlayoffs);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(season.Teams.Select(t => (t.TeamName, t.Division, t.Captain)), Is.EqualTo(new[]
            {
                ("Red Army", (string?)"Amateur", (string?)"Sarge"), ("Blue Base", "Amateur", "Church"),
            }), "The Pro team is the Pro division's");
            Assert.That(season.Teams.Select(t => string.Join(", ", t.Roster)), Is.EqualTo(Sml11Rosters),
                "The captain, then a column per player");
        }
    }

    private static byte[] GrifballCom() => Workbook(
        ("Season_Player_Stats", [
            Row("league_id", "gamertag", "team_name", "games_played", "wins", "losses", "goals", "kills", "deaths", "grabs"),
            Row(1, "Church", "Red Team", 2, 1, 1, 3, 60, 50, 9),
            Row(1, "Tex", "Blue Team", 2, 1, 1, 1, 50, 60, 4)]),
        ("Matches", [
            Row("match_id", "league_id", "season_name", "division_tier", "bungie_game_id", "is_forfeit", "team1_id", "team1_name", "team1_score", "team2_id", "team2_name", "team2_score"),
            Row(15, 1, "Winter League 2008", "Open", 242214554, 0, 1, "Red Team", 7, 2, "Blue Team", 2),
            Row(16, 1, "Winter League 2008", "Open", 242214555, 0, 2, "Blue Team", 5, 1, "Red Team", 5),
            Row(88, 1, "Winter League 2008", "Open", "NULL", 1, 1, "Red Team", 0, 2, "Blue Team", 0),
            Row(89, 1, "Winter League 2008", "Open", 242214554, 0, 1, "Red Team", 7, 2, "Blue Team", 2),
            Row(90, 2, "Spring League 2008", "Open", 300000000, 0, 1, "Red Team", 7, 2, "Blue Team", 2),
            Row(91, 1, "Winter League 2008", "Open", 300000001, 0, 1, "Red Team", 7, 2, "NULL", 2),
            Row("NULL", 1, "Winter League 2008", "Open", 300000002, 0, 1, "Red Team", 7, 2, "Blue Team", 2)]),
        ("Match_Players", [
            Row("match_id", "league_id", "bungie_game_id", "side", "team_name", "outcome", "gamertag", "goals", "kills", "deaths", "kd_spread", "betrayals", "grabs"),
            Row(15, 1, 242214554, 1, "Red Team", "W", "Church", 3, 52, 40, 12, 2, 6),
            Row(15, 1, 242214554, 2, "Blue Team", "L", "Tex", 1, 40, 52, -12, 0, 2),
            Row(15, 1, 242214554, 2, "Blue Team", "L", "TEX", 1, 40, 52, -12, 0, 2),
            Row(15, 1, 242214554, 2, "Blue Team", "L", "NULL", 0, 1, 1, 0, 0, 0),
            Row(16, 1, 242214555, 1, "Blue Team", "T", "Tex", 0, 10, 8, 2, 0, 2),
            Row(89, 1, 242214554, 1, "Red Team", "W", "Church", 3, 52, 40, 12, 2, 6)]));

    [Test]
    public void Matches_EachGameWithItsPlayersLines_GrabsIncluded_AForfeitWonByTheFirstTeam()
    {
        var build = Build(ComId, GrifballCom());
        var games = Games(build, "Matches");
        const string season = $"{ComId}/Season_Player_Stats#s1";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(g => (g.SourceKey, g.SeasonSourceId, g.GameVersion, g.IsPlayoffs, g.PlayedAt)), Is.EqualTo(new[]
            {
                ($"{ComId}/Matches#15", season, "halo3", false, (DateTime?)null),
                ($"{ComId}/Matches#16", season, "halo3", false, null),
            }), "No dates, no phase: the site kept neither");
            Assert.That(Sides(games[0]), Is.EqualTo(new[] { (1, (string?)"Red Team", Outcomes.Won, (int?)7), (2, "Blue Team", Outcomes.Lost, 2) }));
            Assert.That(Sides(games[1]), Is.EqualTo(new[] { (1, (string?)"Blue Team", Outcomes.Tie, (int?)5), (2, "Red Team", Outcomes.Tie, 5) }));
            Assert.That(games[0].Players.Select(p => (p.Gamertag, p.Side, p.Goals, p.Kills, p.Deaths, p.Betrayals, p.Grabs)), Is.EqualTo(new[]
            {
                ("Church", 1, (int?)3, 52, 40, (int?)2, (int?)6), ("Tex", 2, (int?)1, 40, 52, (int?)0, (int?)2),
            }), "One line per player; a line without a gamertag is left out");
            Assert.That(Forfeits(build, "Matches").Select(f => (f.SourceKey, f.SeasonSourceId, f.Winner, f.Loser, f.BothForfeited, f.PlayedAt)), Is.EqualTo(new[]
            {
                ($"{ComId}/Matches#88", season, "Red Team", "Blue Team", false, (DateTime?)null),
            }));
            Assert.That(build.Warnings.Where(w => w.StartsWith($"{ComId}: Matches:", StringComparison.Ordinal)), Is.EquivalentTo(new[]
            {
                $"{ComId}: Matches: 1 game(s) of season 2, which Season_Player_Stats's config doesn't name, skipped",
                $"{ComId}: Matches: 1 row(s) without both sides or a result, skipped",
                $"{ComId}: Matches: 1 game(s) listed twice, skipped",
                $"{ComId}: Matches: 1 second player line(s) for a player in one game, skipped",
                $"{ComId}: Matches: 1 player line(s) of no listed game or of neither side, skipped",
            }));
        }
    }

    [Test]
    public void AGameTable_WithoutItsStatsTable_OrSheet_OrPlayersSheet_IsReported_OrReadWithoutLines()
    {
        var config = new HistoricalSourceConfig
        {
            Tables = CatalogConfig(ComId).Tables,
            GameTables =
            [
                new() { Sheet = "Matches", Stats = "nope" },
                new() { Sheet = "missing", Stats = "Season_Player_Stats" },
                new() { Sheet = "Matches", Stats = "Season_Player_Stats", PlayersSheet = "missing", DeletedSheet = "missing" },
            ],
        };

        var build = Build(ComId, GrifballCom(), config);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(build.Warnings, Does.Contain($"{ComId}: games table 'Matches' not read (no stats table 'nope')"));
            Assert.That(build.Warnings, Does.Contain($"{ComId}: games table 'missing' not read (no such sheet, or it is empty)"));
            Assert.That(build.Input.RecordedGames!.Select(g => g.Players.Count), Is.EqualTo(NoLines), "No players sheet: the games without lines");
        }
    }

    [Test]
    public void AGameOfASeasonWithNoStats_IsLeftOut_AndReported()
    {
        // League 17 is in the config, but no stats row and no standings make it a season
        var workbook = Workbook(
            ("Season_Player_Stats", [Row("league_id", "gamertag", "team_name", "games_played", "kills"), Row(1, "Church", "Red Team", 1, 10)]),
            ("Matches", [
                Row("match_id", "league_id", "bungie_game_id", "is_forfeit", "team1_name", "team1_score", "team2_name", "team2_score"),
                Row(1, 17, 1, 0, "Roos", 5, "Kiwis", 1),
                Row(2, 17, "NULL", 1, "Roos", 0, "Kiwis", 0)]));

        var build = Build(ComId, workbook);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(build.Input.RecordedGames, Is.Null);
            Assert.That(build.Input.Forfeits, Is.Null);
            Assert.That(build.Warnings, Does.Contain($"{ComId}: 2 game(s) or forfeit(s) of a season with no stats, skipped"));
        }
    }

    [Test]
    public void TheChangedSource_MakesItsGamesSeasonsTheBuildsSeasons()
    {
        var build = HistoricalArchiveBuilder.Build([new HistoricalSourceContent(
            HistoricalTestData.Source(ComId, "https://example.org/com", CatalogConfig(ComId).ToJson()), 1, GrifballCom())], new HashSet<string> { ComId });

        Assert.That(build.Seasons, Does.Contain($"{ComId}/Season_Player_Stats#s1"));
    }

    private const string EdgeId = "edge-db";

    /// <summary>A stats table (season 1 split by ispro, season 2 not), its teams and players, and a games table of each
    /// layout, every row one the parser has to sort out.</summary>
    private static HistoricalArchiveBuild Edges()
    {
        var config = new HistoricalSourceConfig
        {
            Tables =
            [
                new()
                {
                    Sheet = "stats", Game = "reach", SeasonColumn = "season", DivisionColumn = "ispro",
                    Teams = new() { Sheet = "teams", Key = "teamid", Column = "teamname" },
                    Players = new() { Sheet = "players", Key = "pid", Column = "gamertag", RowKey = "playerid" },
                    Seasons = new() { ["1"] = new() { Name = "S1", Divisions = new() { ["1"] = "Pro", ["0"] = "Amateur" } }, ["2"] = new() { Name = "S2" } },
                },
                new() { Sheet = "stats2", Game = "halo4", SeasonColumn = "season", Seasons = new() { ["9"] = new() { Name = "S9" } } },
            ],
            GameTables =
            [
                new() { Sheet = "teamgames", Layout = GamesTableLayout.TeamRows, Stats = "stats", DeletedSheet = "empty" },
                new() { Sheet = "schedule2", Layout = GamesTableLayout.Schedule, Stats = "stats2", PlayersSheet = "lines2" },
                new() { Sheet = "schedule", Layout = GamesTableLayout.Schedule, Stats = "stats", PlayersSheet = "lines" },
                new() { Sheet = "reported", Layout = GamesTableLayout.Reported, Stats = "stats", PlayersSheet = "rlines" },
                new() { Sheet = "matches", Layout = GamesTableLayout.Matches, Stats = "stats" },
            ],
        };
        var workbook = Workbook(
            ("stats", [Row("season", "teamid", "gamertag", "kills"), Row(1, 10, "Pro One", 5), Row(1, 20, "Am One", 5), Row(2, 30, "Open One", 5)]),
            ("teams", [Row("teamid", "teamname", "ispro", null), Row(10, "Pros", 1), Row(20, "Ams", 0), Row(30, "Opens", 0), Row(40, null, 0), Row(50, "NoDiv", null), Row(60, "Odd", 7)]),
            ("players", [Row("pid", "gamertag"), Row(1, "Alpha")]),
            ("empty", []),
            ("stats2", [Row("season", "gamertag", "kills"), Row(9, "Nine", 1)]),
            ("schedule2", [Row("id", "season", "teama", "teamb", "result", "win", "link"), Row(1, 9, "A", "B", "5-0", "A", "L9")]),
            ("lines2", [Row("link", "teamid", "playerid", "gamertag", "kills"), Row("L9", "A", 1, null, 1), Row("L9", "B", null, "Bee", 1)]),
            ("teamgames", [
                Row("gameid", "teamid", "t_season", "t_playoffs", "t_win", "t_loss", "t_fwin", "t_floss", "t_goals"),
                Row(1, null, 2, 0, 1, 0, 0, 0, 5),
                Row(null, 30, 2, 0, 1, 0, 0, 0, 5),
                Row("abc", 30, 2, 0, 0, 1, 0, 0, 2),
                Row("abc", 40, 2, 0, 0, 0, 0, 0, 1),
                Row(7, 30, 2, 0, 0, 0, 1, 0, 0), Row(7, 40, 2, 0, 0, 0, 0, 0, 0),
                Row(8, 30, 2, 0, 0, 0, 0, 1, 0), Row(8, 40, 2, 0, 0, 0, 0, 1, 0),
                Row(9, 99, 1, 0, 1, 0, 0, 0, 5), Row(9, 10, 1, 0, 0, 1, 0, 0, 2),
                Row(11, 50, 1, 0, 1, 0, 0, 0, 5), Row(11, 10, 1, 0, 0, 1, 0, 0, 2),
                Row(12, 60, 1, 0, 1, 0, 0, 0, 5), Row(12, 10, 1, 0, 0, 1, 0, 0, 2),
                Row(13, 30, 3, 0, 1, 0, 0, 0, 5), Row(13, 40, 3, 0, 0, 1, 0, 0, 2),
                Row(14, 30, 2, 0, 0, 0, 0, 0, 0), Row(14, 40, 2, 0, 0, 0, 1, 0, 0),
                Row(15, 30, 2, 0, 0, 0, 0, 1, 0), Row(15, 40, 2, 0, 0, 0, 0, 0, 0),
                Row(16, 30, 2, 1, 0, 0, 1, 0, 0), Row(16, 40, 2, 1, 0, 0, 0, 1, 0)]),
            ("schedule", [
                Row("id", "season", "teama", "teamb", "result", "win", "playoffs", "link", "date_reported"),
                Row(null, 2, 30, 40, "5-1", 30, 0, null, null),
                Row(1, 2, 30, 40, null, 0, 0, null, null),
                Row(2, 2, null, 40, "5-1", 40, 0, null, null),
                Row(11, 2, 30, null, "5-1", 30, 0, null, null),
                Row(3, 2, 30, 40, "5-1", 99, 0, null, null),
                Row(4, 2, 30, 40, "abc", 30, 0, null, null),
                Row(5, 3, 30, 40, "5-1", 30, 0, null, null),
                Row(6, 2, 30, 40, "forfeit", 40, 0, null, "nope"),
                Row(7, 2, 30, 40, "5-1", 30, 0, null, null),
                Row(8, 2, 30, 40, "(2 - 6)", 40, 1, "L1", null),
                Row(9, 2, 30, 40, "6-2", 30, 0, "L1", null),
                Row(10, 2, 30, 40, "6-2", 30, 0, "L2", null),
                Row(12, 2, 30, 40, "double forfeit", 0, 0, null, null),
                Row(13, 2, 30, 40, "5-1", 30, 0, "0", null)]),
            ("lines", [
                Row("link", "teamid", "playerid", "gamertag", "kills", "deaths", "season", "wins", "losses", "playoffs"),
                Row("L1", 30, null, "Own", 4, 2),
                Row("L1", 40, 1, null, 3, 3),
                Row("L1", 40, 9, null, 1, 1),
                Row("L1", 99, null, "Elsewhere", 1, 1),
                Row("L1", 40, null, null, 1, 1),
                Row("0", 30, null, "Zero", 1, 1),
                Row("L3", 30, null, "Three", 5, 1, 2, 1, 0, 1),
                Row("L3", 40, null, "Four", 1, 5, 2, 0, 1, 1),
                Row("L5", 30, null, "Solo", 1, 1, 2, 1, 0, 0)]),
            ("reported", [
                Row("index", "gameid", "winning_team", "losing_team", "game_date", "g_season", "forfeit"),
                Row(null, 5, "Opens", "Ams", 1, 2, 0),
                Row(1, 5, null, "Ams", 1, 2, 0),
                Row(2, 0, "Opens", "Ams", 0, 2, 1),
                Row(3, null, "Opens", "Ams", 1, 2, 0),
                Row(4, 5, "Opens", "Ams", -5, 2, 0),
                Row(5, 5, "Opens", "Ams", 1, 2, 0),
                Row(6, 6, "Opens", "Ams", null, 2, 0)]),
            ("rlines", [Row("gameid", "gamertag", "teamname", "kills", "playoffs"), Row(5, "W1", "Opens", 3, 0), Row(5, "W2", "OPENS", 2, 0), Row(5, null, "Opens", 1, 0), Row(5, "X", "Other", 1, 0)]),
            ("matches", [
                Row("match_id", "season", "bungie_game_id", "is_forfeit", "team1_name", "team1_score", "team2_name", "team2_score"),
                Row(1, 2, null, 0, "Opens", 1e12, "Ams", -1e12)]));
        return HistoricalArchiveBuilder.Build([new HistoricalSourceContent(HistoricalTestData.Source(EdgeId, "https://example.org/edge", config.ToJson()), 1, workbook)],
            new HashSet<string> { "another-source" });
    }

    [Test]
    public void TeamRows_EveryRowTheParserHasToSortOut()
    {
        var build = Edges();
        var game = Games(build, "teamgames").Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((game.SourceKey, game.SeasonSourceId), Is.EqualTo(($"{EdgeId}/teamgames#abc", $"{EdgeId}/stats#s2")));
            Assert.That(Sides(game), Is.EqualTo(new[] { (1, (string?)"Opens", Outcomes.Lost, (int?)2), (2, "40", Outcomes.DidNotFinish, 1) }),
                "A team without a name is its ID; a side neither won nor lost has no result");
            Assert.That(Forfeits(build, "teamgames").Select(f => (f.Winner, f.Loser, f.BothForfeited, f.IsPlayoffs)), Is.EqualTo(new[]
            {
                ("Opens", "40", false, false), ("Opens", "40", true, false), ("40", "Opens", false, false), ("40", "Opens", false, false), ("Opens", "40", false, true),
            }), "Won by its own forfeit win, or by the other side's forfeit; a playoff one says so");
            Assert.That(build.Warnings, Is.SupersetOf(new[]
            {
                $"{EdgeId}: teamgames: 1 game(s) of season 3, which stats's config doesn't name, skipped",
                $"{EdgeId}: teamgames: 3 game(s) of a team in none of its season's divisions, skipped",
                $"{EdgeId}: teamgames: 1 row(s) without both sides or a result, skipped",
            }));
            Assert.That(build.Seasons, Is.Empty, "Another source changed: none of these seasons");
        }
    }

    [Test]
    public void Schedule_EveryRowTheParserHasToSortOut()
    {
        var build = Edges();
        var games = Games(build, "schedule");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(games.Select(g => (g.SourceKey, g.IsPlayoffs, g.Players.Count)), Is.EqualTo(new[]
            {
                ($"{EdgeId}/schedule#7", false, 0), ($"{EdgeId}/schedule#8", true, 2), ($"{EdgeId}/schedule#9", false, 0), ($"{EdgeId}/schedule#10", false, 0),
                ($"{EdgeId}/schedule#13", false, 0), ($"{EdgeId}/schedule#L3", true, 2),
            }), "A link's lines go to its first game only, never the placeholder link 0's; a link no row lists is a game of its own");
            Assert.That(Sides(games[^1]), Is.EqualTo(new[] { (1, (string?)"Opens", Outcomes.Won, (int?)null), (2, "40", Outcomes.Lost, null) }), "Its result its lines'");
            Assert.That(games[^1].Players.Select(p => (p.Gamertag, p.Side)), Is.EqualTo(new[] { ("Three", 1), ("Four", 2) }));
            Assert.That(Sides(games[0]), Is.EqualTo(new[] { (1, (string?)"Opens", Outcomes.Won, (int?)5), (2, "40", Outcomes.Lost, 1) }));
            Assert.That(Sides(games[1]), Is.EqualTo(new[] { (1, (string?)"Opens", Outcomes.Lost, (int?)2), (2, "40", Outcomes.Won, 6) }));
            Assert.That(games[1].Players.Select(p => (p.Gamertag, p.Side)), Is.EqualTo(new[] { ("Own", 1), ("Alpha", 2) }), "Its own gamertag, or the lookup's by the player's ID");
            Assert.That(Forfeits(build, "schedule").Select(f => (f.Winner, f.Loser, f.BothForfeited, f.PlayedAt)), Is.EqualTo(new[]
            {
                ("40", "Opens", false, (DateTime?)null), ("40", "Opens", true, null),
            }), "A double forfeit names no winner");
            Assert.That(build.Warnings, Is.SupersetOf(new[]
            {
                $"{EdgeId}: schedule: 1 game(s) of season 3, which stats's config doesn't name, skipped",
                $"{EdgeId}: schedule: 4 row(s) without both sides or a result, skipped",
                $"{EdgeId}: schedule: 5 player line(s) of no listed game or of neither side, skipped",
            }));
        }
    }

    [Test]
    public void Reported_AndMatches_EveryRowTheParserHasToSortOut()
    {
        var build = Edges();
        var reported = Games(build, "reported");
        var match = Games(build, "matches").Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reported.Select(g => (g.SourceKey, g.PlayedAt, g.Players.Count)), Is.EqualTo(new[]
            {
                ($"{EdgeId}/reported#4", (DateTime?)null, 2), ($"{EdgeId}/reported#6", null, 0),
            }), "A date before 1970 is none, and so is no date; a game without lines has no players");
            Assert.That(reported[0].Players.Select(p => (p.Gamertag, p.Side)), Is.EqualTo(new[] { ("W1", 1), ("W2", 1) }), "A team's name in any case");
            Assert.That(Games(build, "schedule2").Single().Players.Select(p => p.Gamertag), Is.EqualTo(OnlyBee), "No players lookup: a line's own gamertag only");
            Assert.That(Forfeits(build, "reported").Select(f => (f.SourceKey, f.PlayedAt)), Is.EqualTo(new[] { ($"{EdgeId}/reported#2", (DateTime?)null) }));
            Assert.That(Sides(match), Is.EqualTo(new[] { (1, (string?)"Opens", Outcomes.Tie, (int?)null), (2, "Ams", Outcomes.Tie, null) }),
                "A score beyond a number is none; no players sheet, no lines");
            Assert.That(match.Players, Is.Empty);
            Assert.That(build.Warnings, Is.SupersetOf(new[]
            {
                $"{EdgeId}: reported: 2 row(s) without both sides or a result, skipped",
                $"{EdgeId}: reported: 1 game(s) listed twice, skipped",
                $"{EdgeId}: reported: 2 player line(s) of no listed game or of neither side, skipped",
            }));
        }
    }

    [Test]
    public void ScheduleResults_ParsesDecidedMatchesAndForfeits_SkipsUnplayed_AndIgnoresTimeOnlyDates()
    {
        var config = new HistoricalSourceConfig
        {
            Season = new() { Name = "Multi", League = "AGLA", Game = "multi" },
            Tables =
            [
                new()
                {
                    Sheet = "Player_Stats",
                    Game = "halo4",
                    SeasonColumn = "season_id",
                    StandingsSheet = "Standings",
                    Seasons = new()
                    {
                        ["ggl-fl15"] = new() { Name = "GGL Fall League 2015", League = "GGL", Game = "halo5" },
                    },
                },
            ],
            GameTables = [new() { Sheet = "Matches_Schedule", Layout = GamesTableLayout.ScheduleResults, Stats = "Player_Stats" }],
        };
        var workbook = Workbook(
            ("Player_Stats", [Row("season_id", "gamertag", "team", "games played")]),
            ("Standings", [
                Row("season_id", "team_name", "rank", "wins", "losses"),
                Row("ggl-fl15", "Alpha", 1, 2, 0),
                Row("ggl-fl15", "Bravo", 2, 0, 2),
            ]),
            ("Matches_Schedule", [
                Row("season_id", "division", "team_a", "team_b", "winner", "score_a", "score_b", "is_forfeit", "match_time"),
                Row("ggl-fl15", "Open", "Alpha", "Bravo", "Alpha", 5, 2, 0, "2015-09-20"),
                Row("ggl-fl15", "Playoffs", "Bravo", "Alpha", "Alpha", null, null, 0, "Sunday, 9:00pm"),
                Row("ggl-fl15", "Open", "Alpha", "Bravo", "Alpha", null, null, 1, "2015-09-21"),
                Row("ggl-fl15", "Open", "Alpha", "Bravo", null, null, null, 0, "Sunday, 9:00pm"),
            ]));

        var build = Build("hub", workbook, config);
        var games = Games(build, "Matches_Schedule");
        var forfeits = Forfeits(build, "Matches_Schedule");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(build.Warnings, Is.Empty, "unplayed schedule rows skip silently without warning");
            Assert.That(games, Has.Count.EqualTo(2));
            Assert.That((games[0].GameVersion, games[0].IsPlayoffs, games[0].PlayedAt),
                Is.EqualTo(("halo5", false, new DateTime(2015, 9, 20, 0, 0, 0, DateTimeKind.Utc))));
            Assert.That(Sides(games[0]), Is.EqualTo(new[] { (1, (string?)"Alpha", Outcomes.Won, (int?)5), (2, "Bravo", Outcomes.Lost, 2) }));
            Assert.That((games[1].IsPlayoffs, games[1].PlayedAt), Is.EqualTo((true, (DateTime?)null)), "time-only strings do not default to the current year");
            Assert.That(Sides(games[1]), Is.EqualTo(new[] { (1, (string?)"Bravo", Outcomes.Lost, (int?)null), (2, "Alpha", Outcomes.Won, null) }));
            Assert.That(forfeits.Select(f => (f.Winner, f.Loser, f.PlayedAt)), Is.EqualTo(new[] { ("Alpha", "Bravo", (DateTime?)new DateTime(2015, 9, 21, 0, 0, 0, DateTimeKind.Utc)) }));
        }
    }

    [Test]
    public void ScheduleResults_ScoresDecideAMatchNoWinnerNames_SecondTeamWinsAForfeit_AndOneSidedRowsAreCounted()
    {
        var config = new HistoricalSourceConfig
        {
            Season = new() { Name = "Multi", League = "AGLA", Game = "multi" },
            Tables =
            [
                new()
                {
                    Sheet = "Player_Stats",
                    Game = "halo4",
                    SeasonColumn = "season_id",
                    StandingsSheet = "Standings",
                    Seasons = new()
                    {
                        ["ggl-fl15"] = new() { Name = "GGL Fall League 2015", League = "GGL", Game = "halo5" },
                        ["ggl-wl16"] = new() { Name = "GGL Winter League 2016", League = "GGL" },
                    },
                },
            ],
            GameTables = [new() { Sheet = "Matches_Schedule", Layout = GamesTableLayout.ScheduleResults, Stats = "Player_Stats" }],
        };
        var workbook = Workbook(
            ("Player_Stats", [Row("season_id", "gamertag", "team", "games played")]),
            ("Standings", [
                Row("season_id", "team_name", "rank", "wins", "losses"),
                Row("ggl-fl15", "Alpha", 1, 2, 0),
                Row("ggl-wl16", "Bravo", 1, 1, 0),
            ]),
            ("Matches_Schedule", [
                Row("season_id", "division", "team_a", "team_b", "winner", "score_a", "score_b", "is_forfeit", "match_time"),
                Row("ggl-wl16", "Open", "Alpha", "Bravo", null, 3, 5, 0, null),
                Row("ggl-fl15", "Open", "Alpha", null, "Alpha", 5, 2, 0, "2015-09-20"),
                Row("ggl-fl15", "Open", "Alpha", "Bravo", null, 3, null, 0, "2015-09-22"),
                Row("ggl-fl15", "Open", "Alpha", "Bravo", "Bravo", null, null, 1, "2015-02-30"),
            ]));

        var build = Build("hub", workbook, config);
        var games = Games(build, "Matches_Schedule");
        var forfeits = Forfeits(build, "Matches_Schedule");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(build.Warnings, Has.Member("hub: Matches_Schedule: 1 row(s) without both sides or a result, skipped"));
            Assert.That(games, Has.Count.EqualTo(1), "a row with one score and no winner is unplayed");
            Assert.That((games[0].GameVersion, games[0].PlayedAt), Is.EqualTo(("halo4", (DateTime?)null)), "a season naming no game is its table's");
            Assert.That(Sides(games[0]), Is.EqualTo(new[] { (1, (string?)"Alpha", Outcomes.Lost, (int?)3), (2, "Bravo", Outcomes.Won, 5) }));
            Assert.That(forfeits.Select(f => (f.Winner, f.Loser, f.PlayedAt)), Is.EqualTo(new[] { ("Bravo", "Alpha", (DateTime?)null) }),
                "a date that isn't one is none");
        }
    }
}
