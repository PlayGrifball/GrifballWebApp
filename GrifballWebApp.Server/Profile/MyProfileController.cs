using GrifballWebApp.Application.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using GrifballWebApp.Application.Historical;

namespace GrifballWebApp.Server.Profile;

/// <summary>
/// What a signed-in user changes about their own account from their profile: their region, and claims of past gamertags
/// (a sysadmin decides those on the claims page).
/// </summary>
[ApiController]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class MyProfileController : ControllerBase
{
    private readonly RegionService _regions;
    private readonly UserGamertagClaimService _claims;

    public MyProfileController(RegionService regions, UserGamertagClaimService claims)
    {
        _regions = regions;
        _claims = claims;
    }

    /// <summary>The regions a player can be in.</summary>
    [AllowAnonymous]
    [HttpGet("regions", Name = "GetRegions")]
    [ProducesResponseType<List<RegionDto>>(StatusCodes.Status200OK)]
    public Task<List<RegionDto>> Regions(CancellationToken ct)
        => _regions.List(ct);

    /// <summary>Sets the signed-in user's region (the import never overwrites it).</summary>
    [HttpPost("me/region", Name = "SetMyRegion")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> SetRegion([FromBody] SetRegionRequest request, CancellationToken ct)
    {
        if (GetUserID() is not int userId)
            return this.UnauthorizedProblem(NotSignedIn);
        return await _regions.Set(userId, request.RegionID, ct) switch
        {
            SetRegionResult.NoSuchRegion => this.BadRequestProblem("There is no such region"),
            SetRegionResult.NoSuchUser => this.NotFoundProblem("There is no such user"),
            _ => NoContent(),
        };
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
