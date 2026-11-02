using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.LateLeague;

/// <summary>Late League: the Discord queue's matchmaking (random, skill-matched sides), as it is played now and the archive's
/// seasons of it.</summary>
[Route("[controller]/[action]")]
[ApiController]
public class LateLeagueController : ControllerBase
{
    private readonly MatchmakingStatsService _stats;

    public LateLeagueController(MatchmakingStatsService stats)
    {
        _stats = stats;
    }

    [HttpGet(Name = "GetLateLeagueQueue")]
    public Task<MatchmakingQueueDto> GetQueue(CancellationToken ct) => _stats.GetQueueAsync(ct);

    [HttpGet(Name = "GetLateLeagueSeasons")]
    public Task<List<MatchmakingSeasonDto>> GetSeasons(CancellationToken ct) => _stats.GetSeasonsAsync(ct);

    [HttpGet("{matchmakingSeasonID:int}", Name = "GetLateLeagueSeason")]
    public async Task<ActionResult<MatchmakingSeasonDetailDto>> GetSeason([FromRoute] int matchmakingSeasonID, CancellationToken ct)
        => await _stats.GetSeasonAsync(matchmakingSeasonID, ct) is { } season ? season : NotFound();
}
