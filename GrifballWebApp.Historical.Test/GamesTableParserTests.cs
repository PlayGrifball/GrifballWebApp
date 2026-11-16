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
[Parallelizable(ParallelScope.All)]
public class GamesTableParserTests
{
    private const string InfoId = "history-grifball-info-db";
    private const string ComId = "history-grifball-com-db";

    private static readonly string[] ComSheets = ["Matches", "Match_Players"];
    private static readonly string[] InfoSheets = ["gb_teamstats", "gb_deletedgames", "h_games", "h4_pstats", "agla_playerstats", "agla_teamindex", "agla_gamesreported", "agla_deletedgames"];
    private static readonly string[] Sml11Rosters = ["Sarge, Simmons", "Church, Grif, Tucker"];
    private static readonly int[] NoLines = [0, 0];

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
                ($"{InfoId}/agla_gamesreported#1", sml11, "halo3", false, (DateTime?)new DateTime(2011, 7, 26, 2, 25, 53, DateTimeKind.Utc)),
                ($"{InfoId}/agla_gamesreported#2", sml11, "halo3", true, (DateTime?)new DateTime(2011, 7, 27, 2, 40, 35, DateTimeKind.Utc)),
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
}
