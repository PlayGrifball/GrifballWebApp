using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using GrifballWebApp.Application.Teams;

namespace GrifballWebApp.Server.Teams;

/// <summary>
/// A season's draft: its teams and player pool, the captains (placed, resorted, removed, locked) and the picks. Every
/// change is also sent to the draft's other viewers over TeamsHub (all but the connection that made it).
/// </summary>
[ApiController]
public class TeamsController : ControllerBase
{
    private readonly TeamService _teamService;

    public TeamsController(TeamService teamService)
    {
        _teamService = teamService;
    }

    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/draft/teams", Name = "GetTeams")]
    [ProducesResponseType<List<TeamResponseDto>>(StatusCodes.Status200OK)]
    public Task<List<TeamResponseDto>> GetTeams([FromRoute] int seasonID, CancellationToken ct)
    {
        return _teamService.GetTeams(seasonID, ct);
    }

    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/draft/player-pool", Name = "GetPlayerPool")]
    [ProducesResponseType<List<PlayerDto>>(StatusCodes.Status200OK)]
    public Task<List<PlayerDto>> GetPlayerPool([FromRoute] int seasonID, CancellationToken ct)
    {
        return _teamService.GetPlayerPool(seasonID, ct);
    }

    /// <summary>Makes a signup a captain, with a team of their own: answers the team.</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("seasons/{seasonID:int}/draft/captains", Name = "AddCaptain")]
    [ProducesResponseType<CaptainAddedResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<CaptainAddedResponseDto>> AddCaptain([FromRoute] int seasonID, [FromBody] CaptainPlacementDto dto, [FromHeader] string? signalRConnectionID, CancellationToken ct)
    {
        if (RouteIDs.Differs(dto.SeasonID, seasonID))
            return this.BadRequestProblem(RouteIDs.Mismatch("season"));
        dto.SeasonID = seasonID;
        var teamName = await _teamService.AddCaptain(dto, resortOnly: false, signalRConnectionID, ct);
        return new CaptainAddedResponseDto { TeamName = teamName!, TeamID = await _teamService.CaptainsTeamID(dto.SeasonID, dto.PersonID, ct) };
    }

    /// <summary>Moves a captain to another place in the draft order.</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("seasons/{seasonID:int}/draft/captains/resort", Name = "ResortCaptain")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> ResortCaptain([FromRoute] int seasonID, [FromBody] CaptainPlacementDto dto, [FromHeader] string? signalRConnectionID, CancellationToken ct)
    {
        if (RouteIDs.Differs(dto.SeasonID, seasonID))
            return this.BadRequestProblem(RouteIDs.Mismatch("season"));
        dto.SeasonID = seasonID;
        await _teamService.AddCaptain(dto, resortOnly: true, signalRConnectionID, ct);
        return NoContent();
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost("seasons/{seasonID:int}/draft/captains/remove", Name = "RemoveCaptain")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> RemoveCaptain([FromRoute] int seasonID, [FromBody] RemoveCaptainDto dto, [FromHeader] string? signalRConnectionID, CancellationToken ct)
    {
        if (RouteIDs.Differs(dto.SeasonID, seasonID))
            return this.BadRequestProblem(RouteIDs.Mismatch("season"));
        dto.SeasonID = seasonID;
        await _teamService.RemoveCaptain(dto, signalRConnectionID, ct);
        return NoContent();
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost("seasons/{seasonID:int}/draft/players/remove", Name = "RemovePlayerFromTeam")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> RemovePlayerFromTeam([FromRoute] int seasonID, [FromBody] RemovePlayerFromTeamRequestDto dto, [FromHeader] string? signalRConnectionID, CancellationToken ct)
    {
        if (RouteIDs.Differs(dto.SeasonID, seasonID))
            return this.BadRequestProblem(RouteIDs.Mismatch("season"));
        dto.SeasonID = seasonID;
        await _teamService.RemovePlayerFromTeam(dto, signalRConnectionID, ct);
        return NoContent();
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost("seasons/{seasonID:int}/draft/players/move", Name = "MovePlayerToTeam")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> MovePlayerToTeam([FromRoute] int seasonID, [FromBody] MovePlayerToTeamRequestDto dto, [FromHeader] string? signalRConnectionID, CancellationToken ct)
    {
        if (RouteIDs.Differs(dto.SeasonID, seasonID))
            return this.BadRequestProblem(RouteIDs.Mismatch("season"));
        dto.SeasonID = seasonID;
        await _teamService.MovePlayerToTeam(dto, signalRConnectionID, ct);
        return NoContent();
    }

    /// <summary>A pick: a captain (or a commissioner for them) adds a player to the captain's team.</summary>
    [Authorize(Roles = "Commissioner,Player")]
    [HttpPost("seasons/{seasonID:int}/draft/picks", Name = "AddPlayerToTeam")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> AddPlayerToTeam([FromRoute] int seasonID, [FromBody] AddPlayerToTeamRequestDto dto, [FromHeader] string? signalRConnectionID, CancellationToken ct)
    {
        if (RouteIDs.Differs(dto.SeasonID, seasonID))
            return this.BadRequestProblem(RouteIDs.Mismatch("season"));
        dto.SeasonID = seasonID;
        var userID = GetUserID();
        if (userID == 0)
            return this.UnauthorizedProblem("You must be logged in to add a player to a team");

        await _teamService.AddPlayerToTeam(dto, userID, signalRConnectionID, ct);
        return NoContent();
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost("seasons/{seasonID:int}/draft/captains/lock", Name = "LockCaptains")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> LockCaptains([FromRoute] int seasonID, [FromHeader] string? signalRConnectionID, CancellationToken ct)
    {
        await _teamService.LockCaptains(seasonID, @lock: true, signalRConnectionID, ct);
        return NoContent();
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost("seasons/{seasonID:int}/draft/captains/unlock", Name = "UnlockCaptains")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> UnlockCaptains([FromRoute] int seasonID, [FromHeader] string? signalRConnectionID, CancellationToken ct)
    {
        await _teamService.LockCaptains(seasonID, @lock: false, signalRConnectionID, ct);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/draft/captains/locked", Name = "AreCaptainsLocked")]
    [ProducesResponseType<bool>(StatusCodes.Status200OK)]
    public Task<bool> AreCaptainsLocked([FromRoute] int seasonID, CancellationToken ct)
    {
        return _teamService.AreCaptainsLocked(seasonID, ct);
    }

    private int GetUserID()
    {
        var userIDStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIDStr == null)
            return 0;
        return int.TryParse(userIDStr, out var userID) ? userID : 0;
    }
}
