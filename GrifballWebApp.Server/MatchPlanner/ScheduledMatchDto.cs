namespace GrifballWebApp.Server.MatchPlanner;

public class ScheduledMatchDto
{
    public required int SeasonMatchID { get; set; }
    public required string HomeCaptain { get; set; }
    public required string AwayCaptain { get; set; }
    public required bool Complete { get; set; }
    public required DateTime Time { get; set; }
}
