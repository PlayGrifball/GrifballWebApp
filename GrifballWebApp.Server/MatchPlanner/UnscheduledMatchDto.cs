namespace GrifballWebApp.Server.MatchPlanner;

public record UnscheduledMatchDto
{
    public required int SeasonMatchID { get; set; }
    public required string HomeCaptain { get; set; }
    public required string AwayCaptain { get; set; }
}
