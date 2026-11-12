using Microsoft.AspNetCore.Authorization;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.League;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.TeamPage;

namespace GrifballWebApp.Server.TeamPage;
[ApiController]
public class TeamController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly TeamPageService _teams;
    private readonly TeamHistoryService _history;
    public TeamController(IApplicationDbContext context, TeamPageService teams, TeamHistoryService history)
    {
        _context = context;
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
        => await _history.GetAsync(teamID, ct) is { } history ? history : this.NotFoundProblem("There is no such team");

    /// <summary>A team's matches, soonest unplayed first, then played ones by scheduled time.</summary>
    [AllowAnonymous]
    [HttpGet("teams/{teamID:int}/matches", Name = "Matches")]
    [ProducesResponseType<MatchDto[]>(StatusCodes.Status200OK)]
    public Task<MatchDto[]> Matches([FromRoute] int teamID, CancellationToken ct)
    {
        return _context.SeasonMatches.Where(x => x.HomeTeamID == teamID)
            .Select(x => new MatchDto()
            {
                SeasonMatchID = x.SeasonMatchID,
                ScheduledTime = x.ScheduledTime,
                BestOf = x.BestOf,
                OtherTeamID = x.AwayTeamID,
                OtherTeamName = x.AwayTeam!.TeamName,
                Score = x.HomeTeamScore,
                OtherScore = x.AwayTeamScore,
                Result = x.HomeTeamResult,
                OtherResult = x.AwayTeamResult,
            })
            .Concat(_context.SeasonMatches.Where(x => x.AwayTeamID == teamID)
                .Select(x => new MatchDto()
                {
                    SeasonMatchID = x.SeasonMatchID,
                    ScheduledTime = x.ScheduledTime,
                    BestOf = x.BestOf,
                    OtherTeamID = x.HomeTeamID,
                    OtherTeamName = x.HomeTeam!.TeamName,
                    Score = x.AwayTeamScore,
                    OtherScore = x.HomeTeamScore,
                    Result = x.AwayTeamResult,
                    OtherResult = x.HomeTeamResult,
                }))
            // Order so that matches that still need to be played are first, in order of scheduled date.
            .OrderBy(x => x.Result == null ? 0 : 1) // Note: use Result and not Score which will be null in case of forfeits/byes
            .ThenBy(x => x.ScheduledTime)
            .ToArrayAsync(ct);
    }
}
