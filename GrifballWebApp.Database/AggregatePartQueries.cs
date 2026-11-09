using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Database;

/// <summary>
/// The aggregates' parts, to read: untracked views of the context's internal DbSets, for the readers (the draft board,
/// stats, leaderboards, profiles, the merge's checks). The parts are changed only through their roots, which are loaded
/// with them through the Domain's loaders (TeamQueries, SeasonQueries, XboxUserQueries, MatchedMatchQueries).
/// </summary>
public static class AggregatePartQueries
{
    /// <summary>Every team's roster rows, to read.</summary>
    public static IQueryable<TeamPlayer> Rosters(this GrifballContext context) => context.TeamPlayers.AsNoTracking();

    /// <summary>Every season's titles, to read.</summary>
    public static IQueryable<PlayerChampionship> Championships(this GrifballContext context) => context.PlayerChampionships.AsNoTracking();

    /// <summary>Every season's timeslots, to read.</summary>
    public static IQueryable<SeasonAvailability> SeasonTimeslots(this GrifballContext context) => context.SeasonAvailability.AsNoTracking();

    /// <summary>Every account's past gamertags, to read.</summary>
    public static IQueryable<XboxGamertagHistory> PastGamertags(this GrifballContext context) => context.XboxGamertagHistory.AsNoTracking();

    /// <summary>Every result of a vote-decided match: one for each of its players still in it (<see cref="MatchedPlayer.CountedByVote"/>),
    /// a win or a loss. For the stats, which count them as games played with no box score.</summary>
    public static IQueryable<MatchedVoteResult> VoteResults(this GrifballContext context) => context.MatchedPlayers
        .Where(MatchedPlayer.CountedByVote)
        .Select(MatchedPlayer.VoteResult);
}
