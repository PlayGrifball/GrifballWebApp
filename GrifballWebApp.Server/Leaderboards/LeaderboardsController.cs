using GrifballWebApp.Domain.Models;
using GrifballWebApp.Server.Search;
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

    /// <summary>The player suggestions, by the toolbar search's rules: current name or past gamertag, each labelled current gamertag or archive.</summary>
    [HttpGet(Name = "SearchPlayers")]
    public Task<QuickSearchPlayerDto[]> SearchPlayers([FromQuery] string query, CancellationToken ct)
        => _leaderboards.SearchPlayersAsync(query, ct);
}
