using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Archive;

namespace GrifballWebApp.Integration.Test;

/// <summary>The historical import's jobs for tests, queued and moved by their own methods.</summary>
internal static class HistoricalImportJobTestData
{
    /// <summary>Queues <paramref name="job"/> (saved).</summary>
    public static async Task<HistoricalImportJob> Queue(this GrifballContext context, HistoricalImportJob job)
    {
        context.HistoricalImportJobs.Add(job);
        await context.SaveChangesAsync();
        return job;
    }

    /// <summary>A worker claims the queued <paramref name="job"/> at <paramref name="at"/> (its start and heartbeat):
    /// returns the claim its run's moves carry.</summary>
    public static async Task<DateTime> Start(this GrifballContext context, HistoricalImportJob job, DateTime? at = null)
    {
        var claim = await HistoricalImportJob.ClaimAsync(context.HistoricalImportJobs, job.HistoricalImportJobID, at ?? DateTime.UtcNow);
        Assert.That(claim, Is.Not.Null, "claimed");
        return claim!.Value;
    }

    /// <summary>The queued <paramref name="job"/> runs and ends at <paramref name="at"/>.</summary>
    public static async Task Finish(this GrifballContext context, HistoricalImportJob job, bool succeeded = true, DateTime? at = null)
    {
        var claim = await context.Start(job, at);
        Assert.That(await HistoricalImportJob.FinishAsync(context.HistoricalImportJobs, job.HistoricalImportJobID, claim, succeeded, null, succeeded ? null : "boom", at ?? DateTime.UtcNow), Is.True, "finished");
    }
}
