using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using GrifballWebApp.Application.Draft;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Server.Mcp;

namespace GrifballWebApp.Server.Teams;

/// <summary>
/// A season's draft: its teams and player pool, the captains (placed, resorted, removed, locked) and the picks. Its pages
/// hear of every change once it is committed, through LiveHub (the teams' and the season's domain events).
/// </summary>
[ApiController]
public class TeamsController : ControllerBase
{
    private readonly TeamService _teamService;

    public TeamsController(TeamService teamService)
    {
        _teamService = teamService;
    }

    /// <summary>A season's draft teams, with their captains and picks.</summary>
    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/draft/teams", Name = "GetTeams")]
    [ProducesResponseType<List<TeamResponseDto>>(StatusCodes.Status200OK)]
    public Task<List<TeamResponseDto>> GetTeams([FromRoute] int seasonID, CancellationToken ct)
    {
        return _teamService.GetTeams(seasonID, ct);
    }

    /// <summary>A season's remaining player pool for the draft.</summary>
    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/draft/player-pool", Name = "GetPlayerPool")]
    [ProducesResponseType<List<PlayerDto>>(StatusCodes.Status200OK)]
    public Task<List<PlayerDto>> GetPlayerPool([FromRoute] int seasonID, CancellationToken ct)
    {
        return _teamService.GetPlayerPool(seasonID, ct);
    }

    /// <summary>The 404's reason for suggestions with no team to make them for.</summary>
    public const string NoTeamOnDeck = "There is no team to suggest picks for: no captains yet, or no such team in the season.";

    /// <summary>The most suggested picks one call gives.</summary>
    public const int MaxSuggestions = 25;

    /// <summary>How many picks to suggest for <paramref name="count"/> asked for: 1 to <see cref="MaxSuggestions"/>; none asked, the default.</summary>
    public static int HowManyPicks(int? count) => Math.Clamp(count ?? DraftHelperService.DefaultSuggestions, 1, MaxSuggestions);

