using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.LateLeague;

/// <summary>Late League: matchmaking, the archive's seasons of it and the queue's games alike.</summary>
[Route("[controller]/[action]")]
[ApiController]
public class LateLeagueController : ControllerBase
{
    private readonly MatchmakingStatsService _stats;

    public LateLeagueController(MatchmakingStatsService stats)
    {
        _stats = stats;
    }

    [HttpGet(Name = "GetLateLeagueSeasons")]
    public Task<List<MatchmakingSeasonDto>> GetSeasons(CancellationToken ct) => _stats.GetSeasonsAsync(ct);

    [HttpGet("{matchmakingSeasonID:int}", Name = "GetLateLeagueSeason")]
    public async Task<ActionResult<MatchmakingSeasonDetailDto>> GetSeason([FromRoute] int matchmakingSeasonID, CancellationToken ct)
        => await _stats.GetSeasonAsync(matchmakingSeasonID, ct) is { } season ? season : NotFound();
}
