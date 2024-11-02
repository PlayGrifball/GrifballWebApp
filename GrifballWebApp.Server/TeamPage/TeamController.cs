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
    public TeamController(GrifballContext context)
    {
        _context = context;
    }

    [HttpGet("{teamID:int}", Name = "Team")]
    public Task<TeamDto?> Team([FromRoute] int teamID, CancellationToken ct)
    {
        return _context.Teams.Where(x => x.TeamID == teamID)
            .Select(x => new TeamDto()
            {
                TeamName = x.TeamName,
                Wins = x.HomeMatches.Count(x => x.HomeTeamResult == Database.Models.SeasonMatchResult.Won) + x.AwayMatches.Count(x => x.AwayTeamResult == Database.Models.SeasonMatchResult.Won),
                Losses = x.HomeMatches.Count(x => x.HomeTeamResult != Database.Models.SeasonMatchResult.Won && x.HomeTeamResult != null) + x.AwayMatches.Count(x => x.AwayTeamResult != Database.Models.SeasonMatchResult.Won && x.AwayTeamResult != null),
                TBD = x.HomeMatches.Count(x => x.HomeTeamResult == null) + x.AwayMatches.Count(x => x.AwayTeamResult == null),
                Players = x.TeamPlayers.Select(x => new PlayerDto()
                {
                    TeamPlayerID = x.TeamPlayerID,
                    UserID = x.UserID,
                    Gamertag = x.User.XboxUser.Gamertag,
                    DraftCaptainOrder = x.DraftCaptainOrder,
                    DraftRound = x.DraftRound,
                }).OrderBy(x => x.DraftCaptainOrder != null ? 0 : x.DraftRound).ToArray(),
            })
            .FirstOrDefaultAsync(ct);
    }

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