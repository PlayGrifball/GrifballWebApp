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

    /// <param name="format">Count only the titles of seasons of this format; null: every format. A Late League season never
    /// counts (it has no champion).</param>
    [HttpGet(Name = "GetLeaderboards")]
    public Task<LeaderboardsDto> GetLeaderboards([FromQuery] SeasonFormat? format, CancellationToken ct)
        => _leaderboards.GetAsync(format, ct);
}
