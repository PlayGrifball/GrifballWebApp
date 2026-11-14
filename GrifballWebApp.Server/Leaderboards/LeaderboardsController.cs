using Microsoft.AspNetCore.Authorization;
using GrifballWebApp.Domain.League;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.Search;

namespace GrifballWebApp.Server.Leaderboards;

/// <summary>All-time leaderboards over every season, imported from the historical archive or played in the app alike.</summary>
[ApiController]
public class LeaderboardsController : ControllerBase
{
    private readonly LeaderboardsService _leaderboards;

    public LeaderboardsController(LeaderboardsService leaderboards)
    {
        _leaderboards = leaderboards;
    }

    /// <summary>The all-time leaderboards.</summary>
    /// <param name="formats">The formats counted, titles and stats alike (repeat the parameter for several); none: every format.</param>
    /// <param name="ct">Cancelled when the caller goes away.</param>
    [AllowAnonymous]
    [HttpGet("leaderboards", Name = "GetLeaderboards")]
    [ProducesResponseType<LeaderboardsDto>(StatusCodes.Status200OK)]
    public Task<LeaderboardsDto> GetLeaderboards([FromQuery] LeaderboardFilter[]? formats, CancellationToken ct)
        => _leaderboards.GetAsync(formats, ct);

    /// <summary>Finds a player's rank across championships and all tracked metrics.</summary>
    /// <param name="query">A gamertag, current or past, looked up when <paramref name="userID"/> is left out.</param>
    /// <param name="userID">The player's user ID.</param>
    /// <param name="formats">The formats counted, titles and stats alike (repeat the parameter for several); none: every format.</param>
    /// <param name="ct">Cancelled when the caller goes away.</param>
    [AllowAnonymous]
    [HttpGet("leaderboards/player-rank", Name = "GetPlayerRank")]
    [ProducesResponseType<PlayerAllTimeRankDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PlayerAllTimeRankDto>> GetPlayerRank([FromQuery] string? query, [FromQuery] int? userID, [FromQuery] LeaderboardFilter[]? formats, CancellationToken ct)
        => await _leaderboards.GetPlayerRankAsync(query, userID, formats, ct) is { } rank ? rank : this.NotFoundProblem("No player matches that");

    /// <summary>The player suggestions, by the toolbar search's rules: current name or past gamertag, each labelled current gamertag or archive.</summary>
    [AllowAnonymous]
    [HttpGet("leaderboards/players", Name = "SearchPlayers")]
    [ProducesResponseType<QuickSearchPlayerDto[]>(StatusCodes.Status200OK)]
    public Task<QuickSearchPlayerDto[]> SearchPlayers([FromQuery] string query, CancellationToken ct)
        => _leaderboards.SearchPlayersAsync(query, ct);
}
