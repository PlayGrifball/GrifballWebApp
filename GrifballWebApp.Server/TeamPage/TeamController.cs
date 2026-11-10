using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.League;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.TeamPage;

namespace GrifballWebApp.Server.TeamPage;
[Route("[controller]/[action]")]
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
    /// not in the app), roster and titles; its players' stats are Season/GetSeasonPlayerStats?teamID=.
    /// </summary>
    [HttpGet("{teamID:int}", Name = "Team")]
    public Task<TeamDto?> Team([FromRoute] int teamID, CancellationToken ct) => _teams.GetAsync(teamID, ct);

    /// <summary>
    /// The franchise of the team <paramref name="teamID"/>: every season's team of its league with its name (trimmed,
    /// case ignored; a placeholder name like "Team 1": the team alone), newest first, with its record, roster, captain
    /// and titles, and the franchise's totals. Null when there is no such team.
    /// </summary>
    [HttpGet("{teamID:int}", Name = "History")]
    public Task<TeamHistoryDto?> History([FromRoute] int teamID, CancellationToken ct) => _history.GetAsync(teamID, ct);

    [HttpGet("{teamID:int}", Name = "Matches")]
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
