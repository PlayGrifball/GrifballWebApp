namespace GrifballWebApp.Application.Scheduler;

/// <summary>
/// One match of a schedule the season scheduler tries: its teams and its time, in league wall-clock time. What is wrong with
/// the time is the season's rules' to say (ScheduleChecker, through SeasonScheduleEvaluator), each match its own length.
/// </summary>
public struct SeasonMatchGene
{
    public required int SeasonMatchID { get; set; }
    public required int HomeTeamID { get; set; }
    public required int AwayTeamID { get; set; }
    public required DateTime ScheduledTime { get; set; }
}
