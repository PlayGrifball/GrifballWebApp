using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Historical.Sources;

public class HistoricalSourceDto
{
    public required string SourceID { get; set; }
    public required string Category { get; set; }
    public int? Year { get; set; }
    public required string Title { get; set; }
    public required string Url { get; set; }
    public required string Kind { get; set; }
    public string? League { get; set; }
    public string? GameVersion { get; set; }
    public string? Format { get; set; }
    public bool Enabled { get; set; }
    public bool IsDead { get; set; }
    /// <summary>The import reads it (its catalog entry says what); the others are kept as a backup.</summary>
    public bool Imported { get; set; }
    public DateTime? LastFetchedAt { get; set; }
    public string? LastFetchStatus { get; set; }
    public DateTime? LastChangedAt { get; set; }
    public int SnapshotCount { get; set; }
    public long? LatestSnapshotSize { get; set; }
}

/// <summary>What a refresh did: the sources whose content changed (with their new snapshot), those that failed, and how
/// many were unchanged.</summary>
public record HistoricalRefreshResult(List<(string SourceID, int SnapshotID)> Changed, List<(string SourceID, string Reason)> Failed, int Unchanged);

/// <summary>A source with its snapshot's content, for the parsers.</summary>
public record HistoricalSourceContent(HistoricalSource Source, int SnapshotID, byte[] Content);

/// <summary>
/// The archive's sources and their snapshots: the catalog kept in the database, refreshes (download, keep the content
/// when its hash changed), and the snapshots an import reads.
/// </summary>
public class HistoricalSourceService
{
    private readonly GrifballContext _context;
    private readonly HistoricalSourceFetcher _fetcher;
    private readonly TimeProvider _time;
    private readonly ILogger<HistoricalSourceService> _logger;

    public HistoricalSourceService(GrifballContext context, HistoricalSourceFetcher fetcher, TimeProvider time, ILogger<HistoricalSourceService> logger)
    {
        _context = context;
        _fetcher = fetcher;
        _time = time;
        _logger = logger;
    }

    /// <summary>Adds the catalog's new sources and brings the others' catalog fields up to date; Enabled and the fetch status
    /// are left alone (a dead source keeps Enabled, masked by IsDead; one revived before it was ever downloaded takes the catalog's again). A source no longer in the catalog is kept (its snapshots are a backup).</summary>
    public async Task EnsureCatalog(CancellationToken ct = default)
    {
        var existing = await _context.HistoricalSources.ToDictionaryAsync(s => s.HistoricalSourceID, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var entry in HistoricalSourceCatalog.Sources)
        {
            if (existing.TryGetValue(entry.HistoricalSourceID, out var source))
                source.UpdateCatalog(entry.ToListing(), entry.Enabled);
            else
                _context.HistoricalSources.Add(HistoricalSource.FromCatalog(entry.HistoricalSourceID, entry.ToListing(), entry.Enabled));
        }
        // Only what changed is written
        if (_context.ChangeTracker.HasChanges())
            await _context.SaveChangesAsync(ct);
    }

