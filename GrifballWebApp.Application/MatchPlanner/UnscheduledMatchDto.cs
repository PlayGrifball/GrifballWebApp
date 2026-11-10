namespace GrifballWebApp.Application.MatchPlanner;

public record UnscheduledMatchDto
{
    public required int SeasonMatchID { get; set; }
    public required string HomeCaptain { get; set; }
    public required string AwayCaptain { get; set; }
    /// <summary>The home team as the schedule shows it: its name, then its captain in parentheses (when it has one and the
    /// name doesn't already say it).</summary>
    public required string HomeTeam { get; set; }
    public required string AwayTeam { get; set; }
    public required bool Complete { get; set; }
}
