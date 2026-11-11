using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Seasons;

namespace GrifballWebApp.Server.Seasons;

[Route("[controller]/[action]")]
[ApiController]
public class SeasonController : ControllerBase
{
    private readonly SeasonService _seasonService;

    public SeasonController(SeasonService seasonService)
    {
        _seasonService = seasonService;
    }

    [AllowAnonymous]
    [HttpGet(Name = "GetCurrentSeasonID")]
    public Task<int> GetCurrentSeasonID(CancellationToken ct)
    {
        return _seasonService.GetCurrentSeasonID(ct);
    }

    [AllowAnonymous]
    [HttpGet("{seasonID:int}", Name = "GetSeasonName")]
    public Task<string?> GetSeasonName([FromRoute] int seasonID, CancellationToken ct)
    {
        return _seasonService.GetSeasonName(seasonID, ct);
    }

    [AllowAnonymous]
    [HttpGet("{seasonID:int}", Name = "GetSeasonSummary")]
    public async Task<ActionResult<SeasonSummaryDto>> GetSeasonSummary([FromRoute] int seasonID, CancellationToken ct)
    {
        var summary = await _seasonService.GetSeasonSummary(seasonID, ct);
        return summary is null ? NotFound() : summary;
    }

    [AllowAnonymous]
    [HttpGet("{seasonID:int}", Name = "GetSeasonTeams")]
    public async Task<ActionResult<List<SeasonTeamDto>>> GetSeasonTeams([FromRoute] int seasonID, CancellationToken ct)
    {
        var teams = await _seasonService.GetSeasonTeams(seasonID, ct);
        return teams is null ? NotFound() : teams;
    }

    [AllowAnonymous]
    [HttpGet("{seasonID:int}", Name = "GetSeasonPlayerStats")]
    public Task<List<SeasonPlayerStatDto>> GetSeasonPlayerStats([FromRoute] int seasonID, [FromQuery] int? teamID, CancellationToken ct)
    {
        return _seasonService.GetSeasonPlayerStats(seasonID, teamID, ct);
    }

    /// <summary>The season list (home page's past seasons, the seasons page): filtered, sorted and paged.</summary>
    [AllowAnonymous]
    [HttpGet(Name = "GetSeasonList")]
    public Task<GrifballWebApp.Application.Dtos.PaginationResult<SeasonListItemDto>> GetSeasons([FromQuery] SeasonListFilter filter, CancellationToken ct)
    {
        return _seasonService.GetSeasons(filter, ct);
    }
}
