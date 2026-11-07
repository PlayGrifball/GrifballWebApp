#nullable disable

namespace GrifballWebApp.Database.Models;
public class Match : AuditableEntity
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

    /// <summary>The custom game mode the match was played on (Waypoint's MatchInfo.UgcGameVariant): its asset and that
    /// asset's version. Null for a match saved before they were kept, until the backfill fetches them.</summary>
    public Guid? UgcGameVariantAssetID { get; set; }
    public Guid? UgcGameVariantVersionID { get; set; }

    /// <summary>How its players' Score and PersonalScore were decoded into their goals and the mode's other stats
    /// (MatchParticipant.Goals and the rest); null until it's been decided (the stats then read Score as goals when
    /// it's 20 or less).</summary>
    public CustomScoreLayout? ScoreLayout { get; set; }

    public SeriesGame SeriesGame { get; set; } // A season match's game
    public MatchedMatch MatchedMatch { get; private set; } // Late league: set from the match's side only (MatchedMatch.Report)

    public virtual ICollection<MatchTeam> MatchTeams { get; set; }
}
