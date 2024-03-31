using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Seasons;

public class SeasonService
{
    private readonly GrifballContext _context;
    public SeasonService(GrifballContext context)
    {
        _context = context;
    }

    public Task<int> GetCurrentSeasonID(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return _context.Seasons
            .Where(season => season.SeasonStart <= now && now <= season.SeasonEnd)
            .Select(season => season.SeasonID)
            .FirstOrDefaultAsync(ct);
    }

    public Task<string?> GetSeasonName(int seasonID, CancellationToken ct = default)
    {
        return _context.Seasons
            .Where(season => season.SeasonID == seasonID)
            .Select(season => season.SeasonName)
            .FirstOrDefaultAsync(ct);
    }
}
