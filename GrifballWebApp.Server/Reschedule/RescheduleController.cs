using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using GrifballWebApp.Application.Reschedule;

namespace GrifballWebApp.Server.Reschedule;

/// <summary>Reschedule requests: asked for by a match's players, decided by the organizers.</summary>
[ApiController]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
public class RescheduleController : ControllerBase
{
    private const string ProblemJson = "application/problem+json";

    private readonly RescheduleService _rescheduleService;

    public RescheduleController(RescheduleService rescheduleService)
    {
        _rescheduleService = rescheduleService;
    }

    /// <summary>Asks to move a match to a new time: a 201 with the request's ID, at the match's page. GlobalExceptionHandler
    /// answers the service's refusals (not allowed, forbidden, one already active).</summary>
    [HttpPost("season-matches/{seasonMatchID:int}/reschedules", Name = "RequestReschedule")]
    [ProducesResponseType<RescheduleRequestedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<IActionResult> RequestReschedule([FromRoute] int seasonMatchID, [FromBody] RescheduleRequestDto dto, CancellationToken ct)
    {
        dto.SeasonMatchID = seasonMatchID;
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            var reschedule = await _rescheduleService.RequestRescheduleAsync(dto, userId, ct);
            return CreatedAtRoute("GetSeasonMatchPage", new { seasonMatchID }, new RescheduleRequestedDto(reschedule.MatchRescheduleID));
        }
        catch (ArgumentException ex)
        {
            // The only one the service throws: no such match
            return this.NotFoundProblem(ex.Message);
        }
    }

    /// <summary>Approves or rejects a reschedule request.</summary>
    [HttpPost("reschedules/{rescheduleID:int}/decision", Name = "ProcessReschedule")]
    [Authorize(Roles = "Commissioner,Sysadmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<IActionResult> ProcessReschedule([FromRoute] int rescheduleID, [FromBody] ProcessRescheduleDto dto, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            await _rescheduleService.ProcessRescheduleAsync(rescheduleID, dto, userId, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return this.NotFoundProblem(ex.Message);
        }
    }

    /// <summary>The reschedule requests still awaiting a decision.</summary>
    [HttpGet("reschedules/pending", Name = "GetPendingReschedules")]
    [Authorize(Roles = "Commissioner,Sysadmin")]
    [ProducesResponseType<List<RescheduleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public Task<List<RescheduleDto>> GetPendingReschedules(CancellationToken ct)
    {
        return _rescheduleService.GetPendingReschedulesAsync(ct);
    }

    /// <summary>Opens the request's Discord thread, for the teams and organizers to talk it over.</summary>
    [HttpPost("reschedules/{rescheduleID:int}/discord-thread", Name = "CreateDiscordThread")]
    [Authorize(Roles = "Commissioner,Sysadmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> CreateDiscordThread([FromRoute] int rescheduleID, CancellationToken ct)
    {
        try
        {
            await _rescheduleService.CreateDiscordThreadAsync(rescheduleID, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return this.NotFoundProblem(ex.Message);
        }
    }
}

/// <summary>A reschedule request made: its ID.</summary>
public record RescheduleRequestedDto(int RescheduleID);
