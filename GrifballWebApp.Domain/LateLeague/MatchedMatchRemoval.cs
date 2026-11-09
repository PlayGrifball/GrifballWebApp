using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Domain.LateLeague;

/// <summary>
/// Deleting an archive game's <see cref="MatchedMatch"/>: a model operation (the match readies itself first, through its
/// internal <see cref="MatchedMatch.Discard"/>), so it stays beside the model; the context has no DbSet for its sides and
/// players, which go with it through the match.
/// </summary>
[AggregateAccess(typeof(MatchedMatch))]
public static class MatchedMatchRemoval
{
    /// <summary>
    /// Deletes an archive game's matched match with its sides and players (the match readies itself first,
    /// <see cref="MatchedMatch.Discard"/>, and refuses a queue match). An archive game has no votes.
    /// </summary>
    public static void RemoveArchivedMatch(this DbContext context, MatchedMatch match)
    {
        match.Discard();
        context.RemoveRange(match.Players);
        context.Remove(match);
        context.RemoveRange(match.HomeTeam, match.AwayTeam);
    }
}
