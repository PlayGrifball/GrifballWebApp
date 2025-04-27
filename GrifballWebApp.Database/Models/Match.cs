#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class Match
{
    public Match()
    {
        MatchTeams = new HashSet<MatchTeam>();
    }
    public Guid MatchID { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration { get; set; }

    public DateTime? StatsPullDate { get; set; }

    //[InverseProperty(nameof(MatchLink.Match))]
    public MatchLink MatchLink { get; set; } // Regular season
    public MatchedMatch MatchedMatch { get; set; } // Late league

    public virtual ICollection<MatchTeam> MatchTeams { get; set; }
}
