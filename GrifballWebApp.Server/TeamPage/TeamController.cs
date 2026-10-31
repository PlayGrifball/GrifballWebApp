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

    /// <summary>
    /// The team: its record (from its matches, or the one its source spreadsheet gives for a season whose matches are
    /// not in the app), roster and titles; its players' stats are Season/GetSeasonPlayerStats?teamID=.
    /// </summary>
    [HttpGet("{teamID:int}", Name = "Team")]
    public async Task<TeamDto?> Team([FromRoute] int teamID, CancellationToken ct)
    {
        var team = await _context.Teams.Where(x => x.TeamID == teamID)
            .Select(x => new TeamDto()
            {
                TeamName = x.TeamName,
                SeasonID = x.SeasonID,
                IsHistorical = x.Season.IsHistorical,
                HistoricalSourceUrl = x.Season.HistoricalSourceUrl,
                SeasonHasEnded = x.Season.SeasonEnd < DateTime.UtcNow,
                Wins = 0,
                Losses = 0,
                TBD = x.HomeMatches.Count(x => x.HomeTeamResult == null) + x.AwayMatches.Count(x => x.AwayTeamResult == null),
                Titles = x.PlayerChampionships.Select(c => c.Tier).Distinct().ToArray(),
                Players = x.TeamPlayers.Select(x => new PlayerDto()
                {
                    TeamPlayerID = x.TeamPlayerID,
                    UserID = x.UserID,
                    // As the app names a user everywhere (UserNames): a placeholder of an imported season has no Xbox account
                    Gamertag = x.User.XboxUser != null ? x.User.XboxUser.Gamertag
                        : x.User.DiscordUser != null ? x.User.DiscordUser.DiscordUsername
                        : x.User.DisplayName ?? x.User.UserName,
                    DraftCaptainOrder = x.DraftCaptainOrder,
                    DraftRound = x.DraftRound,
                    IsCaptain = x.CaptainTeam != null || x.DraftCaptainOrder != null,
                }).OrderBy(x => x.IsCaptain ? 0 : 1).ThenBy(x => x.DraftRound).ThenBy(x => x.Gamertag).ToArray(),
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);
        if (team is null)
            return null;
        // Counted as the standings count it
        var record = await _context.Teams.Where(x => x.TeamID == teamID).Select(GrifballWebApp.Server.TeamStandings.TeamRecord.Of).FirstAsync(ct);
        (team.Wins, team.Losses, team.RecordFromSource) = (record.Wins, record.Losses, record.FromSource);
        // A placeholder of the import is shown by its gamertag, never its internal user name
        foreach (var player in team.Players)
            player.Gamertag = GrifballWebApp.Server.Extensions.UserNames.WithoutPlaceholderPrefix(player.Gamertag) ?? "";
        return team;
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
    public int SeasonID { get; set; }
    /// <summary>The season was backfilled from the historical archive: <see cref="HistoricalSourceUrl"/> is its spreadsheet.</summary>
    public bool IsHistorical { get; set; }
    public string? HistoricalSourceUrl { get; set; }
    /// <summary>Wins and Losses are the source spreadsheet's (the season's matches are not in the app).</summary>
    public bool RecordFromSource { get; set; }
    /// <summary>The team's season has ended: a section without data says it wasn't recorded.</summary>
    public bool SeasonHasEnded { get; set; }
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