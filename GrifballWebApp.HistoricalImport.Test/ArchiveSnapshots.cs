using System.Text.Json;
using GrifballWebApp.Application.Historical.Sources;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Historical.Sources;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.HistoricalImport.Test;

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
/// A local copy of the archive's sources: each catalog source's content, from a database's snapshots
/// (<see cref="FillFromDatabase"/>) or downloaded (<see cref="FillFromDownload"/>), kept in a folder outside git
/// (<see cref="Folder"/>). The tests read it with the catalog's current config (<see cref="Contents"/>), so a config
/// change is tested without fetching again.
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

    private string ManifestPath => Path.Combine(Folder, ManifestFile);

    /// <summary>The catalog's sources the import reads: alive or dead (a dead one imports from its last snapshot), with a config.</summary>
    public static IEnumerable<HistoricalSourceCatalog.Entry> ImportedEntries()
        => HistoricalSourceCatalog.Sources.Where(e => e.Config is not null);

    /// <summary>Each cached source of the catalog, as the import's build reads a snapshot: its current catalog row (the
    /// config in the code now) and the cached content.</summary>
    public List<HistoricalSourceContent> Contents()
    {
        var contents = new List<HistoricalSourceContent>();
        var id = 0;
        foreach (var entry in HistoricalSourceCatalog.Sources)
        {
            if (!Manifest.ContainsKey(entry.HistoricalSourceID))
                continue;
            var source = HistoricalSource.FromCatalog(entry.HistoricalSourceID, entry.ToListing(), entry.Enabled);
            contents.Add(new HistoricalSourceContent(source, ++id, File.ReadAllBytes(ContentPath(entry.HistoricalSourceID))));
        }
        return contents;
    }

    /// <summary>Copies every catalog source's latest snapshot from the database at <paramref name="connectionString"/>
    /// (read only). Returns the catalog's sources it had none of.</summary>
    public async Task<List<string>> FillFromDatabase(DatabaseProvider provider, string connectionString, CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(provider, connectionString).Options;
        await using var context = new GrifballContext(options);
        var missing = new List<string>();
        foreach (var entry in HistoricalSourceCatalog.Sources)
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

    /// <summary>Downloads every downloadable catalog source (alive and enabled) as a refresh does. A source that fails
    /// keeps its cached copy, if any. Returns each failure: the source and why.</summary>
    public async Task<List<string>> FillFromDownload(HttpClient http, TimeProvider time, CancellationToken ct = default)
    {
        var fetcher = new HistoricalSourceFetcher(http);
        var failed = new List<string>();
        foreach (var entry in HistoricalSourceCatalog.Sources.Where(e => e.Enabled && !e.IsDead))
        {
            var source = HistoricalSource.FromCatalog(entry.HistoricalSourceID, entry.ToListing(), entry.Enabled);
            try
            {
                var fetched = await fetcher.Fetch(source, ct);
                await Save(new CachedSnapshot(entry.HistoricalSourceID, SnapshotOrigin.Download, time.GetUtcNow().UtcDateTime,
                    HistoricalSourceSnapshot.Hash(fetched.Content), fetched.ContentType), fetched.Content, ct);
            }
            catch (Exception ex) when (ex is HistoricalFetchException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                failed.Add($"{entry.HistoricalSourceID}: {ex.Message}");
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
