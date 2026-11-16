using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Historical.Parsing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using GrifballWebApp.Historical.Import;

namespace GrifballWebApp.HistoricalArchive.Test;

/// <summary>
/// The real archive, once per run: the local copy of the sources is filled when asked (<c>GRIF_HISTORICAL_FILL</c>),
/// built into the import's input with the catalog's current config, and imported whole into a fresh SQLite database
/// (<c>import.db</c> in the copy's folder, kept after the run to look into). Every test reads these.
/// <list type="bullet">
/// <item><c>GRIF_HISTORICAL_ARCHIVE</c>: the copy's folder; <c>.historical-archive</c> in the repository (git ignores it) by default.</item>
/// <item><c>GRIF_HISTORICAL_FILL</c>: <c>database</c> (the latest snapshots in <c>GRIF_HISTORICAL_DATABASE</c>, a
/// connection string; <c>GRIF_HISTORICAL_DATABASE_PROVIDER</c>, SqlServer by default), <c>download</c> (from Google
/// and the other sources' URLs), or <c>both</c> (the database's, then a download over each that is still public).
/// Unset: the copy as it is.</item>
/// </list>
/// With no copy and no fill, every test is ignored.
/// <para>
/// No Halo API: the Waypoint matches of Halo Infinite seasons are not pulled (<see cref="WaypointNotPulled"/>), so those
/// seasons have none of their Waypoint games.
/// </para>
/// </summary>
public static class Archive
{
    public static ArchiveSnapshots Snapshots { get; internal set; } = null!;
    /// <summary>What filling the copy could not get (a source the database has no snapshot of, a failed download).</summary>
    public static List<string> FillProblems { get; } = [];
    public static HistoricalArchiveBuild Build { get; internal set; } = null!;
    public static HistoricalImportReport Report { get; internal set; } = null!;
    public static string DatabasePath { get; internal set; } = null!;

    /// <summary>The import's skips of a Waypoint match it could not pull: every one, with no Halo API here.</summary>
    public static bool WaypointNotPulled(string skipped)
        => skipped.StartsWith("Waypoint match ", StringComparison.Ordinal);

    /// <summary>The seasons (their source IDs) whose games are Waypoint matches, which this run doesn't have.</summary>
    public static IReadOnlySet<string> WaypointSeasons
        => (Build.Input.WaypointMatches?.Keys ?? Enumerable.Empty<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>A context on the imported database. Read only: the tests share it.</summary>
    public static GrifballContext NewContext()
        => new(new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={DatabasePath};Pooling=False").Options);
}

/// <summary>Fills <see cref="Archive"/> before any test.</summary>
[SetUpFixture]
public class ArchiveSetUp
{
    [OneTimeSetUp]
    public async Task SetUp()
    {
        Archive.Snapshots = new ArchiveSnapshots(Environment.GetEnvironmentVariable("GRIF_HISTORICAL_ARCHIVE") is { Length: > 0 } folder ? folder : DefaultFolder());
        var fill = Environment.GetEnvironmentVariable("GRIF_HISTORICAL_FILL")?.Trim().ToLowerInvariant();
        await Fill(fill);
        foreach (var problem in Archive.FillProblems)
            await TestContext.Progress.WriteLineAsync($"Not filled: {problem}");
        if (Archive.Snapshots.Manifest.Count == 0)
        {
            // Asked to fill and got nothing: a failure, not a run without a copy
            if (fill is { Length: > 0 })
                throw new InvalidOperationException($"GRIF_HISTORICAL_FILL={fill} copied no source: {string.Join("; ", Archive.FillProblems)}");
            Assert.Ignore($"No copy of the archive in {Archive.Snapshots.Folder}: set GRIF_HISTORICAL_FILL (docs/historical-import.md)");
        }

        // One source at a time, as the worker builds
        var builder = new HistoricalArchiveBuilder();
        foreach (var content in Archive.Snapshots.Contents())
            builder.Add(content);
        Archive.Build = builder.Finish();

        Archive.DatabasePath = Path.Combine(Archive.Snapshots.Folder, "import.db");
        // The journal files too: a run stopped mid-import leaves them, and SQLite would replay them into the new file
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            File.Delete(Archive.DatabasePath + suffix);
        await using var context = Archive.NewContext();
        await context.Database.MigrateAsync();
        var import = new HistoricalImportService(context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions { MatchPullAttempts = 1 }),
            NullLogger<HistoricalImportService>.Instance, new GamertagMatching(context), TimeProvider.System);
        Archive.Report = await import.Run(Archive.Build.Input, new HistoricalImportRunOptions());
        await TestContext.Progress.WriteLineAsync($"Imported {Archive.Report.SeasonsCreated} seasons, {Archive.Report.MatchmakingSeasonsCreated} Late League seasons, " +
            $"{Archive.Report.StatsInserted} stat lines; {Archive.Build.Warnings.Count} build warnings, {Archive.Report.Warnings.Count} import warnings, " +
            $"{Archive.Report.Skipped.Count(s => !Archive.WaypointNotPulled(s))} skipped, {Archive.Report.Errors.Count} errors. Database: {Archive.DatabasePath}");
    }

    private static async Task Fill(string? fill)
    {
        if (fill is null or "")
            return;
        if (fill is not ("database" or "download" or "both"))
            throw new InvalidOperationException($"GRIF_HISTORICAL_FILL is database, download or both, not {fill}");
        // The problems of the sources the import reads: another sheet (one a config may read next) missing is no gap
        var imported = ArchiveSnapshots.ImportedEntries().Select(e => e.HistoricalSourceID).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var databaseMissing = new List<string>();
        if (fill is "database" or "both")
        {
            var connectionString = Environment.GetEnvironmentVariable("GRIF_HISTORICAL_DATABASE")
                ?? throw new InvalidOperationException("GRIF_HISTORICAL_FILL=database needs GRIF_HISTORICAL_DATABASE (a connection string)");
            var provider = Environment.GetEnvironmentVariable("GRIF_HISTORICAL_DATABASE_PROVIDER") is { Length: > 0 } name
                ? Enum.Parse<DatabaseProvider>(name, ignoreCase: true)
                : DatabaseProvider.SqlServer;
            databaseMissing = await Archive.Snapshots.FillFromDatabase(provider, connectionString);
        }
        if (fill is "download" or "both")
        {
            var failed = await Archive.Snapshots.FillFromDownload(TimeProvider.System);
            // A failed download of a source the copy has (the database's snapshot, or an earlier one) is only stale
            Archive.FillProblems.AddRange(failed.Where(f => imported.Contains(f.SourceID)).Select(f => f.Kept ? $"{f.SourceID}: download failed ({f.Reason}); the copy keeps its earlier one" : $"{f.SourceID}: {f.Reason}"));
        }
        // A source the database has none of that the download got is filled
        Archive.FillProblems.AddRange(databaseMissing.Where(id => imported.Contains(id) && !Archive.Snapshots.Manifest.ContainsKey(id)).Select(id => $"{id}: no snapshot in the database"));
    }

    /// <summary><c>.historical-archive</c> in the repository: the folder above the build output that has the solution.</summary>
    private static string DefaultFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "GrifballWebApp.slnx")))
                return Path.Combine(dir.FullName, ".historical-archive");
        throw new InvalidOperationException("No repository above the test's build: set GRIF_HISTORICAL_ARCHIVE");
    }
}
