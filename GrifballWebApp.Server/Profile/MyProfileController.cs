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
[ApiController]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
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
    [HttpGet("regions", Name = "GetRegions")]
    [ProducesResponseType<List<RegionDto>>(StatusCodes.Status200OK)]
    public Task<List<RegionDto>> Regions(CancellationToken ct)
        => _context.Regions.AsNoTracking().OrderBy(r => r.RegionID)
            .Select(r => new RegionDto { RegionID = r.RegionID, RegionName = r.RegionName }).ToListAsync(ct);

    /// <summary>Sets the signed-in user's region (the import never overwrites it).</summary>
    [HttpPost("me/region", Name = "SetMyRegion")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> SetRegion([FromBody] SetRegionRequest request, CancellationToken ct)
    {
        if (GetUserID() is not int userId)
            return this.UnauthorizedProblem(NotSignedIn);
        if (request.RegionID is not null && !await _context.Regions.AnyAsync(r => r.RegionID == request.RegionID, ct))
            return this.BadRequestProblem("There is no such region");
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return this.NotFoundProblem("There is no such user");
        user.ChooseRegion(request.RegionID, RegionSource.User);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>The placeholder claiming a gamertag would merge, its seasons, and why it can't be claimed if it can't.</summary>
    [HttpGet("me/gamertag-claims/preview", Name = "PreviewGamertagClaim")]
    [ProducesResponseType<ClaimPreviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClaimPreviewDto>> PreviewClaim([FromQuery] string gamertag, CancellationToken ct)
        => GetUserID() is int userId ? await _claims.Preview(userId, gamertag, ct) : this.UnauthorizedProblem(NotSignedIn);

    /// <summary>Claims a past gamertag's placeholder: a pending claim for a sysadmin to decide (201, at the user's claims).</summary>
    [HttpPost("me/gamertag-claims", Name = "ClaimGamertag")]
    [ProducesResponseType<GamertagClaimDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<GamertagClaimDto>> ClaimGamertag([FromBody] SubmitGamertagClaimRequest request, CancellationToken ct)
        => GetUserID() is int userId
            ? CreatedAtAction(nameof(GamertagClaims), null, await _claims.Submit(userId, request, ct))
            : this.UnauthorizedProblem(NotSignedIn);

    /// <summary>The signed-in user's claims of past gamertags and their status.</summary>
    [HttpGet("me/gamertag-claims", Name = "GetMyGamertagClaims")]
    [ProducesResponseType<List<GamertagClaimDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GamertagClaimDto>>> GamertagClaims(CancellationToken ct)
        => GetUserID() is int userId ? await _claims.ClaimsOf(userId, ct) : this.UnauthorizedProblem(NotSignedIn);

    private const string NotSignedIn = "You must be signed in";

    private int? GetUserID()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
