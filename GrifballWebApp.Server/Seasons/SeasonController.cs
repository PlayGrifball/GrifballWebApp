using GrifballWebApp.Server.EventOrganizer;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Seasons;

[Route("api/[controller]/[action]")]
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
}
