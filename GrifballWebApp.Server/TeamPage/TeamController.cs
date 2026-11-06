using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.TeamPage;
[Route("[controller]/[action]")]
[ApiController]
public class TeamController : ControllerBase
{
    private readonly GrifballContext _context;
    private readonly TeamPageService _teams;
    private readonly TeamHistoryService _history;
    public TeamController(GrifballContext context, TeamPageService teams, TeamHistoryService history)
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
                OtherTeamName = x.AwayTeam.TeamName,
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
                    OtherTeamName = x.HomeTeam.TeamName,
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

public class TeamDto
{
    public required string TeamName { get; set; }
    public required GrifballWebApp.Server.Seasons.SeasonInfoDto Season { get; set; }
    /// <summary>Wins and Losses are the source spreadsheet's (the season's matches are not in the app).</summary>
    public bool RecordFromSource { get; set; }
    /// <summary>The tiers whose title the team won.</summary>
    public string[] Titles { get; set; } = [];
    public required int Wins { get; set; }
    public required int Losses { get; set; }
    public required int TBD { get; set; }
    public required PlayerDto[] Players { get; set; }
}

public class PlayerDto
{
    public required int TeamPlayerID { get; set; }
    public required int UserID { get; set; }
    public required string Gamertag { get; set; }
    public required int? DraftCaptainOrder { get; set; }
    public required int? DraftRound { get; set; }
    /// <summary>The team's captain: its drafted captain, or the one its title names.</summary>
    public bool IsCaptain { get; set; }
    /// <summary>Where the player plays from.</summary>
    public string? Region { get; set; }
}

public class MatchDto
{
    public required int SeasonMatchID { get; set; }
    public required DateTime? ScheduledTime { get; set; }
    public required int BestOf { get; set; }
    public required int? Score { get; set; }
    public required SeasonMatchResult? Result { get; set; }
    public required int? OtherTeamID { get; set; }
    public required string? OtherTeamName { get; set; }
    public required int? OtherScore { get; set; }
    public required SeasonMatchResult? OtherResult { get; set; }

}