#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A source of the historical archive: a Google Sheets workbook, a page or a channel the worker downloads itself
/// (HistoricalSourceService). The rows come from the app's own catalog (HistoricalSourceCatalog), inserted and kept up to
/// date at runtime, so a fresh environment needs no setup; only <see cref="Enabled"/> is a sysadmin's.
/// Not temporal: the worker writes its fetch status on every refresh; who changed Enabled is in the audit columns.
/// </summary>
public class HistoricalSource : AuditableEntity
{
    /// <summary>The catalog's ID, e.g. 2023-da-wl23-stats: the "sourceWorkbook" half of a season's HistoricalSourceID.</summary>
    public string HistoricalSourceID { get; set; }
    /// <summary>e.g. AGLA-2023, Late-League, VODs.</summary>
    public string Category { get; set; }
    public int? Year { get; set; }
    public string Title { get; set; }
    /// <summary>The link people open (a Google Sheets edit link, the page): stored on the seasons it makes.</summary>
    public string Url { get; set; }
    public HistoricalSourceKind Kind { get; set; }
    /// <summary>A Google Sheets workbook's ID.</summary>
    public string SheetKey { get; set; }
    /// <summary>The tab a CSV export reads (Google's gviz CSV, one tab per download).</summary>
    public string Gid { get; set; }
    /// <summary>The name the CSV's tab goes by when parsed (what the parse config names it).</summary>
    public string CsvSheetName { get; set; }
    /// <summary>A Wayback capture's timestamp (yyyyMMddHHmmss): the capture is permanent, the live page may not be.</summary>
    public string WaybackTimestamp { get; set; }
    /// <summary>The league, game (GameVersion slug, or "multi") and format of the seasons it holds, for the page.</summary>
    public string League { get; set; }
    public string GameVersion { get; set; }
    public string Format { get; set; }
    /// <summary>What the import reads from it (HistoricalSourceConfig as JSON): its seasons' tabs, Waypoint game lists,
    /// recorded games, titles, players list. Null for a source the import doesn't read (kept as a backup).</summary>
    public string Config { get; set; }
    /// <summary>Downloaded on each refresh. A disabled source keeps its snapshots, and the import still reads the last one.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Gone for good (deleted, private, no capture): never downloaded, listed for the record.</summary>
    public bool IsDead { get; set; }
    public DateTime? LastFetchedAt { get; set; }
    /// <summary>"Changed", "Unchanged" or why the download failed.</summary>
    public string LastFetchStatus { get; set; }
    /// <summary>When its content last changed (its latest snapshot).</summary>
    public DateTime? LastChangedAt { get; set; }

    public virtual ICollection<HistoricalSourceSnapshot> Snapshots { get; set; } = new List<HistoricalSourceSnapshot>();
}

public enum HistoricalSourceKind
{
    /// <summary>export?format=xlsx; a tab's gviz CSV (<see cref="HistoricalSource.Gid"/>) when the export is refused.</summary>
    GoogleSheet = 1,
    /// <summary>One tab as Google's gviz CSV.</summary>
    GoogleSheetCsv = 2,
    /// <summary>The page at <see cref="HistoricalSource.Url"/>.</summary>
    WebPage = 3,
    /// <summary>web.archive.org/web/{timestamp}id_/{url}.</summary>
    Wayback = 4,
    YouTubeChannel = 5,
}
