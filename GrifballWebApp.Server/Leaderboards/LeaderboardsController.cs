using GrifballWebApp.Database.Models;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Leaderboards;

/// <summary>All-time leaderboards over every season, imported from the historical archive or played in the app alike.</summary>
[Route("[controller]/[action]")]
[ApiController]
public class LeaderboardsController : ControllerBase
{
    private readonly LeaderboardsService _leaderboards;

    public LeaderboardsController(LeaderboardsService leaderboards)
    {
        _leaderboards = leaderboards;
    }

    /// <param name="format">Count only the titles of seasons of this format; null: every format.</param>
    [HttpGet(Name = "GetLeaderboards")]
    public Task<LeaderboardsDto> GetLeaderboards([FromQuery] SeasonFormat? format, CancellationToken ct)
        => _leaderboards.GetAsync(format, ct);

    /// <summary>Finds a player's rank across championships and all tracked metrics.</summary>
    [HttpGet(Name = "GetPlayerRank")]
    public Task<PlayerAllTimeRankDto?> GetPlayerRank([FromQuery] string? query, [FromQuery] int? userId, [FromQuery] SeasonFormat? format, CancellationToken ct)
        => _leaderboards.GetPlayerRankAsync(query, userId, format, ct);

    /// <summary>Searches players by gamertag or past alias for autocomplete.</summary>
    [HttpGet(Name = "SearchPlayers")]
    public async Task<IReadOnlyList<PlayerSearchResultDto>> SearchPlayers([FromQuery] string query, CancellationToken ct)
        => await _leaderboards.SearchPlayersAsync(query, ct);
}
