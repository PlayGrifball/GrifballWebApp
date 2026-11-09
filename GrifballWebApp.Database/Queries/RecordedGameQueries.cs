using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Database.Queries;

/// <summary>
/// Games a commissioner entered by hand (manual match entry): <see cref="RecordedGame"/>s in a season match's series
/// that are <see cref="RecordedGame.IsManual"/>. One owner for what every service that deletes season matches (the
/// schedule, the bracket) has to do with them.
/// </summary>
public static class RecordedGameQueries
{
    /// <summary>
    /// Deletes the games entered by hand of the season matches about to be deleted (regenerating a schedule or a
    /// bracket): the database removes a match's series games with it, but not the recorded games they point to, which
    /// would be left in no series. In bulk, through the root: each game goes whole, its sides and players' lines with it
    /// (the database deletes them with their game: ON DELETE CASCADE).
    /// </summary>
    public static async Task DeleteManualForAsync(this IQueryable<RecordedGame> games, IQueryable<SeasonMatch> seasonMatches, CancellationToken ct = default)
    {
        var ids = await games
            .Where(RecordedGame.Manual)
            .Where(g => g.SeriesGame != null && seasonMatches.Any(sm => sm.SeasonMatchID == g.SeriesGame.SeasonMatchID))
            .Select(g => g.RecordedGameID)
            .ToListAsync(ct);
        if (ids.Count == 0)
            return;

        await games.Where(g => ids.Contains(g.RecordedGameID)).ExecuteDeleteAsync(ct);
    }
}
