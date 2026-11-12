using Microsoft.AspNetCore.Authorization;
using GrifballWebApp.Application.TeamStandings;

using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.TeamStandings;

[ApiController]
public class TeamStandingsController : ControllerBase
{
    private readonly TeamStandingsService _teamStandingsService;

    public TeamStandingsController(TeamStandingsService teamStandingsService)
    {
        _teamStandingsService = teamStandingsService;
    }

    /// <summary>A season's team standings.</summary>
    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/standings", Name = "GetTeamStandings")]
    [ProducesResponseType<TeamStandingDto[]>(StatusCodes.Status200OK)]
    public Task<TeamStandingDto[]> GetTeamStandings([FromRoute] int seasonID, CancellationToken ct)
    {
        return _teamStandingsService.GetTeamStandings(seasonID, ct);
    }
}
