using GrifballWebApp.Application.Profile;

namespace GrifballWebApp.Application.Stats;

/// <summary>The ratios every stat line and total is shown with, counted one way everywhere.</summary>
public static class StatMath
{
    /// <summary>Kills per death, to two places; null without a death.</summary>
    public static decimal? KdRatio(int kills, int deaths) => deaths > 0 ? Math.Round((decimal)kills / deaths, 2) : null;

    /// <summary>Games won in a hundred, to one place; null without a game.</summary>
    public static decimal? WinPercent(int wins, int played) => played > 0 ? Math.Round(100m * wins / played, 1) : null;

    /// <summary>A stat per game, to two places; null without a game.</summary>
    public static decimal? PerGame(long total, int games) => games > 0 ? Math.Round((decimal)total / games, 2) : null;
}

/// <summary>
/// Career totals over stat lines, seasons' (<see cref="SeasonStatsService"/>) and Late League's alike, for a profile and the
/// leaderboards.
/// Within one game everything is totalled; across games what every game counts alike (games, wins, kills, deaths, assists)
/// and goals and carrier stops, a career's whatever the game; medals and the rest mean something different in each game.
/// </summary>
public static class CareerTotals
{
    /// <summary>The totals of each game the lines are of, in game order.</summary>
    public static List<GameCareerSummaryDto> ByGame(IEnumerable<StatLine> lines) => lines
        .GroupBy(s => s.GameVersionID)
        .Select(InOneGame)
        .OrderBy(c => c.GameVersionID)
        .ToList();

    /// <summary>The totals of lines of one game.</summary>
    public static GameCareerSummaryDto InOneGame(IEnumerable<StatLine> game)
    {
        var lines = game.ToList();
        var first = lines[0];
        var career = AcrossGames(lines);
        career.GameVersionID = first.GameVersionID;
        career.GameVersionName = first.GameVersionName;
        career.GameVersionSlug = first.GameVersionSlug;
        career.Sprees = lines.Sum(s => s.Sprees);
        career.MultiKills = lines.Sum(s => s.MultiKills);
        career.Exterminations = lines.Sum(s => s.Exterminations);
        career.Betrayals = lines.Sum(s => s.Betrayals);
        career.HammerKills = lines.Sum(s => s.HammerKills);
        career.SwordKills = lines.Sum(s => s.SwordKills);
        career.BallKills = lines.Sum(s => s.BallKills);
        career.Punches = lines.Sum(s => s.Punches);
        career.DamageDealt = lines.Any(s => s.DamageDealt.HasValue) ? lines.Sum(s => (long)(s.DamageDealt ?? 0)) : null;
        career.DamageTaken = lines.Any(s => s.DamageTaken.HasValue) ? lines.Sum(s => (long)(s.DamageTaken ?? 0)) : null;
        career.Score = lines.Any(s => s.Score.HasValue) ? lines.Sum(s => s.Score ?? 0L) : null;
        return career;
    }

    /// <summary>The totals of lines of any games: what every game counts alike, and goals and carrier stops.</summary>
    public static GameCareerSummaryDto AcrossGames(IEnumerable<StatLine> lines)
    {
        var list = lines.ToList();
        var played = list.Sum(s => s.GamesPlayed);
        var wins = list.Sum(s => s.Wins);
        var kills = list.Sum(s => s.Kills);
        var deaths = list.Sum(s => s.Deaths);
        var goals = list.Sum(s => s.Goals);
        // Over the lines that record carrier stops (a game from Waypoint has none), so games without them don't lower it
        var recording = list.Where(s => s.RecordsCarrierKills).ToList();
        return new GameCareerSummaryDto
        {
            GameVersionName = "All Games",
            GameVersionSlug = "all",
            SeasonsCount = list.Select(s => s.SeasonKey).OfType<string>().Distinct().Count(),
            GamesPlayed = played,
            GamesDecidedByVote = list.Sum(s => s.GamesDecidedByVote),
            Wins = wins,
            Losses = list.Sum(s => s.Losses),
            WinPercent = StatMath.WinPercent(wins, played),
            Kills = kills,
            Deaths = deaths,
            Assists = list.Sum(s => s.Assists),
            KdRatio = StatMath.KdRatio(kills, deaths),
            KdSpread = kills - deaths,
            Goals = goals,
            // Per game with a box score: a game decided by vote has no goals or stops to count (#296)
            GoalsPerGame = StatMath.PerGame(goals, list.Sum(s => s.GamesWithStats)),
            CarrierKills = list.Sum(s => s.CarrierKills),
            StopsPerGame = StatMath.PerGame(recording.Sum(s => s.CarrierKills), recording.Sum(s => s.GamesWithStats)),
        };
    }
}
