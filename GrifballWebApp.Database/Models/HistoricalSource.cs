using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A source of the historical archive: a Google Sheets workbook, a page or a channel the worker downloads itself
/// (HistoricalSourceService). The rows come from the app's own catalog (HistoricalSourceCatalog), inserted and kept up to
/// date at runtime, so a fresh environment needs no setup; only <see cref="Enabled"/> is a sysadmin's.
/// Not temporal: the worker writes its fetch status on every refresh; who changed Enabled is in the audit columns.
/// Its snapshots (<see cref="HistoricalSourceSnapshot"/>) are its own: kept by <see cref="Fetched"/>, reached through it.
/// </summary>
[AggregateRoot]
public class HistoricalSource : AuditableEntity
{
    /// <summary>The longest fetch status kept; a longer one is cut.</summary>
    public const int MaxFetchStatusLength = 300;
    public const string ChangedStatus = "Changed";
    public const string UnchangedStatus = "Unchanged";

    /// <summary>For EF. A source begins in the catalog: <see cref="FromCatalog"/>.</summary>
    private HistoricalSource() { }

    /// <summary>A catalog entry new to the database, enabled as the catalog says (a dead one never is).</summary>
    public static HistoricalSource FromCatalog(string historicalSourceID, HistoricalSourceListing listing, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(historicalSourceID);
        var source = new HistoricalSource { HistoricalSourceID = historicalSourceID, Enabled = enabled && !listing.IsDead };
        source.UpdateCatalog(listing);
        return source;
    }

    /// <summary>The catalog's ID, e.g. 2023-da-wl23-stats: the "sourceWorkbook" half of a season's HistoricalSourceID.</summary>
    public string HistoricalSourceID { get; internal set; } = null!;
    /// <summary>e.g. AGLA-2023, Late-League, VODs.</summary>
    public string Category { get; private set; } = null!;
    public int? Year { get; private set; }
    public string Title { get; private set; } = null!;
    /// <summary>The link people open (a Google Sheets edit link, the page): stored on the seasons it makes.</summary>
    public string Url { get; private set; } = null!;
    public HistoricalSourceKind Kind { get; private set; }
    /// <summary>A Google Sheets workbook's ID.</summary>
    public string? SheetKey { get; private set; }
    /// <summary>The tab a CSV export reads (Google's gviz CSV, one tab per download).</summary>
    public string? Gid { get; private set; }
    /// <summary>The name the CSV's tab goes by when parsed (what the parse config names it).</summary>
    public string? CsvSheetName { get; private set; }
    /// <summary>A Wayback capture's timestamp (yyyyMMddHHmmss): the capture is permanent, the live page may not be.</summary>
    public string? WaybackTimestamp { get; private set; }
    /// <summary>The league, game (GameVersion slug, or "multi") and format of the seasons it holds, for the page.</summary>
    public string? League { get; private set; }
    public string? GameVersion { get; private set; }
    public string? Format { get; private set; }
    /// <summary>What the import reads from it (HistoricalSourceConfig as JSON): its seasons' tabs, Waypoint game lists,
    /// recorded games, titles, players list. Null for a source the import doesn't read (kept as a backup).</summary>
    public string? Config { get; private set; }
    /// <summary>Downloaded on each refresh. A disabled source keeps its snapshots, and the import still reads the last one.</summary>
    public bool Enabled { get; private set; } = true;
    /// <summary>Gone for good (deleted, private, no capture): never downloaded, listed for the record.</summary>
    public bool IsDead { get; private set; }
    public DateTime? LastFetchedAt { get; private set; }
    /// <summary>"Changed", "Unchanged" or why the download failed.</summary>
    public string? LastFetchStatus { get; private set; }
    /// <summary>When its content last changed (its latest snapshot).</summary>
    public DateTime? LastChangedAt { get; private set; }

    /// <summary>Its downloaded contents, oldest first: added only by <see cref="Fetched"/>, removed with it.</summary>
    public IReadOnlyCollection<HistoricalSourceSnapshot> Snapshots => _snapshots;
    private readonly List<HistoricalSourceSnapshot> _snapshots = [];

