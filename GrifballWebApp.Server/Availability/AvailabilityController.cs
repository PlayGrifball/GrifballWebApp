using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Availability;

namespace GrifballWebApp.Server.Availability;

[Route("[controller]/[action]")]
[ApiController]
public class AvailabilityController : ControllerBase
{
    private readonly AvailabilityService _availabilityService;

    public AvailabilityController(AvailabilityService availabilityService)
    {
        _availabilityService = availabilityService;
    }

    [AllowAnonymous]
    [HttpGet(Name = "GetSeasonAvailability")]
    public Task<TimeslotDto[]> GetSeasonAvailability([FromQuery] int seasonID, CancellationToken ct)
    {
        return _availabilityService.GetSeasonAvailability(seasonID, ct);
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost(Name = "UpdateSeasonAvailability")]
    public Task UpdateSeasonAvailability([FromBody] SeasonAvailabilityDto a, CancellationToken ct)
    {
        return _availabilityService.UpdateSeasonAvailability(a, ct);
    }
}
