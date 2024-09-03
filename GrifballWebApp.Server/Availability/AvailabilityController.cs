﻿using GrifballWebApp.Server.Availability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Availability;

[Route("[controller]/[action]")]
[ApiController]
public class AvailabilityController : ControllerBase
{
    private readonly ILogger<AvailabilityController> _logger;
    private readonly AvailabilityService _availabilityService;

    public AvailabilityController(ILogger<AvailabilityController> logger, AvailabilityService availabilityService)
    {
        _logger = logger;
        _availabilityService = availabilityService;
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost(Name = "UpdateSeasonAvailability")]
    public Task UpdateSeasonAvailability([FromBody] SeasonAvailabilityDto a, CancellationToken ct)
    {
        return _availabilityService.UpdateSeasonAvailability(a, ct);
    }
}
