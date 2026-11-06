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
    /// <summary>The season's division (Pro, Legacy, ...), the tier its champions win; null: one open tier. Archive
    /// seasons of different divisions are separate seasons (often named "... (Pro)"), so a stat row has no division of
    /// its own.</summary>
    public string Division { get; set; }
    /// <summary>The archive's key for a season imported from the historical archive: the workbook and sheet of its stats,
    /// "{sourceWorkbook}/{sourceSheet}" (e.g. "2018-sml18-fantasy-rey/Total Stats"), what a re-run of the import finds it
    /// by, even after its name is corrected in the archive. Unique where set; null for the app's own seasons, so set is
    /// what makes a season an imported one (teams, rosters, player stats and championships from its spreadsheets, see
    /// <see cref="HistoricalSourceUrl"/>). Otherwise it is a season like any other: what it lacks (signups, a schedule,
    /// matches) is simply not there. Its dates span the year its name gives, ending no later than the import.</summary>
    public string HistoricalSourceID { get; set; }
    /// <summary>The source workbook's link (Google Sheets) from the archive catalog, kept for provenance even when the
    /// sheet is gone. Null for the app's own seasons.</summary>
    public string HistoricalSourceUrl { get; set; }
    /// <summary>What kind of competition the season is (<see cref="SeasonFormat"/>): which sections its page has, and whether it
    /// crowns a champion. Seasons from before formats existed are league seasons (the column's default).</summary>
    public SeasonFormat Format { get; set; } = SeasonFormat.LeagueSeason;
    public virtual GameVersion GameVersion { get; set; }
    public virtual League League { get; set; }
    public virtual ICollection<Team> Teams { get; set; }
    public virtual ICollection<SeasonMatch> SeasonMatches { get; set; }
    public virtual ICollection<SeasonSignup> SeasonSignups { get; set; }
    public virtual ICollection<SeasonAvailability> SeasonAvailability { get; set; }
}

/// <summary>Queries over seasons.</summary>
public static class SeasonQueries
{
    /// <summary>
    /// The seasons anyone may see: started, or announced (their <see cref="Season.PublicAt"/> has passed), whose pages
    /// the quick search links to. (The season list shows only started seasons and the Discord events post only
    /// announced ones not yet over: narrower questions, kept as they are.)
    /// </summary>
    public static IQueryable<Season> Public(this IQueryable<Season> seasons, DateTime now) =>
        seasons.Where(s => s.SeasonStart <= now || (s.PublicAt != null && s.PublicAt <= now));
}
