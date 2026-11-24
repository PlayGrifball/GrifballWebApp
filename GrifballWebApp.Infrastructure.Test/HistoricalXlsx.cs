using ClosedXML.Excel;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>Small xlsx workbooks built in the tests (ClosedXML), shaped like the archive's sheets: never real data.</summary>
internal static class HistoricalXlsx
{
    /// <summary>A workbook of sheets, each its rows (null is an empty cell).</summary>
    public static byte[] Workbook(params (string Name, object?[][] Rows)[] sheets)
        => Build(workbook =>
        {
            foreach (var (name, rows) in sheets)
                Fill(workbook.Worksheets.Add(name), rows);
        });

    public static byte[] Build(Action<XLWorkbook> build)
    {
        using var workbook = new XLWorkbook();
        build(workbook);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static void Fill(IXLWorksheet sheet, object?[][] rows)
    {
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                if (rows[r][c] is { } value)
                    sheet.Cell(r + 1, c + 1).Value = XLCellValue.FromObject(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public static object?[] Row(params object?[] cells) => cells;

    /// <summary>
    /// A workbook shaped like the grifball.info database backup (made-up rows): Reach's hr_pstats (a row per player, team,
    /// season and phase; season 12 split Pro/Amateur by its team's ispro, season -1 all-time totals, a team without ispro and
    /// a row without a gamertag), its teams in gb_teamindex, and Halo 4's h4_pstats (a row per player per game, gamertags
    /// NULL, from h_players; teams from h_teams; Crusader traded mid-season). Its games: Reach's gb_teamstats (a row per team
    /// per game: a regular and a playoff game, a forfeit, two forfeit rows without a game ID, a deleted game, a Pro game of
    /// season 12, a game of a team without ispro, a game with one row), Halo 4's h_games (a game, a forfeit, an unplayed
    /// game, one whose winner is neither team, a double forfeit), and the agla_* tables of a Reach league (SML11: player
    /// lines per game, its teams with captains and rosters, its reported games and forfeits).
    /// </summary>
    public static byte[] GrifballInfo() => Workbook(
        ("intro", [Row("This spreadsheet is a backup of the grifball.info stats database.")]),
        ("hr_pstats", [
            Row("index", "playerid", "teamid", "season", "playingtime", "wins", "losses", "goals", "kills", "deaths", "assists", "betrayals", "multikills", "playoffs", "spread", "kd", "batavg", "gamertag", "oneid"),
            Row(1, 1, 1, 12, 10083, 5, 3, 3, 553, 602, 28, 26, 120, 0, -49, 0.919, 45.628, "halfbreed105", 4),
            Row(2, 2, 2, 12, 9000, 6, 2, 7, 400, 300, 10, 1, 90, 0, 100, 1.333, 50, "Hoya16", "NULL"),
            Row(3, 3, 3, 13, 7000, 4, 4, 5, 300, 310, 12, 2, 60, 0, -10, 0.968, 40, "PriestXYZ", 7),
            Row(4, 3, 3, 13, 1500, 2, 0, 3, 80, 40, 2, 0, 20, 1, 40, 2, 60, "PriestXYZ", 7),
            Row(5, 4, 4, 13, 7200, 3, 5, 2, 250, 290, 9, 3, 50, 0, -40, 0.862, 35, "runNOKYARDrun", 8),
            Row(6, 9, 9, -1, 99999, 100, 50, 300, 4000, 3900, 200, 50, 900, 0, 100, 1.026, 48, "Twizzted Status", 0),
            Row(7, 8, 9, -1, 88888, 90, 60, 250, 3800, 3700, 180, 40, 800, 0, 100, 1.027, 47, "Taylor Cann", 0),
            Row(8, 5, 5, 12, 600, 1, 0, 1, 30, 20, 1, 0, 5, 0, 10, 1.5, 50, "Limbo Larry", 0),
            Row(9, 6, 3, 13, 600, 1, 0, 0, 30, 25, 0, 0, 4, 0, 5, 1.2, 45, "NULL", 0)]),
        ("gb_teamindex", [
            Row("teamid", "teamname", "captain", "division", "season_id", "ispro"),
            Row(1, "Cabs Are Here", 0, 0, 12, 1),
            Row(2, "Downstait", 0, 0, 12, 0),
            Row(3, "Way to Fail", 0, 16, 13, 0),
            Row(4, "Red Ray", 0, 9, 13, 0),
            Row(5, "Limbo", 0, 0, 12, "NULL"),
            Row(9, "All Time", 0, 0, -1, 0)]),
        ("h4_pstats", [
            Row("index", "playerid", "teamid", "season", "playingtime", "wins", "losses", "goals", "kills", "deaths", "assists", "betrayals", "multikills", "playoffs", "spread", "kd", "batavg", "gamertag", "link", "division"),
            Row(1, 10, 20, 20, 600, 1, 0, 2, 30, 20, 1, 0, 50, 0, "NULL", 0, 0, "NULL", "https://example.org/match-a", "NULL"),
            Row(2, 11, 20, 20, 600, 1, 0, 1, 20, 22, 0, 0, 30, 0, "NULL", 0, 0, "NULL", "https://example.org/match-a", "NULL"),
            Row(3, 10, 20, 20, 500, 0, 1, 0, 25, 30, 2, 1, 40, 0, "NULL", 0, 0, "NULL", "https://example.org/match-b", "NULL"),
            Row(4, 10, 21, 20, 450, 1, 0, 3, 28, 15, 0, 0, 45, 0, "NULL", 0, 0, "NULL", "https://example.org/match-c", "NULL")]),
        ("h_players", [Row("playerid", "gamertag", "teamid"), Row(10, "Crusader0102", 21), Row(11, "TrueFlyingCow", 20)]),
        ("h_teams", [Row("teamid", "teamname", "division", "season"), Row(20, "Four Guys One Ball", 5, 20), Row(21, "Hybrid Theory", 2, 20)]),
        ("gb_teamstats", [
            Row("entryid", "gameid", "teamid", "division", "t_goals", "t_win", "t_loss", "t_fwin", "t_floss", "t_season", "t_playoffs"),
            Row(1, 100, 3, 1, 6, 1, 0, 0, 0, 13, 0),
            Row(2, 100, 4, 1, 3, 0, 1, 0, 0, 13, 0),
            Row(3, 101, 3, 1, 2, 0, 1, 0, 0, 13, 1),
            Row(4, 101, 4, 1, 5, 1, 0, 0, 0, 13, 1),
            Row(5, 102, 3, 1, 0, 0, 0, 1, 0, 13, 0),
            Row(6, 102, 4, 1, 0, 0, 0, 0, 1, 13, 0),
            Row(7, 0, 3, 1, "NULL", "NULL", "NULL", 1, 0, 13, 0),
            Row(8, 0, 4, 1, "NULL", "NULL", "NULL", 0, 1, 13, 0),
            Row(9, 103, 3, 1, 5, 1, 0, 0, 0, 13, 0),
            Row(10, 103, 4, 1, 0, 0, 1, 0, 0, 13, 0),
            Row(11, 104, 1, 1, 7, 1, 0, 0, 0, 12, 0),
            Row(12, 104, 2, 1, 1, 0, 1, 0, 0, 12, 0),
            Row(13, 105, 5, 1, 4, 1, 0, 0, 0, 12, 0),
            Row(14, 105, 2, 1, 2, 0, 1, 0, 0, 12, 0),
            Row(15, 106, 3, 1, 5, 1, 0, 0, 0, 13, 0)]),
        ("gb_deletedgames", [Row("index", "gameID", "win_team", "loss_team", "season", "reason"), Row(1, 103, 3, 4, 13, "new gameid/reporter")]),
        ("h_games", [
            Row("id", "season", "week", "teama", "teamb", "result", "win", "loss", "playoffs", "link", "date_reported", "status"),
            Row(1, 20, 1, 20, 21, "(7 - 2)", 21, 20, 0, "https://example.org/match-a", "2013-01-26 11:43:32", 5),
            Row(2, 20, 1, 20, 21, "forfeit", 20, 21, 0, "0", "2013-01-27 10:00:00", 2),
            Row(3, 20, 2, 20, 21, "NULL", 0, 0, 0, "0", "NULL", 1),
            Row(4, 20, 2, 20, 21, "5-0", 0, 0, 0, "0", "NULL", 5),
            Row(5, 20, 3, 21, 20, "double forfeit", 21, 20, 1, "0", "NULL", 2)]),
        ("agla_playerstats", [
            Row("EntryID", "GameID", "Gamertag", "TeamName", "PlayingTime", "Goals", "Kills", "Deaths", "Assists", "Betrayals", "Win", "Loss", "bombstops", "sword_kills", "hammer_kills", "bomb_kills", "p_season", "playoffs"),
            Row(1, 900, "Sarge", "Red Army", 700, 3, 30, 20, 2, 1, 1, 0, 4, 0, 25, 2, "SML11", 0),
            Row(2, 900, "Grif", "Blue Base", 700, 1, 20, 30, 1, 0, 0, 1, 2, 1, 15, 0, "SML11", 0),
            Row(3, 900, "Sarge", "Red Army", 700, 3, 30, 20, 2, 1, 1, 0, 4, 0, 25, 2, "SML11", 0),
            Row(4, 900, "Doc", "NULL", 700, 0, 5, 9, 0, 0, 1, 0, 0, 0, 5, 0, "SML11", 0),
            Row(5, 901, "Sarge", "Red Army", 600, 2, 25, 22, 1, 0, 1, 0, 1, 0, 20, 1, "SML11", 1),
            Row(6, 901, "Grif", "Blue Base", 600, 0, 22, 25, 0, 0, 0, 1, 1, 0, 18, 0, "SML11", 1),
            Row(7, 902, "Simmons", "Red Army", 500, 1, 10, 10, 0, 0, 1, 0, 0, 0, 9, 0, "SML11", 0)]),
        ("agla_teamindex", [
            Row("TeamId", "TeamName", "Captain", "Division", "isPro", "p2", "p3", "season"),
            Row(1, "Red Army", "Sarge", 1, 0, "Simmons", "NULL", "SML11"),
            Row(2, "Blue Base", "Church", 1, 0, "Grif", "Tucker", "SML11"),
            Row(3, "Freelancers", "Tex", 9, 1, "Wash", "NULL", "SML11")]),
        ("agla_gamesreported", [
            Row("Index", "GameID", "Winning_team", "Losing_team", "Game_Date", "Date_Reported", "g_season", "forfeit", "forfeit_id"),
            Row(1, 900, "Red Army", "Blue Base", 1311647153, 1311631305, "SML11", 0, 0),
            Row(2, 901, "Red Army", "Blue Base", 1311734435, 1311710992, "SML11", 0, 0),
            Row(3, 0, "Blue Base", "Red Army", 0, 0, "SML11", 1, "forfeit1"),
            Row(4, 0, "Red Army", "Blue Base", 0, 0, "SML11", 2, "forfeit2"),
            Row(5, 0, "Red Army", "Blue Base", 0, 0, "NULL", 1, "forfeit1"),
            Row(6, 903, "Red Army", "Blue Base", 1311800000, 1311800000, "SML11", 0, 0),
            Row(7, 900, "Red Army", "Blue Base", 1311647153, 1311631305, "SML11", 0, 0)]),
        ("agla_deletedgames", [Row("index", "gameID", "win_team", "loss_team", "season", "reason"), Row(1, 903, "Red Army", "Blue Base", "SML11", "reported twice")]));

    /// <summary>A season stats tab: Player, Team, Games, Wins, Losses, Goals, Kills, Deaths, Assists.</summary>
    public static object?[][] StatsTab(params (string Gamertag, string? Team, int Kills)[] players)
        => [Row("Player", "Team", "Games", "Wins", "Losses", "Goals", "Kills", "Deaths", "Assists"),
            .. players.Select(p => Row(p.Gamertag, p.Team, 4, 2, 2, 1, p.Kills, 10, 3))];
}
