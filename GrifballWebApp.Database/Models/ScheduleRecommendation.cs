namespace GrifballWebApp.Database.Models;

/// <summary>
/// A time for each of a season's regular matches, proposed by the schedule recommendation job (the genetic algorithm,
/// ScheduleService): its best schedule, how good it is, and whether a commissioner applied it to the matches.
/// </summary>
public class ScheduleRecommendation : AuditableEntity
{
    public int ScheduleRecommendationID { get; set; }
    public int SeasonID { get; set; }
    public Season Season { get; set; } = null!;
    /// <summary>Who asked for it (a user name).</summary>
    public string? RequestedBy { get; set; }
    /// <summary>0 to 1: 1 is a schedule with no overlap and every team available (SeasonScheduleEvaluator).</summary>
    public double Fitness { get; set; }
    /// <summary>The generations the algorithm ran.</summary>
    public int Generations { get; set; }
    /// <summary>When its times were set on the matches, and by whom; null until then.</summary>
    public DateTime? AppliedAt { get; set; }
    public string? AppliedBy { get; set; }
    public List<ScheduleRecommendationMatch> Matches { get; set; } = [];
}

/// <summary>One match's proposed time in a <see cref="ScheduleRecommendation"/>, and what is wrong with it.</summary>
public class ScheduleRecommendationMatch
{
    public int ScheduleRecommendationMatchID { get; set; }
    public int ScheduleRecommendationID { get; set; }
    public ScheduleRecommendation ScheduleRecommendation { get; set; } = null!;
    /// <summary>The season match (no foreign key: a match deleted since is skipped when the times are applied).</summary>
    public int SeasonMatchID { get; set; }
    public DateTime ScheduledTime { get; set; }
    /// <summary>Overlaps another of either team's matches.</summary>
    public bool Overlaps { get; set; }
    public bool HomeTeamAvailable { get; set; }
    public bool AwayTeamAvailable { get; set; }
}
