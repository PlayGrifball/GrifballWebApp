using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.Seasons;

namespace GrifballWebApp.Server.Seasons;

/// <summary>The seasons as everyone sees them: the list, and each season's header, teams and stats.</summary>
[ApiController]
[AllowAnonymous]
public class SeasonController : ControllerBase
{
    private readonly SeasonService _seasonService;

    public SeasonController(SeasonService seasonService)
    {
        _seasonService = seasonService;
    }

    /// <summary>The ID of the season running now; 0 when none is.</summary>
    [HttpGet("seasons/current", Name = "GetCurrentSeasonID")]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    public Task<int> GetCurrentSeasonID(CancellationToken ct)
    {
        return _seasonService.GetCurrentSeasonID(ct);
    }

    /// <summary>A season's display name.</summary>
    [HttpGet("seasons/{seasonID:int}/name", Name = "GetSeasonName")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, "text/plain")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<string>> GetSeasonName([FromRoute] int seasonID, CancellationToken ct)
    {
        return await _seasonService.GetSeasonName(seasonID, ct) is { } name ? name : this.NotFoundProblem("There is no such season");
    }

    /// <summary>A season's header summary (name, dates, format).</summary>
    [HttpGet("seasons/{seasonID:int}/summary", Name = "GetSeasonSummary")]
    [ProducesResponseType<SeasonSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<SeasonSummaryDto>> GetSeasonSummary([FromRoute] int seasonID, CancellationToken ct)
    {
        var summary = await _seasonService.GetSeasonSummary(seasonID, ct);
        return summary is null ? this.NotFoundProblem("There is no such season") : summary;
    }

    /// <summary>A season's teams.</summary>
    [HttpGet("seasons/{seasonID:int}/teams", Name = "GetSeasonTeams")]
    [ProducesResponseType<List<SeasonTeamDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<List<SeasonTeamDto>>> GetSeasonTeams([FromRoute] int seasonID, CancellationToken ct)
    {
        var teams = await _seasonService.GetSeasonTeams(seasonID, ct);
        return teams is null ? this.NotFoundProblem("There is no such season") : teams;
    }

    /// <summary>A season's player stats; with a team, that team's players only.</summary>
    [HttpGet("seasons/{seasonID:int}/player-stats", Name = "GetSeasonPlayerStats")]
    [ProducesResponseType<List<SeasonPlayerStatDto>>(StatusCodes.Status200OK)]
    public Task<List<SeasonPlayerStatDto>> GetSeasonPlayerStats([FromRoute] int seasonID, [FromQuery] int? teamID, CancellationToken ct)
    {
        return _seasonService.GetSeasonPlayerStats(seasonID, teamID, ct);
    }

    /// <summary>The season list (home page's past seasons, the seasons page): filtered, sorted and paged.</summary>
    [HttpGet("seasons", Name = "GetSeasonList")]
    [ProducesResponseType<PaginationResult<SeasonListItemDto>>(StatusCodes.Status200OK)]
    public Task<PaginationResult<SeasonListItemDto>> GetSeasons([FromQuery] SeasonListFilter filter, CancellationToken ct)
    {
        return _seasonService.GetSeasons(filter, SeasonVisibility.For(User), ct);
    }
}
