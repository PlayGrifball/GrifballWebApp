using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Spartan;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using GrifballWebApp.Application.Historical;

namespace GrifballWebApp.Server.Profile;

/// <summary>
/// What a signed-in user changes about their own account from their profile: their region, the picture that shows for them, and claims of past gamertags
/// (a sysadmin decides those on the claims page).
/// </summary>
[ApiController]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class MyProfileController : ControllerBase
{
    private readonly RegionService _regions;
    private readonly ProfilePictureService _pictures;
    private readonly UserGamertagClaimService _claims;
    private readonly SpartanService _spartans;

    public MyProfileController(RegionService regions, ProfilePictureService pictures, UserGamertagClaimService claims, SpartanService spartans)
    {
        _regions = regions;
        _pictures = pictures;
        _claims = claims;
        _spartans = spartans;
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
            SetRegionResult.NoSuchUser => this.NotFoundProblem(ApiProblems.NoSuchUser),
            _ => NoContent(),
        };
    }

    /// <summary>Sets which of the signed-in user's Xbox images (Halo emblem or gamer picture) shows for them.</summary>
    [HttpPost("me/profile-picture", Name = "SetMyProfilePicture")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> SetProfilePicture([FromBody] SetProfilePictureRequest request, CancellationToken ct)
    {
        if (GetUserID() is not int userId)
            return this.UnauthorizedProblem(NotSignedIn);
        if (!Enum.IsDefined(request.ProfilePicture))
            return this.BadRequestProblem("There is no such profile picture");
        return await _pictures.Set(userId, request.ProfilePicture, ct) ? NoContent() : this.NotFoundProblem(ApiProblems.NoSuchUser);
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

    /// <summary>The signed-in user's Spartan: their own choice if they made one, and its render.</summary>
    [HttpGet("me/spartan", Name = "GetMySpartan")]
    [ProducesResponseType<SpartanProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<SpartanProfileDto>> MySpartan(CancellationToken ct)
    {
        if (GetUserID() is not int userId)
            return this.UnauthorizedProblem(NotSignedIn);
        return await _spartans.Get(userId, ct) is { } dto ? dto : this.NotFoundProblem(ApiProblems.NoSuchUser);
    }

    /// <summary>What the render service can render: the cores, their pieces by region, the coatings and the visors.</summary>
    [HttpGet("spartan/catalog", Name = "GetSpartanCatalog")]
    [ProducesResponseType<SpartanCatalog>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<SpartanCatalog>> CatalogOfSpartans(CancellationToken ct)
        => await _spartans.Catalog(ct) is { } catalog ? catalog : this.Refusal(StatusCodes.Status503ServiceUnavailable, CatalogDown);

    /// <summary>Sets the Spartan the signed-in user's profile shows, instead of the one they have equipped, and asks for its render.</summary>
    [HttpPost("me/spartan", Name = "SetMySpartan")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> SetSpartan([FromBody] SpartanChoice choice, CancellationToken ct)
    {
        if (GetUserID() is not int userId)
            return this.UnauthorizedProblem(NotSignedIn);
        var result = await _spartans.Set(userId, choice, ct);
        return result.Outcome switch
        {
            SetSpartanOutcome.Invalid => this.BadRequestProblem(string.Join("; ", result.Problems)),
            SetSpartanOutcome.CatalogUnavailable => this.Refusal(StatusCodes.Status503ServiceUnavailable, CatalogDown),
            SetSpartanOutcome.NoSuchUser => this.NotFoundProblem(ApiProblems.NoSuchUser),
            _ => NoContent(),
        };
    }

    /// <summary>Goes back to the Spartan the signed-in user has equipped in Halo Infinite.</summary>
    [HttpPost("me/spartan/clear", Name = "ClearMySpartan")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> ClearSpartan(CancellationToken ct)
        => GetUserID() is not int userId ? this.UnauthorizedProblem(NotSignedIn)
            : await _spartans.Clear(userId, ct) ? NoContent() : this.NotFoundProblem(ApiProblems.NoSuchUser);

    /// <summary>Asks for the render of the signed-in user's Spartan (again); the render job makes it within minutes.</summary>
    [HttpPost("me/spartan/render", Name = "RenderMySpartan")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> RenderSpartan(CancellationToken ct)
        => GetUserID() is not int userId ? this.UnauthorizedProblem(NotSignedIn)
            : await _spartans.RequestRender(userId, ct) ? Accepted() : this.NotFoundProblem(ApiProblems.NoSuchUser);

    /// <summary>A user's Spartan as anyone sees it on their profile: their choice if they made one, and its render.</summary>
    [AllowAnonymous]
    [HttpGet("users/{userId:int}/spartan", Name = "GetUserSpartan")]
    [ProducesResponseType<SpartanProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<SpartanProfileDto>> UserSpartan([FromRoute] int userId, CancellationToken ct)
        => await _spartans.Get(userId, ct) is { } dto ? dto : this.NotFoundProblem(ApiProblems.NoSuchUser);

    /// <summary>A file of a user's rendered Spartan: glb (one model with the neutral and stance poses as clips), stance-body, stance-head, neutral-body or neutral-head.</summary>
    [AllowAnonymous]
    [HttpGet("users/{userId:int}/spartan/{name}", Name = "GetUserSpartanFile")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK, "image/png", "model/gltf-binary")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> SpartanFile([FromRoute] int userId, [FromRoute] string name, CancellationToken ct)
    {
        if (!SpartanService.FileNames.Contains(name) || await _spartans.File(userId, name, ct) is not { } file)
            return this.NotFoundProblem("There is no such file");
        Response.Headers.CacheControl = "public, max-age=300";
        return File(file.Content, file.ContentType);
    }

    private const string NotSignedIn = "You must be signed in";
    private const string CatalogDown = "The Spartan render service is not available right now";

    private int? GetUserID()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