    /// <summary>
    /// A season's remaining player pool with what a captain weighs: each player's region, whether they'd captain, the timeslots they
    /// signed up for and their career (null for one who never played).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/draft/pool-details", Name = "GetDraftPoolDetails")]
    [ProducesResponseType<List<DraftPoolPlayer>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<DraftPoolPlayer>> GetDraftPoolDetails([FromRoute] int seasonID, [FromServices] DraftHelperService helper, CancellationToken ct)
        => await helper.PoolAsync(seasonID, ct);

    /// <summary>
    /// The picks suggested for a team (the one on deck unless <paramref name="teamID"/> is given), best first, each with its score and
    /// reasons: how the player has played against the pool, the team's short timeslots they fill, and the captain's region.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/draft/suggestions", Name = "GetDraftSuggestions")]
    [ProducesResponseType<DraftSuggestions>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetDraftSuggestions([FromRoute] int seasonID, [FromQuery] int? teamID, [FromQuery] int? count,
        [FromServices] DraftHelperService helper, CancellationToken ct)
        => await helper.SuggestAsync(seasonID, teamID, HowManyPicks(count), ct) is { } suggestions
            ? Ok(suggestions)
            : this.NotFoundProblem(NoTeamOnDeck);

    /// <summary>Makes a signup a captain, with a team of their own: answers the team.</summary>
    [Authorize(Roles = "Commissioner")]
    [AllowAccessToken]
    [HttpPost("seasons/{seasonID:int}/draft/captains", Name = "AddCaptain")]
    [ProducesResponseType<CaptainAddedResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<CaptainAddedResponseDto>> AddCaptain([FromRoute] int seasonID, [FromBody] CaptainPlacementDto dto, CancellationToken ct)
    {
        dto.SeasonID = seasonID;
        var teamName = await _teamService.AddCaptain(dto, resortOnly: false, ct);
        return new CaptainAddedResponseDto { TeamName = teamName!, TeamID = await _teamService.CaptainsTeamID(dto.SeasonID, dto.PersonID, ct) };
    }

    /// <summary>Moves a captain to another place in the draft order.</summary>
    [Authorize(Roles = "Commissioner")]
    [AllowAccessToken]
    [HttpPost("seasons/{seasonID:int}/draft/captains/resort", Name = "ResortCaptain")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> ResortCaptain([FromRoute] int seasonID, [FromBody] CaptainPlacementDto dto, CancellationToken ct)
    {
        dto.SeasonID = seasonID;
        await _teamService.AddCaptain(dto, resortOnly: true, ct);
        return NoContent();
    }

    /// <summary>Removes a captain and disbands their team, returning its players to the pool.</summary>
    [Authorize(Roles = "Commissioner")]
    [AllowAccessToken]
    [HttpPost("seasons/{seasonID:int}/draft/captains/remove", Name = "RemoveCaptain")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> RemoveCaptain([FromRoute] int seasonID, [FromBody] RemoveCaptainDto dto, CancellationToken ct)
    {
        dto.SeasonID = seasonID;
        await _teamService.RemoveCaptain(dto, ct);
        return NoContent();
    }

    /// <summary>Removes a player from their team, back into the draft's player pool.</summary>
    [Authorize(Roles = "Commissioner")]
    [AllowAccessToken]
    [HttpPost("seasons/{seasonID:int}/draft/players/remove", Name = "RemovePlayerFromTeam")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> RemovePlayerFromTeam([FromRoute] int seasonID, [FromBody] RemovePlayerFromTeamRequestDto dto, CancellationToken ct)
    {
        dto.SeasonID = seasonID;
        await _teamService.RemovePlayerFromTeam(dto, ct);
        return NoContent();
    }

    /// <summary>Moves a player from one team to another, a commissioner's override outside the normal picks.</summary>
    [Authorize(Roles = "Commissioner")]
    [AllowAccessToken]
    [HttpPost("seasons/{seasonID:int}/draft/players/move", Name = "MovePlayerToTeam")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> MovePlayerToTeam([FromRoute] int seasonID, [FromBody] MovePlayerToTeamRequestDto dto, CancellationToken ct)
    {
        dto.SeasonID = seasonID;
        await _teamService.MovePlayerToTeam(dto, ct);
        return NoContent();
    }

    /// <summary>A pick: a captain (or a commissioner for them) adds a player to the captain's team.</summary>
    [Authorize(Roles = "Commissioner,Player")]
    [HttpPost("seasons/{seasonID:int}/draft/picks", Name = "AddPlayerToTeam")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> AddPlayerToTeam([FromRoute] int seasonID, [FromBody] AddPlayerToTeamRequestDto dto, CancellationToken ct)
    {
        dto.SeasonID = seasonID;
        var userID = GetUserID();
        if (userID == 0)
            return this.UnauthorizedProblem("You must be logged in to add a player to a team");

        await _teamService.AddPlayerToTeam(dto, userID, ct: ct);
        return NoContent();
    }

    /// <summary>Locks the draft order so captains can no longer be resorted or removed.</summary>
    [Authorize(Roles = "Commissioner")]
    [AllowAccessToken]
    [HttpPost("seasons/{seasonID:int}/draft/captains/lock", Name = "LockCaptains")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> LockCaptains([FromRoute] int seasonID, CancellationToken ct)
    {
        await _teamService.LockCaptains(seasonID, @lock: true, ct);
        return NoContent();
    }

    /// <summary>Unlocks the draft order, allowing captains to be resorted or removed again.</summary>
    [Authorize(Roles = "Commissioner")]
    [AllowAccessToken]
    [HttpPost("seasons/{seasonID:int}/draft/captains/unlock", Name = "UnlockCaptains")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> UnlockCaptains([FromRoute] int seasonID, CancellationToken ct)
    {
        await _teamService.LockCaptains(seasonID, @lock: false, ct);
        return NoContent();
    }

    /// <summary>Whether the season's draft order is currently locked.</summary>
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
