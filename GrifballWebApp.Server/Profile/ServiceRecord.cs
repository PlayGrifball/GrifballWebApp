using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Stats;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
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
    /// <summary>Halo Infinite games whose goals can't be read (a custom mode's packed Score not decoded yet, see #217),
    /// left out of <see cref="BombScores"/>.</summary>
    public int UncountedBombScoreGames { get; set; }
}

/// <summary>Builds a profile's <see cref="ServiceRecordDto"/> from the stat lines the profile already has.</summary>
public static class ServiceRecord
{
    /// <summary>The service record of a user, from their season (<paramref name="league"/>) and Late League
    /// (<paramref name="matchmaking"/>) lines.</summary>
    public static async Task<ServiceRecordDto> LoadAsync(GrifballContext context, User user, IReadOnlyCollection<StatLine> league,
        IReadOnlyCollection<StatLine> matchmaking, CancellationToken ct = default)
    {
        // The user's Infinite games whose goals can't be read (GrifballGoals has none for them), league and queue alike,
        // in one round trip: the stat lines (PlayerGameStatsQuery) leave the same games' goals out. Only an Infinite game
        // has a Waypoint participant.
        var unread = await context.HaloMatchParticipants
            .Where(GrifballGoals.Inline((MatchParticipant p) => p.XboxUser.User!.Id == user.Id
                && GrifballGoals.Infinite(p) == null
                && (p.MatchTeam.Match.SeriesGame != null || p.MatchTeam.Match.MatchedMatch != null)))
            .Select(p => new { League = p.MatchTeam.Match.SeriesGame != null, Matchmaking = p.MatchTeam.Match.MatchedMatch != null })
            .ToListAsync(ct);
        var leagueTotals = Totals(league, unread.Count(g => g.League));
        var matchmakingTotals = Totals(matchmaking, unread.Count(g => g.Matchmaking));
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
        || await context.MatchedMatches.Where(MatchedMatch.ReportedFromHalo).AnyAsync(MatchedMatch.HasPlayer(user.Id), ct);

    /// <summary>The lines' totals, with <paramref name="unreadGoalGames"/> games whose goals the lines couldn't count.</summary>
    public static ServiceRecordTotalsDto Totals(IReadOnlyCollection<StatLine> lines, int unreadGoalGames)
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
            BombScores = lines.Sum(l => l.Goals),
            UncountedBombScoreGames = unreadGoalGames,
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
