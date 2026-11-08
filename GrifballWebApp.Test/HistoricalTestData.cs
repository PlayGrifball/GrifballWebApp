using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Test;

/// <summary>The historical archive's rows for tests, made and moved by their own methods.</summary>
internal static class HistoricalTestData
{
    /// <summary>An enabled source, as a catalog entry would make it.</summary>
    public static HistoricalSource Source(string id = "test-source", string url = "https://example.org/", string? config = null,
        HistoricalSourceKind kind = HistoricalSourceKind.GoogleSheet, string? sheetKey = null, string? waybackTimestamp = null,
        string category = "Test", string? title = null)
        => HistoricalSource.FromCatalog(id, new HistoricalSourceListing
        {
            Category = category,
            Title = title ?? id,
            Url = url,
            Kind = kind,
            SheetKey = sheetKey,
            WaybackTimestamp = waybackTimestamp,
            Config = config,
        }, enabled: true);

    /// <summary>Queues <paramref name="job"/> (saved).</summary>
    public static async Task<HistoricalImportJob> Queue(this GrifballContext context, HistoricalImportJob job)
    {
        context.HistoricalImportJobs.Add(job);
        await context.SaveChangesAsync();
        return job;
    }

    /// <summary>A worker claims the queued <paramref name="job"/> at <paramref name="at"/> (its start and heartbeat).</summary>
    public static async Task Start(this GrifballContext context, HistoricalImportJob job, DateTime? at = null)
        => Assert.That(await HistoricalImportJob.ClaimAsync(context.HistoricalImportJobs, job.HistoricalImportJobID, at ?? DateTime.UtcNow), Is.True, "claimed");

    /// <summary>The queued <paramref name="job"/> runs and ends at <paramref name="at"/>.</summary>
    public static async Task Finish(this GrifballContext context, HistoricalImportJob job, bool succeeded = true, DateTime? at = null)
    {
        await context.Start(job, at);
        Assert.That(await HistoricalImportJob.FinishAsync(context.HistoricalImportJobs, job.HistoricalImportJobID, succeeded, null, succeeded ? null : "boom", at ?? DateTime.UtcNow), Is.True, "finished");
    }
}
