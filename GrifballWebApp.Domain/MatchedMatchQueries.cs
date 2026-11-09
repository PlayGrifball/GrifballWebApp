using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Domain;

/// <summary>
/// How a <see cref="MatchedMatch"/> is read: as a whole, with what its rules read (its sides, their players and its
/// votes). A match whose votes weren't loaded answers its rules wrongly, not with an error, so every rule that reads its
/// players or votes gets a match loaded through these. A transition that reads neither (<see cref="MatchedMatch.AttachThread"/>)
/// may load the match alone, and queries that only count or filter (<see cref="MatchedMatch.HasPlayer"/>) need none of it.
/// </summary>
public static class MatchedMatchQueries
{
    /// <summary>The match with its sides, their players, and its winner and kick votes: all its transitions need.</summary>
    public static IQueryable<MatchedMatch> WithPlayersAndVotes(this IQueryable<MatchedMatch> matches) => matches
        .Include(m => m.HomeTeam.Players)
        .Include(m => m.AwayTeam.Players)
        .Include(m => m.MatchedWinnerVotes)
        .Include(m => m.MatchedKickVotes)
        .AsSplitQuery();

    /// <summary>The same, and its players' users with their gamertags and Discord names: for the messages that name them.</summary>
    public static IQueryable<MatchedMatch> WithPlayerNames(this IQueryable<MatchedMatch> matches) => matches
        .WithPlayersAndVotes()
        .Include(m => m.HomeTeam.Players).ThenInclude(p => p.User.XboxUser)
        .Include(m => m.HomeTeam.Players).ThenInclude(p => p.User.DiscordUser)
        .Include(m => m.AwayTeam.Players).ThenInclude(p => p.User.XboxUser)
        .Include(m => m.AwayTeam.Players).ThenInclude(p => p.User.DiscordUser);
}
