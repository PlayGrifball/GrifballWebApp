using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Historical.Parsing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.HistoricalImport.Test;

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
/// With no copy, every test is ignored.
/// </summary>
public static class Archive
{
    public static ArchiveSnapshots Snapshots { get; internal set; } = null!;
    /// <summary>What filling the copy could not get (a source the database has no snapshot of, a failed download).</summary>
    public static List<string> FillProblems { get; } = [];
    public static HistoricalArchiveBuild Build { get; internal set; } = null!;
    public static HistoricalImportReport Report { get; internal set; } = null!;
    public static string DatabasePath { get; internal set; } = null!;

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
        await Fill(Environment.GetEnvironmentVariable("GRIF_HISTORICAL_FILL")?.Trim().ToLowerInvariant());
        if (Archive.Snapshots.Manifest.Count == 0)
            Assert.Ignore($"No copy of the archive in {Archive.Snapshots.Folder}: set GRIF_HISTORICAL_FILL (docs/historical-import.md)");
        foreach (var problem in Archive.FillProblems)
            await TestContext.Progress.WriteLineAsync($"Not filled: {problem}");

        Archive.Build = HistoricalArchiveBuilder.Build(Archive.Snapshots.Contents());

        Archive.DatabasePath = Path.Combine(Archive.Snapshots.Folder, "import.db");
        File.Delete(Archive.DatabasePath);
        await using var context = Archive.NewContext();
        await context.Database.MigrateAsync();
        var import = new HistoricalImportService(context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions()),
            NullLogger<HistoricalImportService>.Instance, new GamertagMatching(context), TimeProvider.System);
        Archive.Report = await import.Run(Archive.Build.Input, new HistoricalImportRunOptions());
        await TestContext.Progress.WriteLineAsync($"Imported {Archive.Report.SeasonsCreated} seasons, {Archive.Report.MatchmakingSeasonsCreated} Late League seasons, " +
            $"{Archive.Report.StatsInserted} stat lines; {Archive.Build.Warnings.Count} build warnings, {Archive.Report.Warnings.Count} import warnings, {Archive.Report.Errors.Count} errors. Database: {Archive.DatabasePath}");
    }

    private static async Task Fill(string? fill)
    {
        if (fill is null or "")
            return;
        if (fill is not ("database" or "download" or "both"))
            throw new InvalidOperationException($"GRIF_HISTORICAL_FILL is database, download or both, not {fill}");
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
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            Archive.FillProblems.AddRange(await Archive.Snapshots.FillFromDownload(http, TimeProvider.System));
        }
        // A source the database has none of that the download got is filled
        Archive.FillProblems.AddRange(databaseMissing.Where(id => !Archive.Snapshots.Manifest.ContainsKey(id)).Select(id => $"{id}: no snapshot in the database"));
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
