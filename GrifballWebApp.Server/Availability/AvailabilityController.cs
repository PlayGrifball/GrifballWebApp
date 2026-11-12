using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Availability;

namespace GrifballWebApp.Server.Availability;

/// <summary>The times a season offers for its matches, which its signups choose from.</summary>
[ApiController]
public class AvailabilityController : ControllerBase
{
    private readonly AvailabilityService _availabilityService;

    public AvailabilityController(AvailabilityService availabilityService)
    {
        _availabilityService = availabilityService;
    }

    /// <summary>A season's offered timeslots.</summary>
    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/availability", Name = "GetSeasonAvailability")]
    [ProducesResponseType<TimeslotDto[]>(StatusCodes.Status200OK)]
    public Task<TimeslotDto[]> GetSeasonAvailability([FromRoute] int seasonID, CancellationToken ct)
    {
        return _availabilityService.GetSeasonAvailability(seasonID, ct);
    }

    /// <summary>Replaces a season's offered timeslots. Commissioners only.</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("seasons/{seasonID:int}/availability", Name = "UpdateSeasonAvailability")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> UpdateSeasonAvailability([FromRoute] int seasonID, [FromBody] SeasonAvailabilityDto a, CancellationToken ct)
    {
        a.SeasonID = seasonID;
        await _availabilityService.UpdateSeasonAvailability(a, ct);
        return NoContent();
    }
}
