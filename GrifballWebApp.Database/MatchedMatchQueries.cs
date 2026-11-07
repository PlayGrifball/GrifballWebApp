using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Database;

/// <summary>
/// How a <see cref="MatchedMatch"/> is loaded: as a whole, with what its rules read (its sides, their players and its
/// votes). A match whose votes weren't loaded answers its rules wrongly, not with an error, so load the root through
/// these, never by hand. Queries that only count or filter (<see cref="MatchedMatch.HasPlayer"/>) need none of it.
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

    /// <summary>
    /// A tracked match's players and votes as saved now: others may have voted, or been kicked, since it was loaded (each
    /// Discord interaction in a context of its own). A query doesn't overwrite a tracked row, so each is reloaded; then
    /// the votes saved since are loaded. Its own unsaved changes stay.
    /// </summary>
    public static async Task RefreshPlayersAndVotesAsync(this GrifballContext context, MatchedMatch match, CancellationToken ct = default)
    {
        IEnumerable<object> rows = match.Players.Cast<object>().Concat(match.MatchedWinnerVotes).Concat(match.MatchedKickVotes);
        foreach (var row in rows.Where(r => context.Entry(r).State is EntityState.Unchanged).ToList())
            await context.Entry(row).ReloadAsync(ct);
        var entry = context.Entry(match);
        await entry.Collection(m => m.MatchedWinnerVotes).Query().LoadAsync(ct);
        await entry.Collection(m => m.MatchedKickVotes).Query().LoadAsync(ct);
    }
}
