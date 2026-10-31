#nullable disable

namespace GrifballWebApp.Database.Models;
public class Season : AuditableEntity
{
    public Season()
    {
        Teams = new HashSet<Team>();
        SeasonMatches = new HashSet<SeasonMatch>();
        SeasonSignups = new HashSet<SeasonSignup>();
        SeasonAvailability = new HashSet<SeasonAvailability>();
    }
    public int SeasonID { get; set; }
    public string SeasonName { get; set; }
    public bool CaptainsLocked { get; set; }
    /// <summary>
    /// The date and time when the season is/was made public (posted to the Discord events channel). Null: not public
    /// yet, as a season is when it is created, until a commissioner sets it.
    /// </summary>
    public DateTime? PublicAt { get; set; }
    public DateTime SignupsOpen { get; set; }
    public DateTime SignupsClose { get; set; }
    public DateTime DraftStart { get; set; }
    public DateTime SeasonStart { get; set; }
    public DateTime SeasonEnd { get; set; }
    public int? GameVersionID { get; set; }
    /// <summary>The league the season is in; the app's own seasons are in <see cref="League.DefaultLeagueID"/>.</summary>
    public int LeagueID { get; set; } = League.DefaultLeagueID;
    public string Division { get; set; }
    /// <summary>Imported from the historical archive: no signups, teams or matches in the app, only player stats and
    /// championships. Left out of the app's season lists.</summary>
    public bool IsHistorical { get; set; }
    /// <summary>The archive's key for a historical season, "{league slug}/{season name slug}": what a re-run of the
    /// import finds it by. Unique where set; null for the app's own seasons.</summary>
    public string HistoricalSourceID { get; set; }
    public virtual GameVersion GameVersion { get; set; }
    public virtual League League { get; set; }
    public virtual ICollection<Team> Teams { get; set; }
    public virtual ICollection<SeasonMatch> SeasonMatches { get; set; }
    public virtual ICollection<SeasonSignup> SeasonSignups { get; set; }
    public virtual ICollection<SeasonAvailability> SeasonAvailability { get; set; }
}
