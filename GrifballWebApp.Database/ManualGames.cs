using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Database;

/// <summary>
/// Games a commissioner entered by hand (manual match entry): <see cref="RecordedGame"/>s in a season match's series
/// that are <see cref="RecordedGame.IsManual"/>. One owner for what every service that deletes season matches (the
/// schedule, the bracket) has to do with them.
/// </summary>
public static class ManualGames
{
    /// <summary>
    /// Deletes the games entered by hand of the season matches about to be deleted (regenerating a schedule or a
    /// bracket): the database removes a match's series games with it, but not the recorded games they point to, which
    /// would be left in no series. In bulk, past the game's methods: each goes whole, with its sides and lines.
    /// </summary>
    [AggregateAccess(typeof(RecordedGame))]
    public static async Task DeleteForAsync(GrifballContext context, IQueryable<SeasonMatch> seasonMatches, CancellationToken ct = default)
    {
        var ids = await context.SeriesGames
            .Where(g => g.RecordedGameID != null && g.RecordedGame!.Origin == RecordedGameOrigin.Manual
                && seasonMatches.Any(sm => sm.SeasonMatchID == g.SeasonMatchID))
            .Select(g => g.RecordedGameID!.Value)
            .ToListAsync(ct);
        if (ids.Count == 0)
            return;

        await context.RecordedGameParticipants.Where(p => ids.Contains(p.RecordedGameID)).ExecuteDeleteAsync(ct);
        await context.RecordedGameTeams.Where(t => ids.Contains(t.RecordedGameID)).ExecuteDeleteAsync(ct);
        await context.RecordedGames.Where(g => ids.Contains(g.RecordedGameID)).ExecuteDeleteAsync(ct);
    }
}
