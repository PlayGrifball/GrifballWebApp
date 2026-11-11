using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using GrifballWebApp.Application.Signups;

namespace GrifballWebApp.Server.Signups;

/// <summary>A season's signups: the public list, and the signed-in player's own signup and its timeslots.</summary>
[ApiController]
public class SignupsController : ControllerBase
{
    private const string NotSignedIn = "You must be logged in to sign up for a season";

    private readonly SignupsService _signupsService;

    public SignupsController(SignupsService signupsService)
    {
        _signupsService = signupsService;
    }

    /// <summary>The season's signups, each by the name the app shows for the player (never their login name).</summary>
    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/signups", Name = "GetSignups")]
    [ProducesResponseType<List<SignupResponseDto>>(StatusCodes.Status200OK)]
    public Task<List<SignupResponseDto>> GetSignups([FromRoute] int seasonID, CancellationToken ct)
    {
        return _signupsService.GetSignups(seasonID: seasonID, ct);
    }

    /// <summary>The season's timeslots, checked where the signed-in player's signup has them, in their time zone (offset in minutes).</summary>
    [Authorize]
    [HttpGet("seasons/{seasonID:int}/signups/mine/timeslots", Name = "GetTimeslots")]
    [ProducesResponseType<TimeslotDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<TimeslotDto[]>> GetTimeslots([FromRoute] int seasonID, [FromQuery] int offset, CancellationToken ct)
    {
        var userID = GetUserID();
        if (userID == 0)
            return this.UnauthorizedProblem(NotSignedIn);

        return await _signupsService.GetTimeslots(seasonID: seasonID, offset: offset, userID: userID, ct);
    }

    /// <summary>The signed-in player's signup for the season; 204 when they have none.</summary>
    [Authorize(Roles = "Player,Sysadmin")]
    [HttpGet("seasons/{seasonID:int}/signups/mine", Name = "GetSignup")]
    [ProducesResponseType<SignupResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> GetSignup([FromRoute] int seasonID, [FromQuery] int offset, CancellationToken ct)
    {
        var userID = GetUserID();
        if (userID == 0)
            return this.UnauthorizedProblem(NotSignedIn);

        return await _signupsService.GetSignup(seasonID: seasonID, offset: offset, userID: userID, ct) is { } signup ? Ok(signup) : NoContent();
    }

    /// <summary>Signs the signed-in player up for the season, or changes their signup.</summary>
    [Authorize(Roles = "Player,Sysadmin")]
    [HttpPost("seasons/{seasonID:int}/signups/mine", Name = "UpsertSignup")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> UpsertSignup([FromRoute] int seasonID, [FromBody] SignupRequestDto signupDto, CancellationToken ct)
    {
        if (RouteIDs.Differs(signupDto.SeasonID, seasonID))
            return this.BadRequestProblem(RouteIDs.Mismatch("season"));
        var userID = GetUserID();
        if (userID == 0)
            return this.UnauthorizedProblem(NotSignedIn);

        signupDto.SeasonID = seasonID;
        // The signed-in player's own signup, whoever the body names
        signupDto.UserID = userID;
        // Closed signups, no gamertag or a team name too long: refused by GlobalExceptionHandler as 400 ProblemDetails
        await _signupsService.UpsertSignup(signupDto, ct);
        return NoContent();
    }

    private int GetUserID()
    {
        var userIDStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIDStr == null)
            return 0;
        return int.TryParse(userIDStr, out var userID) ? userID : 0;
    }
}
