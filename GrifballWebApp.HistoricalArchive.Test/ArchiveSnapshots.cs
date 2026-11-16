using System.Text.Json;
using GrifballWebApp.Historical.Sources;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.HistoricalArchive.Test;

/// <summary>Where a cached copy of a source came from.</summary>
public enum SnapshotOrigin
{
    /// <summary>A database's latest snapshot of the source (the test environment's: it has the sheets that went private).</summary>
    Database,
    /// <summary>Downloaded now, as a refresh downloads it.</summary>
    Download,
}

/// <summary>One cached source: its file is <c>{SourceID}.bin</c> beside the manifest.</summary>
public sealed record CachedSnapshot(string SourceID, SnapshotOrigin Origin, DateTime FetchedAt, string Sha256, string? ContentType);

/// <summary>
/// A local copy of the archive's sources: each one's content, from a database's snapshots (<see cref="FillFromDatabase"/>)
/// or downloaded (<see cref="FillFromDownload"/>), kept in a folder outside git (<see cref="Folder"/>). The tests read it
/// with the catalog's current config (<see cref="Contents"/>), so a config change is tested without fetching again.
/// </summary>
public sealed class ArchiveSnapshots
{
    private const string ManifestFile = "manifest.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ArchiveSnapshots(string folder)
    {
        Folder = folder;
        Directory.CreateDirectory(folder);
        Manifest = File.Exists(ManifestPath)
            ? JsonSerializer.Deserialize<Dictionary<string, CachedSnapshot>>(File.ReadAllText(ManifestPath), JsonOptions)!
            : [];
    }

    public string Folder { get; }

    /// <summary>The cached sources, by source ID.</summary>
    public Dictionary<string, CachedSnapshot> Manifest { get; }

    /// <summary>Some of the copy is a database's snapshots: a copy without (downloaded only) can't have the sources only the
    /// database still has.</summary>
    public bool HasDatabaseSnapshots => Manifest.Values.Any(s => s.Origin == SnapshotOrigin.Database);

    private string ManifestPath => Path.Combine(Folder, ManifestFile);

    /// <summary>The catalog's sources the import reads: alive or dead (a dead one imports from its last snapshot), with a config.</summary>
    public static IEnumerable<HistoricalSourceCatalog.Entry> ImportedEntries()
        => HistoricalSourceCatalog.Sources.Where(e => e.Config is not null);

    /// <summary>The sources worth a copy: those the import reads, and every other sheet (one a config may read next). Not the
    /// web pages and channels the import never reads.</summary>
    public static IEnumerable<HistoricalSourceCatalog.Entry> CopiedEntries()
        => HistoricalSourceCatalog.Sources.Where(e => e.Config is not null || e.Kind is HistoricalSourceKind.GoogleSheet or HistoricalSourceKind.GoogleSheetCsv);

    /// <summary>Each cached source the import reads, as the import's build reads a snapshot: its current catalog row (the
    /// config in the code now) and the cached content. One at a time, as the worker reads them: a content is read from
    /// disk only when asked for.</summary>
    public IEnumerable<HistoricalSourceContent> Contents()
    {
        var id = 0;
        foreach (var entry in ImportedEntries().Where(e => Manifest.ContainsKey(e.HistoricalSourceID)))
        {
            var source = HistoricalSource.FromCatalog(entry.HistoricalSourceID, entry.ToListing(), entry.Enabled);
            yield return new HistoricalSourceContent(source, ++id, File.ReadAllBytes(ContentPath(entry.HistoricalSourceID)));
        }
    }

    /// <summary>Copies each source's latest snapshot from the database at <paramref name="connectionString"/>; it is only
    /// read. Returns the sources it had none of.</summary>
    public async Task<List<string>> FillFromDatabase(DatabaseProvider provider, string connectionString, CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(provider, connectionString).Options;
        await using var context = new GrifballContext(options);
        var missing = new List<string>();
        foreach (var entry in CopiedEntries())
        {
            var snapshot = await context.HistoricalSources.AsNoTracking()
                .Where(s => s.HistoricalSourceID == entry.HistoricalSourceID)
                .SelectMany(s => s.Snapshots).Where(HistoricalSourceSnapshot.Latest)
                .Select(s => new { s.FetchedAt, s.Sha256, s.ContentType, s.Content })
                .FirstOrDefaultAsync(ct);
            if (snapshot is null)
            {
                missing.Add(entry.HistoricalSourceID);
                continue;
            }
            await Save(new CachedSnapshot(entry.HistoricalSourceID, SnapshotOrigin.Database, snapshot.FetchedAt, snapshot.Sha256, snapshot.ContentType), snapshot.Content, ct);
        }
        await SaveManifest(ct);
        return missing;
    }

    /// <summary>Downloads each downloadable source (alive and enabled) with the refresh's client
    /// (<see cref="HistoricalSourceFetcher.Configure"/>). A source that fails keeps its cached copy, if any. Returns each
    /// failure: the source, why, and whether the copy still has it.</summary>
    public async Task<List<(string SourceID, string Reason, bool Kept)>> FillFromDownload(TimeProvider time, CancellationToken ct = default)
    {
        using var http = new HttpClient();
        HistoricalSourceFetcher.Configure(http);
        var fetcher = new HistoricalSourceFetcher(http);
        var failed = new List<(string, string, bool)>();
        foreach (var entry in CopiedEntries().Where(e => e.Enabled && !e.IsDead))
        {
            var source = HistoricalSource.FromCatalog(entry.HistoricalSourceID, entry.ToListing(), entry.Enabled);
            try
            {
                var fetched = await fetcher.Fetch(source, ct);
                await Save(new CachedSnapshot(entry.HistoricalSourceID, SnapshotOrigin.Download, time.GetUtcNow().UtcDateTime,
                    HistoricalSourceSnapshot.Hash(fetched.Content), fetched.ContentType), fetched.Content, ct);
            }
            // What HistoricalSourceService's refresh counts as a failed download
            catch (Exception ex) when (ex is HistoricalFetchException or HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                failed.Add((entry.HistoricalSourceID, ex is TaskCanceledException ? "timed out" : ex.Message, Manifest.ContainsKey(entry.HistoricalSourceID)));
            }
        }
        await SaveManifest(ct);
        return failed;
    }

    private string ContentPath(string sourceId) => Path.Combine(Folder, $"{sourceId}.bin");

    private async Task Save(CachedSnapshot snapshot, byte[] content, CancellationToken ct)
    {
        await File.WriteAllBytesAsync(ContentPath(snapshot.SourceID), content, ct);
        Manifest[snapshot.SourceID] = snapshot;
    }

    private async Task SaveManifest(CancellationToken ct)
        => await File.WriteAllTextAsync(ManifestPath, JsonSerializer.Serialize(Manifest, JsonOptions), ct);
}
