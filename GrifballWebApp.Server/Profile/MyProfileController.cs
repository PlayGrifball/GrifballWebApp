using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using GrifballWebApp.Application.Historical;

namespace GrifballWebApp.Server.Profile;

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

/// <summary>
/// What a signed-in user changes about their own account from their profile: their region, and claims of past gamertags
/// (a sysadmin decides those on the claims page).
/// </summary>
[Route("[controller]/[action]")]
[ApiController]
[Authorize]
public class MyProfileController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly UserGamertagClaimService _claims;

    public MyProfileController(IApplicationDbContext context, UserGamertagClaimService claims)
    {
        _context = context;
        _claims = claims;
    }

    /// <summary>The regions a player can be in.</summary>
    [AllowAnonymous]
    [HttpGet(Name = "GetRegions")]
    public Task<List<RegionDto>> Regions(CancellationToken ct)
        => _context.Regions.AsNoTracking().OrderBy(r => r.RegionID)
            .Select(r => new RegionDto { RegionID = r.RegionID, RegionName = r.RegionName }).ToListAsync(ct);

    /// <summary>Sets the signed-in user's region (the import never overwrites it).</summary>
    [HttpPost(Name = "SetMyRegion")]
    public async Task<IActionResult> SetRegion([FromBody] SetRegionRequest request, CancellationToken ct)
    {
        if (GetUserID() is not int userId)
            return Unauthorized();
        if (request.RegionID is not null && !await _context.Regions.AnyAsync(r => r.RegionID == request.RegionID, ct))
            return BadRequest("There is no such region");
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return NotFound();
        user.ChooseRegion(request.RegionID, RegionSource.User);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>The placeholder claiming a gamertag would merge, its seasons, and why it can't be claimed if it can't.</summary>
    [HttpGet(Name = "PreviewGamertagClaim")]
    public async Task<ActionResult<ClaimPreviewDto>> PreviewClaim([FromQuery] string gamertag, CancellationToken ct)
        => GetUserID() is int userId ? await _claims.Preview(userId, gamertag, ct) : Unauthorized();

    /// <summary>Claims a past gamertag's placeholder: a pending claim for a sysadmin to decide.</summary>
    [HttpPost(Name = "ClaimGamertag")]
    public async Task<ActionResult<GamertagClaimDto>> ClaimGamertag([FromBody] SubmitGamertagClaimRequest request, CancellationToken ct)
        => GetUserID() is int userId ? await _claims.Submit(userId, request, ct) : Unauthorized();

    /// <summary>The signed-in user's claims of past gamertags and their status.</summary>
    [HttpGet(Name = "GetMyGamertagClaims")]
    public async Task<ActionResult<List<GamertagClaimDto>>> GamertagClaims(CancellationToken ct)
        => GetUserID() is int userId ? await _claims.ClaimsOf(userId, ct) : Unauthorized();

    private int? GetUserID()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
