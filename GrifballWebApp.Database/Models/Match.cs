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

    public SeriesGame SeriesGame { get; set; } // A season match's game
    public MatchedMatch MatchedMatch { get; set; } // Late league

    public virtual ICollection<MatchTeam> MatchTeams { get; set; }
}
