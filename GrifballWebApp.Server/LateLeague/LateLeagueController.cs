using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.LateLeague;

namespace GrifballWebApp.Server.LateLeague;

/// <summary>Late League: the Discord queue's matchmaking (random, skill-matched sides), as it is played now and the archive's
/// seasons of it.</summary>
[ApiController]
public class LateLeagueController : ControllerBase
{
    private readonly MatchmakingStatsService _stats;

    public LateLeagueController(MatchmakingStatsService stats)
    {
        _stats = stats;
    }

    [AllowAnonymous]
    [HttpGet("late-league/queue", Name = "GetLateLeagueQueue")]
    [ProducesResponseType<MatchmakingQueueDto>(StatusCodes.Status200OK)]
    public Task<MatchmakingQueueDto> GetQueue(CancellationToken ct) => _stats.GetQueueAsync(ct);

    [AllowAnonymous]
    [HttpGet("late-league/seasons", Name = "GetLateLeagueSeasons")]
    [ProducesResponseType<List<MatchmakingSeasonDto>>(StatusCodes.Status200OK)]
    public Task<List<MatchmakingSeasonDto>> GetSeasons(CancellationToken ct) => _stats.GetSeasonsAsync(ct);

    [AllowAnonymous]
    [HttpGet("late-league/seasons/{matchmakingSeasonID:int}", Name = "GetLateLeagueSeason")]
    [ProducesResponseType<MatchmakingSeasonDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MatchmakingSeasonDetailDto>> GetSeason([FromRoute] int matchmakingSeasonID, CancellationToken ct)
        => await _stats.GetSeasonAsync(matchmakingSeasonID, ct) is { } season ? season : this.NotFoundProblem("There is no such Late League season");
}
