using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Stats;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Profile;

/// <summary>
/// The stat strip at the top of a profile (its service record): MMR, and career games, wins, K/D and bomb scores over
/// league seasons, over matchmaking (Late League), and over both.
/// </summary>
public class ServiceRecordDto
{
    /// <summary>The player's Late League rating; null for a player who has never played matchmaking (everyone starts at
    /// the same default, which says nothing about them).</summary>
    public int? Mmr { get; set; }
    /// <summary>League seasons and matchmaking together: the strip's default.</summary>
    public required ServiceRecordTotalsDto All { get; set; }
    public required ServiceRecordTotalsDto League { get; set; }
    public required ServiceRecordTotalsDto Matchmaking { get; set; }
}

public class ServiceRecordTotalsDto
{
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public decimal? WinPercent { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public decimal? KdRatio { get; set; }
    /// <summary>Bomb scores (goals), every game's alike: a bomb score is one in any Halo.</summary>
    public long BombScores { get; set; }
    /// <summary>Halo Infinite games whose score is not goals (a custom mode's encoded counts, see #217), left out of
    /// <see cref="BombScores"/>.</summary>
    public int UncountedBombScoreGames { get; set; }
}

/// <summary>Builds a profile's <see cref="ServiceRecordDto"/> from the stat lines the profile already has.</summary>
public static class ServiceRecord
{
    /// <summary>
    /// The most goals one player scores in one Halo Infinite game. Infinite's Score is goals in Grifball (1–5 a game), but
    /// some custom modes keep their own counts in it, in the hundreds of millions (#217): a game above this isn't goals.
    /// </summary>
    public const int MaxBombScoresPerGame = 20;

    /// <summary>The service record of a user, from their season (<paramref name="league"/>) and Late League
    /// (<paramref name="matchmaking"/>) lines.</summary>
    public static async Task<ServiceRecordDto> LoadAsync(GrifballContext context, User user, IReadOnlyCollection<StatLine> league,
        IReadOnlyCollection<StatLine> matchmaking, CancellationToken ct = default)
    {
        var leagueEncoded = await EncodedGoalsAsync(PlayerGameStatsQuery.Query(context), user.Id, ct);
        var matchmakingEncoded = await EncodedGoalsAsync(PlayerGameStatsQuery.Matchmaking(context), user.Id, ct);
        var leagueTotals = Totals(league, leagueEncoded);
        var matchmakingTotals = Totals(matchmaking, matchmakingEncoded);
        return new ServiceRecordDto
        {
            Mmr = matchmaking.Count > 0 || user.Wins + user.Losses > 0 ? user.MMR : null,
            All = Totals([.. league, .. matchmaking], [.. leagueEncoded, .. matchmakingEncoded]),
            League = leagueTotals,
            Matchmaking = matchmakingTotals,
        };
    }

    /// <summary>The goals of the user's Infinite games whose score isn't goals (above <see cref="MaxBombScoresPerGame"/>).</summary>
    private static Task<List<long>> EncodedGoalsAsync(IQueryable<PlayerGameStat> games, int userID, CancellationToken ct) => games
        // A Waypoint game is the only one with a MatchID; a recorded game's goals are its own count
        .Where(g => g.UserID == userID && g.MatchID != null && g.Goals > MaxBombScoresPerGame)
        .Select(g => (long)g.Goals!.Value)
        .ToListAsync(ct);

    /// <summary>The lines' totals; their goals less the goals of <paramref name="encoded"/>, the games whose score isn't
    /// goals, which the lines (totalled from those games) include.</summary>
    public static ServiceRecordTotalsDto Totals(IReadOnlyCollection<StatLine> lines, IReadOnlyCollection<long> encoded)
    {
        var played = lines.Sum(l => l.GamesPlayed);
        var wins = lines.Sum(l => l.Wins);
        var kills = lines.Sum(l => l.Kills);
        var deaths = lines.Sum(l => l.Deaths);
        return new ServiceRecordTotalsDto
        {
            GamesPlayed = played,
            Wins = wins,
            Losses = lines.Sum(l => l.Losses),
            WinPercent = StatMath.WinPercent(wins, played),
            Kills = kills,
            Deaths = deaths,
            KdRatio = StatMath.KdRatio(kills, deaths),
            BombScores = lines.Sum(l => l.Goals) - encoded.Sum(),
            UncountedBombScoreGames = encoded.Count,
        };
    }
}
