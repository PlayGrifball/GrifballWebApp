using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.TeamPage;

namespace GrifballWebApp.Server.TeamPage;
[ApiController]
public class TeamController : ControllerBase
{
    private readonly TeamPageService _teams;
    private readonly TeamHistoryService _history;
    public TeamController(TeamPageService teams, TeamHistoryService history)
    {
        _teams = teams;
        _history = history;
    }

    /// <summary>
    /// The team: its record (from its matches, or the one its source spreadsheet gives for a season whose matches are
    /// not in the app), roster and titles; its players' stats are GET seasons/{seasonID}/player-stats?teamID=.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("teams/{teamID:int}", Name = "Team")]
    [ProducesResponseType<TeamDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TeamDto>> Team([FromRoute] int teamID, CancellationToken ct)
        => await _teams.GetAsync(teamID, ct) is { } team ? team : this.NotFoundProblem("There is no such team");

    /// <summary>
    /// The franchise of the team <paramref name="teamID"/>: every season's team of its league with its name (trimmed,
    /// case ignored; a placeholder name like "Team 1": the team alone), newest first, with its record, roster, captain
    /// and titles, and the franchise's totals. 404 when there is no such team.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("teams/{teamID:int}/history", Name = "History")]
    [ProducesResponseType<TeamHistoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TeamHistoryDto>> History([FromRoute] int teamID, CancellationToken ct)
        => await _history.GetAsync(teamID, SeasonVisibility.For(User), ct) is { } history ? history : this.NotFoundProblem("There is no such team");

    /// <summary>A team's matches, soonest unplayed first, then played ones by scheduled time.</summary>
    [AllowAnonymous]
    [HttpGet("teams/{teamID:int}/matches", Name = "Matches")]
    [ProducesResponseType<MatchDto[]>(StatusCodes.Status200OK)]
    public Task<MatchDto[]> Matches([FromRoute] int teamID, CancellationToken ct)
    {
        return _teams.GetMatchesAsync(teamID, ct);
    }
}
