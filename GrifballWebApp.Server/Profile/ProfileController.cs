using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using GrifballWebApp.Application.Profile;

namespace GrifballWebApp.Server.Profile;

[ApiController]
public class ProfileController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ISetGamertagService _setGamertagService;
    private readonly ProfileStatsService _profiles;
    private readonly ProfileCommendationsService _commendations;

    public ProfileController(IApplicationDbContext context, ISetGamertagService setGamertagService, ProfileStatsService profiles, ProfileCommendationsService commendations)
    {
        _context = context;
        _setGamertagService = setGamertagService;
        _profiles = profiles;
        _commendations = commendations;
    }

    /// <summary>A player's current gamertag.</summary>
    [AllowAnonymous]
    [HttpGet("users/{userID:int}/gamertag", Name = "GetGamertag")]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ProfileDto>> GetGamertag([FromRoute] int userID, CancellationToken ct)
    {
        var profile = await _context.Users
            .Where(x => x.Id == userID)
            .Select(x => new ProfileDto()
            {
                Gamertag = x.XboxUser!.Gamertag
            })
            .FirstOrDefaultAsync(ct);
        return profile is null ? this.NotFoundProblem("There is no such user") : profile;
    }

    /// <summary>A player's profile stats.</summary>
    [AllowAnonymous]
    [HttpGet("users/{userID:int}/stats", Name = "GetProfileStats")]
    [ProducesResponseType<ProfileStatsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ProfileStatsDto>> GetProfileStats([FromRoute] int userID, CancellationToken ct)
    {
        var profile = await _profiles.GetAsync(userID, ct);
        if (profile is null)
            return this.NotFoundProblem("There is no such user");
        return profile;
    }

    /// <summary>The player's most earned Halo Infinite medals; empty for an unknown user or one with no Xbox account.</summary>
    [AllowAnonymous]
    [HttpGet("users/{userID:int}/commendations", Name = "GetCommendations")]
    [ProducesResponseType<ProfileCommendationsDto>(StatusCodes.Status200OK)]
    public Task<ProfileCommendationsDto> GetCommendations([FromRoute] int userID, [FromQuery] int top = ProfileCommendationsService.DefaultTop, CancellationToken ct = default)
    {
        return _commendations.GetAsync(userID, top, ct);
    }

    /// <summary>Sets a player's gamertag. A player may only set their own.</summary>
    [Authorize(Roles = "Player")]
    [HttpPost("users/{userID:int}/gamertag", Name = "SetGamertag")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> SetGamertag([FromRoute] int userID, [FromBody] SetGamertagRequest request, CancellationToken ct)
    {
        var id = GetUserID();
        if (id != userID)
            return this.ForbiddenProblem("You can only set your own gamertag");

        var errorMessage = await _setGamertagService.SetGamertag(userID, request.Gamertag, ct);

        if (errorMessage is not null)
            return this.BadRequestProblem(errorMessage);

        return NoContent();
    }

    private int? GetUserID()
    {
        var stringName = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (stringName == null)
            return null;
        var parsed = int.TryParse(stringName, out var id);
        return parsed ? id : null;
    }
}

/// <summary>A gamertag to set on one's own profile.</summary>
public record SetGamertagRequest(string Gamertag);
