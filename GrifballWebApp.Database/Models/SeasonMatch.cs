
namespace GrifballWebApp.Database.Models;
public class SeasonMatch : AuditableEntity
{
    public SeasonMatch()
    {
        SeriesGames = new HashSet<SeriesGame>();
    }
    public int SeasonMatchID { get; set; }
    public int SeasonID { get; set; }
    public Season Season { get; set; } = null!; // Should never be null if included in query
    public int? HomeTeamID { get; set; }
    public Team? HomeTeam { get; set; }
    public int? HomeTeamScore { get; set; }
    public SeasonMatchResult? HomeTeamResult { get; set; }
    public int? AwayTeamID { get; set; }
    public Team? AwayTeam { get; set; }
    public int? AwayTeamScore { get; set; }
    public SeasonMatchResult? AwayTeamResult { get; set; }
    public DateTime? ScheduledTime { get; set; }
    public int BestOf { get; set; }
    public ICollection<SeriesGame> SeriesGames { get; set; }
    public MatchBracketInfo? BracketMatch { get; set; }
    // Read-only: a request is made only by MatchReschedule.Request (EF uses the field)
    public IReadOnlyCollection<MatchReschedule> Reschedules => _reschedules;
    private readonly HashSet<MatchReschedule> _reschedules = [];

    /// <summary>Its pending reschedule request, the active one (one at most), when its requests are loaded.</summary>
    public MatchReschedule? PendingReschedule => _reschedules.SingleOrDefault(r => r.Status == RescheduleStatus.Pending);

    internal void AddReschedule(MatchReschedule reschedule) => _reschedules.Add(reschedule);
}

public enum SeasonMatchResult
{
    Won = 0,
    Loss = 1,
    Forfeit = 2,
    Bye = 3,
}

public enum SeasonMatchStatus
{
    /// <summary>
    /// No matches have been played yet for this season match
    /// </summary>
    NotPlayed = 0,
    /// <summary>
    /// Some matches have been played but the series is not yet complete
    /// </summary>
    InProgress = 1,
    /// <summary>
    /// All matches have been played for this series, or one or both team(s) forfeited
    /// </summary>
    Complete = 3,
}