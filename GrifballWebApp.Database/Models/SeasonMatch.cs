#nullable disable

namespace GrifballWebApp.Database.Models;
public class SeasonMatch : AuditableEntity
{
    public SeasonMatch()
    {
        MatchLinks = new HashSet<MatchLink>();
    }
    public int SeasonMatchID { get; set; }
    public int SeasonID { get; set; }
    public Season Season { get; set; }
    public int? HomeTeamID { get; set; }
    public Team HomeTeam { get; set; }
    public int? HomeTeamScore { get; set; }
    public SeasonMatchResult? HomeTeamResult { get; set; }
    public int? AwayTeamID { get; set; }
    public Team AwayTeam { get; set; }
    public int? AwayTeamScore { get; set; }
    public SeasonMatchResult? AwayTeamResult { get; set; }
    public DateTime? ScheduledTime { get; set; }
    public int BestOf { get; set; }
    public ICollection<MatchLink> MatchLinks { get; set; }
    public MatchBracketInfo BracketMatch { get; set; }
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