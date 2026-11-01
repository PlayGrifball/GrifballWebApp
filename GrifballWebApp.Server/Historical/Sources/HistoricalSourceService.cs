using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

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

    /// <summary>Adds the catalog's new sources and brings the others' catalog fields up to date; Enabled and the fetch
    /// status are left alone. A source no longer in the catalog is kept (its snapshots are a backup).</summary>
    public async Task EnsureCatalog(CancellationToken ct = default)
    {
        var existing = await _context.HistoricalSources.ToDictionaryAsync(s => s.HistoricalSourceID, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var entry in HistoricalSourceCatalog.Sources)
        {
            if (!existing.TryGetValue(entry.HistoricalSourceID, out var source))
            {
                source = new HistoricalSource { HistoricalSourceID = entry.HistoricalSourceID, Enabled = entry.Enabled && !entry.IsDead };
                _context.HistoricalSources.Add(source);
            }
            source.Category = entry.Category;
            source.Year = entry.Year;
            source.Title = entry.Title;
            source.Url = entry.Url;
            source.Kind = entry.Kind;
            source.SheetKey = entry.SheetKey;
            source.Gid = entry.Gid;
            source.CsvSheetName = entry.CsvSheetName;
            source.WaybackTimestamp = entry.WaybackTimestamp;
            source.League = entry.League;
            source.GameVersion = entry.GameVersion;
            source.Format = entry.Format;
            source.IsDead = entry.IsDead;
            source.Config = entry.Config?.ToJson();
        }
        // Only what changed is written
        if (_context.ChangeTracker.HasChanges())
            await _context.SaveChangesAsync(ct);
    }

    public async Task<List<HistoricalSourceDto>> List(CancellationToken ct = default)
    {
        await EnsureCatalog(ct);
        var sources = await _context.HistoricalSources.AsNoTracking()
            .OrderBy(s => s.Category).ThenBy(s => s.HistoricalSourceID)
            .Select(s => new
            {
                s.HistoricalSourceID, s.Category, s.Year, s.Title, s.Url, s.Kind, s.League, s.GameVersion, s.Format, s.Enabled, s.IsDead,
                Imported = s.Config != null, s.LastFetchedAt, s.LastFetchStatus, s.LastChangedAt,
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
            Enabled = s.Enabled,
            IsDead = s.IsDead,
            Imported = s.Imported,
            LastFetchedAt = s.LastFetchedAt,
            LastFetchStatus = s.LastFetchStatus,
            LastChangedAt = s.LastChangedAt,
            SnapshotCount = s.SnapshotCount,
            LatestSnapshotSize = s.LatestSize,
        }).ToList();
    }

    /// <summary>False when there is no such source.</summary>
    public async Task<bool> SetEnabled(string sourceId, bool enabled, CancellationToken ct = default)
    {
        var source = await _context.HistoricalSources.FirstOrDefaultAsync(s => s.HistoricalSourceID == sourceId, ct);
        if (source is null)
            return false;
        source.Enabled = enabled;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Downloads every enabled source that isn't dead. A content whose SHA-256 differs from the source's latest snapshot
    /// is kept as a new snapshot; a failed download keeps the last one (the import goes on reading it). One source at a
    /// time, each saved as it is done: <paramref name="progress"/> hears each, the job's heartbeat.
    /// </summary>
    public async Task<HistoricalRefreshResult> Refresh(Func<string, Task>? progress = null, CancellationToken ct = default)
    {
        await EnsureCatalog(ct);
        var sources = await _context.HistoricalSources.Where(s => s.Enabled && !s.IsDead).OrderBy(s => s.HistoricalSourceID).ToListAsync(ct);
        var changed = new List<(string, int)>();
        var failed = new List<(string, string)>();
        var unchanged = 0;
        foreach (var source in sources)
        {
            ct.ThrowIfCancellationRequested();
            if (progress is not null)
                await progress($"Downloading {source.HistoricalSourceID}");
            var now = _time.GetUtcNow().UtcDateTime;
            source.LastFetchedAt = now;
            FetchedContent fetched;
            try
            {
                fetched = await _fetcher.Fetch(source, ct);
            }
            catch (Exception ex) when (ex is HistoricalFetchException or HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                var reason = ex is TaskCanceledException ? "timed out" : ex.Message;
                _logger.LogWarning("Historical source {SourceID} could not be downloaded ({Reason}); its last snapshot stays", source.HistoricalSourceID, reason);
                source.LastFetchStatus = Truncate($"Failed: {reason}", 300);
                failed.Add((source.HistoricalSourceID, reason));
                await _context.SaveChangesAsync(ct);
                continue;
            }

            var sha = Convert.ToHexStringLower(SHA256.HashData(fetched.Content));
            var latest = await _context.HistoricalSourceSnapshots.Where(s => s.HistoricalSourceID == source.HistoricalSourceID)
                .OrderByDescending(s => s.FetchedAt).ThenByDescending(s => s.HistoricalSourceSnapshotID).Select(s => s.Sha256).FirstOrDefaultAsync(ct);
            if (latest == sha)
            {
                source.LastFetchStatus = "Unchanged";
                unchanged++;
                await _context.SaveChangesAsync(ct);
                continue;
            }
            var snapshot = new HistoricalSourceSnapshot
            {
                HistoricalSourceID = source.HistoricalSourceID,
                FetchedAt = now,
                Sha256 = sha,
                Size = fetched.Content.LongLength,
                ContentType = Truncate(fetched.ContentType, 100),
                Content = fetched.Content,
            };
            _context.HistoricalSourceSnapshots.Add(snapshot);
            source.LastFetchStatus = "Changed";
            source.LastChangedAt = now;
            await _context.SaveChangesAsync(ct);
            // The bytes aren't kept in memory for the rest of the refresh
            _context.Entry(snapshot).State = EntityState.Detached;
            changed.Add((source.HistoricalSourceID, snapshot.HistoricalSourceSnapshotID));
        }
        return new HistoricalRefreshResult(changed, failed, unchanged);
    }

    /// <summary>Each source's latest snapshot (a disabled source's too: disabling stops its downloads, not its import).</summary>
    public async Task<List<int>> LatestSnapshotIds(CancellationToken ct = default)
        => await _context.HistoricalSourceSnapshots
            .Where(s => !_context.HistoricalSourceSnapshots.Any(o => o.HistoricalSourceID == s.HistoricalSourceID
                && (o.FetchedAt > s.FetchedAt || (o.FetchedAt == s.FetchedAt && o.HistoricalSourceSnapshotID > s.HistoricalSourceSnapshotID))))
            .Select(s => s.HistoricalSourceSnapshotID)
            .ToListAsync(ct);

    /// <summary>The snapshots' sources and contents, of the sources the import reads.</summary>
    public async Task<List<HistoricalSourceContent>> Load(IReadOnlyCollection<int> snapshotIds, CancellationToken ct = default)
    {
        var sources = await _context.HistoricalSources.AsNoTracking().Where(s => s.Config != null).ToDictionaryAsync(s => s.HistoricalSourceID, ct);
        var contents = new List<HistoricalSourceContent>();
        // One at a time: a few megabytes each
        foreach (var id in snapshotIds)
        {
            var snapshot = await _context.HistoricalSourceSnapshots.AsNoTracking().Where(s => s.HistoricalSourceSnapshotID == id)
                .Select(s => new { s.HistoricalSourceID, s.Content }).FirstOrDefaultAsync(ct);
            if (snapshot is not null && sources.TryGetValue(snapshot.HistoricalSourceID, out var source))
                contents.Add(new HistoricalSourceContent(source, id, snapshot.Content));
        }
        return contents;
    }

    private static string Truncate(string value, int length) => value.Length > length ? value[..length] : value;
}
