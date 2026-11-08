using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Seasons;

/// <summary>A season named in a list (the seasons a user played), with what its link needs.</summary>
public class SeasonRefDto
{
    public int SeasonID { get; set; }
    public required string SeasonName { get; set; }
    public int Year { get; set; }

    /// <summary>
    /// The seasons each user played, oldest first: those they have stats, a roster spot or a title in. What the
    /// admin pages show to tell who used a gamertag when (claims, duplicate suggestions, a user's claim preview).
    /// </summary>
    public static async Task<Dictionary<int, List<SeasonRefDto>>> PlayedByAsync(GrifballContext context, IEnumerable<int> userIDs, CancellationToken ct)
    {
        var ids = userIDs.Distinct().ToList();
        if (ids.Count == 0)
            return [];
        var played = await context.PlayerSeasonStats.AsNoTracking().Where(s => ids.Contains(s.UserID)).Select(s => new { s.UserID, s.SeasonID })
            .Union(context.Rosters().Where(t => ids.Contains(t.UserID)).Select(t => new { t.UserID, t.Team.SeasonID }))
            .Union(context.Championships().Where(c => ids.Contains(c.UserID)).Select(c => new { c.UserID, c.SeasonID }))
            .Join(context.Seasons, p => p.SeasonID, s => s.SeasonID, (p, s) => new { p.UserID, s.SeasonID, s.SeasonName, s.SeasonStart })
            .ToListAsync(ct);
        return played.GroupBy(p => p.UserID).ToDictionary(
            u => u.Key,
            u => u.OrderBy(p => p.SeasonStart).ThenBy(p => p.SeasonID)
                .Select(p => new SeasonRefDto { SeasonID = p.SeasonID, SeasonName = p.SeasonName, Year = p.SeasonStart.Year }).ToList());
    }
}
