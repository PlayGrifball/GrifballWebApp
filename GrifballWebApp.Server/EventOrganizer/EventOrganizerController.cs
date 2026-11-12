using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.EventOrganizer;

namespace GrifballWebApp.Server.EventOrganizer;

/// <summary>The organizers' seasons: each season's settings (dates, league, format), and the choices they are made from.</summary>
[ApiController]
[Authorize(Roles = "Commissioner,Sysadmin")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public class EventOrganizerController : ControllerBase
{
    private readonly EventOrganizerService _eventOrganizerService;

    public EventOrganizerController(EventOrganizerService eventOrganizerService)
    {
        _eventOrganizerService = eventOrganizerService;
    }

    /// <summary>The seasons, for the organizers' settings page. Commissioners and sysadmins only.</summary>
    [HttpGet("seasons/settings", Name = "GetSeasons")]
    [ProducesResponseType<List<SeasonDto>>(StatusCodes.Status200OK)]
    public Task<List<SeasonDto>> GetSeasons(CancellationToken ct)
    {
        return _eventOrganizerService.GetSeasons(ct);
    }

    /// <summary>A season's settings, for the organizers' settings page. Commissioners and sysadmins only.</summary>
    [HttpGet("seasons/{seasonID:int}/settings", Name = "GetSeason")]
    [ProducesResponseType<SeasonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<SeasonDto>> GetSeason([FromRoute] int seasonID, CancellationToken ct)
    {
        return await _eventOrganizerService.GetSeason(seasonID, ct) is { } season ? season : this.NotFoundProblem("There is no such season");
    }

    /// <summary>The game versions a season can be set to. Commissioners and sysadmins only.</summary>
    [HttpGet("game-versions", Name = "GetGameVersions")]
    [ProducesResponseType<List<GameVersionOptionDto>>(StatusCodes.Status200OK)]
    public Task<List<GameVersionOptionDto>> GetGameVersions(CancellationToken ct) => _eventOrganizerService.GetGameVersions(ct);

    /// <summary>The leagues a season can be set to. Commissioners and sysadmins only.</summary>
    [HttpGet("leagues", Name = "GetLeagues")]
    [ProducesResponseType<List<LeagueOptionDto>>(StatusCodes.Status200OK)]
    public Task<List<LeagueOptionDto>> GetLeagues(CancellationToken ct) => _eventOrganizerService.GetLeagues(ct);

    /// <summary>
    /// Saves a season's settings, and answers its ID: a new season (SeasonID 0) is a 201 at its settings, an existing
    /// one a 200.
    /// </summary>
    [HttpPost("seasons/settings", Name = "UpsertSeason")]
    [ProducesResponseType<int>(StatusCodes.Status201Created)]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<int>> UpsertSeason([FromBody] SeasonDto seasonDto, CancellationToken ct)
    {
        var creating = seasonDto.SeasonID == 0;
        var seasonID = await _eventOrganizerService.UpsertSeason(seasonDto, ct);
        return creating ? CreatedAtAction(nameof(GetSeason), new { seasonID }, seasonID) : seasonID;
    }
}