    public async Task<List<HistoricalSourceDto>> List(CancellationToken ct = default)
    {
        await EnsureCatalog(ct);
        // The sources the import reads, by the model's rule (HistoricalSource.Imported): a few hundred IDs at most
        var imported = await _context.HistoricalSources.AsNoTracking().Where(HistoricalSource.Imported).Select(s => s.HistoricalSourceID).ToHashSetAsync(ct);
        var sources = await _context.HistoricalSources.AsNoTracking()
            .OrderBy(s => s.Category).ThenBy(s => s.HistoricalSourceID)
            .Select(s => new
            {
                s.HistoricalSourceID, s.Category, s.Year, s.Title, s.Url, s.Kind, s.League, s.GameVersion, s.Format, s.Enabled, s.IsDead,
                s.LastFetchedAt, s.LastFetchStatus, s.LastChangedAt,
                SnapshotCount = s.Snapshots.Count(),
                LatestSize = s.Snapshots.OrderByDescending(n => n.FetchedAt).Select(n => (long?)n.Size).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return sources.Select(s => new HistoricalSourceDto
        {
            SourceID = s.HistoricalSourceID,
            Category = s.Category,
            Year = s.Year,
            Title = s.Title,
            Url = s.Url,
            Kind = s.Kind.ToString(),
            League = s.League,
            GameVersion = s.GameVersion,
            Format = s.Format,
            // A dead source is never downloaded, so it is reported off whatever its stored (sysadmin's) Enabled says
            Enabled = s.Enabled && !s.IsDead,
            IsDead = s.IsDead,
            Imported = imported.Contains(s.HistoricalSourceID),
            LastFetchedAt = s.LastFetchedAt,
            LastFetchStatus = s.LastFetchStatus,
            LastChangedAt = s.LastChangedAt,
            SnapshotCount = s.SnapshotCount,
            LatestSnapshotSize = s.LatestSize,
        }).ToList();
    }

    /// <summary>Null when there is no such source; <see cref="HistoricalSourceSetEnabledResult.Dead"/> (nothing changed)
    /// when enabling a dead one.</summary>
    public async Task<HistoricalSourceSetEnabledResult?> SetEnabled(string sourceId, bool enabled, CancellationToken ct = default)
    {
        var source = await _context.HistoricalSources.FirstOrDefaultAsync(s => s.HistoricalSourceID == sourceId, ct);
        if (source is null)
            return null;
        var result = source.SetEnabled(enabled);
        if (result == HistoricalSourceSetEnabledResult.Set)
            await _context.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>
    /// Downloads every enabled source that isn't dead. A content whose SHA-256 differs from the source's latest snapshot
    /// is kept as a new snapshot; a failed download keeps the last one (the import goes on reading it). One source at a
    /// time, each saved as it is done: <paramref name="progress"/> hears each, the job's heartbeat.
    /// </summary>
    public async Task<HistoricalRefreshResult> Refresh(Func<string, Task>? progress = null, CancellationToken ct = default)
    {
        await EnsureCatalog(ct);
        var ids = await _context.HistoricalSources.Where(HistoricalSource.Downloadable).OrderBy(s => s.HistoricalSourceID).Select(s => s.HistoricalSourceID).ToListAsync(ct);
        var changed = new List<(string, int)>();
        var failed = new List<(string, string)>();
        var unchanged = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            // One source at a time, let go of once saved: a source holds the snapshot it keeps, and the bytes aren't kept in
            // memory for the rest of the refresh
            var source = await _context.HistoricalSources.FirstOrDefaultAsync(s => s.HistoricalSourceID == id, ct);
            if (source is null || !source.IsDownloadable)
                continue;
            if (progress is not null)
                await progress($"Downloading {source.HistoricalSourceID}");
            var now = _time.GetUtcNow().UtcDateTime;
            FetchedContent fetched;
            try
            {
                fetched = await _fetcher.Fetch(source, ct);
            }
            catch (Exception ex) when (ex is HistoricalFetchException or HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                var reason = ex is TaskCanceledException ? "timed out" : ex.Message;
                _logger.LogWarning("Historical source {SourceID} could not be downloaded ({Reason}); its last snapshot stays", source.HistoricalSourceID, reason);
                source.FetchFailed(now, reason);
                failed.Add((source.HistoricalSourceID, reason));
                await SaveAndLetGo(source, null, ct);
                continue;
            }

            var latest = await _context.HistoricalSources.Where(s => s.HistoricalSourceID == source.HistoricalSourceID).SelectMany(s => s.Snapshots)
                .OrderByDescending(s => s.FetchedAt).ThenByDescending(s => s.HistoricalSourceSnapshotID).Select(s => s.Sha256).FirstOrDefaultAsync(ct);
            var snapshot = source.Fetched(now, fetched.Content, fetched.ContentType, latest);
            if (snapshot is null)
                unchanged++;
            await SaveAndLetGo(source, snapshot, ct);
            if (snapshot is not null)
                changed.Add((source.HistoricalSourceID, snapshot.HistoricalSourceSnapshotID));
        }
        return new HistoricalRefreshResult(changed, failed, unchanged);
    }

    private async Task SaveAndLetGo(HistoricalSource source, HistoricalSourceSnapshot? snapshot, CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
        if (snapshot is not null)
            _context.Entry(snapshot).State = EntityState.Detached;
        _context.Entry(source).State = EntityState.Detached;
    }

    /// <summary>Each source's latest snapshot (a disabled source's too: disabling stops its downloads, not its import).</summary>
    public async Task<List<int>> LatestSnapshotIds(CancellationToken ct = default)
        => await _context.HistoricalSources.SelectMany(s => s.Snapshots)
            .Where(HistoricalSourceSnapshot.Latest)
            .Select(s => s.HistoricalSourceSnapshotID)
            .ToListAsync(ct);

    /// <summary>The snapshots' sources and contents, of the sources the import reads.</summary>
    public async Task<List<HistoricalSourceContent>> Load(IReadOnlyCollection<int> snapshotIds, CancellationToken ct = default)
    {
        var contents = new List<HistoricalSourceContent>();
        await foreach (var content in Read(snapshotIds, ct))
            contents.Add(content);
        return contents;
    }

    /// <summary>The snapshots' sources and contents, of the sources the import reads, one at a time: each is fetched when
    /// asked for, so a caller that lets go of one before taking the next (the import's build) holds one snapshot, not all.</summary>
    public async IAsyncEnumerable<HistoricalSourceContent> Read(IReadOnlyCollection<int> snapshotIds, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var sources = await _context.HistoricalSources.AsNoTracking().Where(HistoricalSource.Imported).ToDictionaryAsync(s => s.HistoricalSourceID, ct);
        foreach (var id in snapshotIds)
        {
            var snapshot = await _context.HistoricalSources.AsNoTracking().SelectMany(s => s.Snapshots).Where(s => s.HistoricalSourceSnapshotID == id)
                .Select(s => new { s.HistoricalSourceID, s.Content }).FirstOrDefaultAsync(ct);
            if (snapshot is not null && sources.TryGetValue(snapshot.HistoricalSourceID, out var source))
                yield return new HistoricalSourceContent(source, id, snapshot.Content);
        }
    }
}
