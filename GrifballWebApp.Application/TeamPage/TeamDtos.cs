using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Application.TeamPage;

public class TeamDto
{
    public required string TeamName { get; set; }
    public required GrifballWebApp.Application.Seasons.SeasonInfoDto Season { get; set; }
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
