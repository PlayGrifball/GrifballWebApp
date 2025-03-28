﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.EventOrganizer;

[Route("[controller]/[action]")]
[ApiController]
[Authorize(Roles = "Commissioner,Sysadmin")]
public class EventOrganizerController : ControllerBase
{
    private readonly EventOrganizerService _eventOrganizerService;

    public EventOrganizerController(EventOrganizerService eventOrganizerService)
    {
        _eventOrganizerService = eventOrganizerService;
    }

    [HttpGet(Name = "GetSeasons")]
    public async Task<IActionResult> GetSeasons(CancellationToken ct)
    {
        return Ok(await _eventOrganizerService.GetSeasons(ct));
    }

    [HttpGet("{seasonID:int}", Name = "GetSeason")]
    public async Task<IActionResult> GetSeason([FromRoute] int seasonID, CancellationToken ct)
    {
        return Ok(await _eventOrganizerService.GetSeason(seasonID, ct));
    }

    [HttpPost(Name = "UpsertSeason")]
    public async Task<IActionResult> UpsertSeason([FromBody] SeasonDto seasonDto, CancellationToken ct)
    {
        return Ok(await _eventOrganizerService.UpsertSeason(seasonDto, ct));
    }

}