    /// <summary>Downloaded on a refresh: enabled and not dead. For queries.</summary>
    public static readonly Expression<Func<HistoricalSource, bool>> Downloadable = s => s.Enabled && !s.IsDead;

    private static readonly Func<HistoricalSource, bool> _isDownloadable = Downloadable.Compile();

    /// <summary><see cref="Downloadable"/>, for a loaded source.</summary>
    public bool IsDownloadable => _isDownloadable(this);

    /// <summary>Read by the import: its catalog entry says what (<see cref="Config"/>); the others are kept as a backup. For queries.</summary>
    public static readonly Expression<Func<HistoricalSource, bool>> Imported = s => s.Config != null;

    /// <summary><see cref="Imported"/>, for a loaded source.</summary>
    public bool IsImported => Config is not null;

    /// <summary>Brings the catalog's fields up to date; <see cref="Enabled"/> and the fetch status are left alone.</summary>
    public void UpdateCatalog(HistoricalSourceListing listing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listing.Category);
        ArgumentException.ThrowIfNullOrWhiteSpace(listing.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(listing.Url);
        Category = listing.Category;
        Year = listing.Year;
        Title = listing.Title;
        Url = listing.Url;
        Kind = listing.Kind;
        SheetKey = listing.SheetKey;
        Gid = listing.Gid;
        CsvSheetName = listing.CsvSheetName;
        WaybackTimestamp = listing.WaybackTimestamp;
        League = listing.League;
        GameVersion = listing.GameVersion;
        Format = listing.Format;
        IsDead = listing.IsDead;
        Config = listing.Config;
    }

    /// <summary>A sysadmin's: a disabled source is no longer downloaded; the import keeps reading its last snapshot.</summary>
    public void SetEnabled(bool enabled) => Enabled = enabled;

    /// <summary>A download started at <paramref name="at"/> failed for <paramref name="reason"/>: its last snapshot stays.</summary>
    public void FetchFailed(DateTime at, string reason)
    {
        EnsureDownloadable();
        LastFetchedAt = at;
        LastFetchStatus = Truncate($"Failed: {reason}", MaxFetchStatusLength);
    }

    /// <summary>
    /// A download started at <paramref name="at"/> gave <paramref name="content"/>. Kept as a new snapshot (returned) when
    /// its SHA-256 differs from the latest one's, <paramref name="latestSha256"/>; otherwise only noted as unchanged (null).
    /// </summary>
    public HistoricalSourceSnapshot? Fetched(DateTime at, byte[] content, string contentType, string? latestSha256)
    {
        EnsureDownloadable();
        LastFetchedAt = at;
        var sha = HistoricalSourceSnapshot.Hash(content);
        if (sha == latestSha256)
        {
            LastFetchStatus = UnchangedStatus;
            return null;
        }
        var snapshot = new HistoricalSourceSnapshot(this, at, sha, content, contentType);
        _snapshots.Add(snapshot);
        LastFetchStatus = ChangedStatus;
        LastChangedAt = at;
        return snapshot;
    }

    private void EnsureDownloadable()
    {
        if (!IsDownloadable)
            throw new InvalidOperationException($"Historical source {HistoricalSourceID} is {(IsDead ? "dead" : "disabled")}: it isn't downloaded");
    }

    internal static string Truncate(string value, int length) => value.Length > length ? value[..length] : value;
}

/// <summary>A source's catalog fields: what the catalog says of it, without its runtime state.</summary>
public sealed record HistoricalSourceListing
{
    public required string Category { get; init; }
    public int? Year { get; init; }
    public required string Title { get; init; }
    public required string Url { get; init; }
    public HistoricalSourceKind Kind { get; init; }
    public string? SheetKey { get; init; }
    public string? Gid { get; init; }
    public string? CsvSheetName { get; init; }
    public string? WaybackTimestamp { get; init; }
    public string? League { get; init; }
    public string? GameVersion { get; init; }
    public string? Format { get; init; }
    public bool IsDead { get; init; }
    /// <summary>HistoricalSourceConfig as JSON; null for a source the import doesn't read.</summary>
    public string? Config { get; init; }
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
