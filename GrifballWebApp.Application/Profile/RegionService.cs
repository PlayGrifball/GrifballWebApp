using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Profile;

public class RegionDto
{
    public int RegionID { get; set; }
    public required string RegionName { get; set; }
}

public class SetRegionRequest
{
    /// <summary>Null clears it.</summary>
    public int? RegionID { get; set; }
}

/// <summary>How setting a region went.</summary>
public enum SetRegionResult
{
    Set,
    NoSuchRegion,
    NoSuchUser,
}

/// <summary>The regions a player can be in, and a player choosing theirs. The profile page and /me region both use it.</summary>
public class RegionService
{
    private readonly IApplicationDbContext _context;

    public RegionService(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>The regions a player can be in.</summary>
    public Task<List<RegionDto>> List(CancellationToken ct = default)
        => _context.Regions.AsNoTracking().OrderBy(r => r.RegionID)
            .Select(r => new RegionDto { RegionID = r.RegionID, RegionName = r.RegionName }).ToListAsync(ct);

    /// <summary>Sets the user's region, as chosen by them (the import never overwrites it); null clears it.</summary>
    public async Task<SetRegionResult> Set(int userId, int? regionId, CancellationToken ct = default)
    {
        if (regionId is not null && !await _context.Regions.AnyAsync(r => r.RegionID == regionId, ct))
            return SetRegionResult.NoSuchRegion;
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return SetRegionResult.NoSuchUser;
        user.ChooseRegion(regionId, RegionSource.User);
        await _context.SaveChangesAsync(ct);
        return SetRegionResult.Set;
    }
}
