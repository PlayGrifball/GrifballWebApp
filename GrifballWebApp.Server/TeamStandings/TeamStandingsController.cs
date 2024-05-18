using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.TeamStandings;

[Route("[controller]/[action]")]
[ApiController]
public class TeamStandingsController : ControllerBase
{
    private readonly TeamStandingsService _teamStandingsService;

    public TeamStandingsController(TeamStandingsService teamStandingsService)
    {
        _teamStandingsService = teamStandingsService;
    }

    [HttpGet("{seasonID:int}", Name = "GetTeamStandings")]
    public Task<TeamStandingDto[]> GetTeamStandings([FromRoute] int seasonID, CancellationToken ct)
    {
        return _teamStandingsService.GetTeamStandings(seasonID, ct);
    }
}
