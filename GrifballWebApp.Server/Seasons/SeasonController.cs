using GrifballWebApp.Server.EventOrganizer;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Seasons;

[Route("[controller]/[action]")]
[ApiController]
public class SeasonController : ControllerBase
{
    private readonly EventOrganizerService _eventOrganizerService;
    private readonly SeasonService _seasonService;

    public SeasonController(EventOrganizerService eventOrganizerService, SeasonService seasonService)
    {
        _eventOrganizerService = eventOrganizerService;
        _seasonService = seasonService;
    }

    [HttpGet(Name = "GetCurrentSeasonID")]
    public Task<int> GetCurrentSeasonID(CancellationToken ct)
    {
        return _seasonService.GetCurrentSeasonID(ct);
    }

    [HttpGet("{seasonID:int}", Name = "GetSeasonName")]
    public Task<string?> GetSeasonName([FromRoute] int seasonID, CancellationToken ct)
    {
        return _seasonService.GetSeasonName(seasonID, ct);
    }

    [HttpGet("{seasonID:int}", Name = "GetSeasonSummary")]
    public async Task<ActionResult<SeasonSummaryDto>> GetSeasonSummary([FromRoute] int seasonID, CancellationToken ct)
    {
        var summary = await _seasonService.GetSeasonSummary(seasonID, ct);
        return summary is null ? NotFound() : summary;
    }

    [HttpGet("{seasonID:int}", Name = "GetSeasonPlayerStats")]
    public Task<List<SeasonPlayerStatDto>> GetSeasonPlayerStats([FromRoute] int seasonID, [FromQuery] int? teamID, CancellationToken ct)
    {
        return _seasonService.GetSeasonPlayerStats(seasonID, teamID, ct);
    }

    /// <summary>The season list (home page's past seasons, the seasons page): filtered, sorted and paged.</summary>
    [HttpGet(Name = "GetSeasonList")]
    public Task<GrifballWebApp.Server.Dtos.PaginationResult<SeasonListItemDto>> GetSeasons([FromQuery] SeasonListFilter filter, CancellationToken ct)
    {
        return _seasonService.GetSeasons(filter, ct);
    }
}
