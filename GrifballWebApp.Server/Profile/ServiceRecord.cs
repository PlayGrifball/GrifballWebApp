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
        // The user's Infinite games whose score isn't goals, league and queue alike, in one round trip. Only an Infinite
        // game has a Waypoint participant; a recorded game's goals are its own count.
        var encoded = await context.MatchParticipants
            .Where(p => p.XboxUser.User!.Id == user.Id && p.Score > MaxBombScoresPerGame
                && (p.MatchTeam.Match.SeriesGame != null || p.MatchTeam.Match.MatchedMatch != null))
            .Select(p => new
            {
                League = p.MatchTeam.Match.SeriesGame != null,
                Matchmaking = p.MatchTeam.Match.MatchedMatch != null,
                Goals = (long)p.Score,
            })
            .ToListAsync(ct);
        var leagueTotals = Totals(league, [.. encoded.Where(g => g.League).Select(g => g.Goals)]);
        var matchmakingTotals = Totals(matchmaking, [.. encoded.Where(g => g.Matchmaking).Select(g => g.Goals)]);
        return new ServiceRecordDto
        {
            Mmr = await HasPlayedTheQueueAsync(context, user, ct) ? user.MMR : null,
            All = Combine(leagueTotals, matchmakingTotals),
            League = leagueTotals,
            Matchmaking = matchmakingTotals,
        };
    }

    /// <summary>
    /// Whether the user's rating has moved off the default. Only the Discord queue (<c>QueueService</c>) rates a player:
    /// its win/loss record is on the user, and failing that a queue match of theirs that was reported (a Waypoint match).
    /// An archive's matchmaking game (a recorded game) never touches the rating.
    /// </summary>
    private static async Task<bool> HasPlayedTheQueueAsync(GrifballContext context, User user, CancellationToken ct) =>
        user.Wins + user.Losses > 0
        || await context.MatchedMatches.AnyAsync(m => m.MatchID != null && m.RecordedGameID == null
            && (m.HomeTeam.Players.Any(p => p.UserID == user.Id) || m.AwayTeam.Players.Any(p => p.UserID == user.Id)), ct);

    /// <summary>The lines' totals; their goals less the goals of <paramref name="encoded"/>, the games whose score isn't
    /// goals, which the lines (totalled from those games) include.</summary>
    public static ServiceRecordTotalsDto Totals(IReadOnlyCollection<StatLine> lines, IReadOnlyCollection<long> encoded)
    {
        var played = lines.Sum(l => l.GamesPlayed);
        var wins = lines.Sum(l => l.Wins);
        var kills = lines.Sum(l => l.Kills);
        var deaths = lines.Sum(l => l.Deaths);
        return Ratios(new ServiceRecordTotalsDto
        {
            GamesPlayed = played,
            Wins = wins,
            Losses = lines.Sum(l => l.Losses),
            Kills = kills,
            Deaths = deaths,
            BombScores = lines.Sum(l => l.Goals) - encoded.Sum(),
            UncountedBombScoreGames = encoded.Count,
        });
    }

    /// <summary>Two disjoint sets of games' totals as one: the counts added, the ratios worked out again.</summary>
    public static ServiceRecordTotalsDto Combine(ServiceRecordTotalsDto a, ServiceRecordTotalsDto b) => Ratios(new ServiceRecordTotalsDto
    {
        GamesPlayed = a.GamesPlayed + b.GamesPlayed,
        Wins = a.Wins + b.Wins,
        Losses = a.Losses + b.Losses,
        Kills = a.Kills + b.Kills,
        Deaths = a.Deaths + b.Deaths,
        BombScores = a.BombScores + b.BombScores,
        UncountedBombScoreGames = a.UncountedBombScoreGames + b.UncountedBombScoreGames,
    });

    private static ServiceRecordTotalsDto Ratios(ServiceRecordTotalsDto totals)
    {
        totals.WinPercent = StatMath.WinPercent(totals.Wins, totals.GamesPlayed);
        totals.KdRatio = StatMath.KdRatio(totals.Kills, totals.Deaths);
        return totals;
    }
}
