using GrifballWebApp.Application.Queries;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Test;

/// <summary>The historical import jobs' retention (HistoricalImportJobQueries.DeleteEndedAsync) against the fake context:
/// a job that ended before the cut-off goes, one that ended at it or later stays, and one still to run is never deleted.</summary>
[TestFixture]
internal sealed class HistoricalImportJobQueriesUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime At = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task DeleteEndedAsync_DeletesOnlyTheJobsThatEndedBeforeTheCutOff()
    {
        var jobs = Context.HistoricalImportJobs;
        var ended = HistoricalImportJob.Upload("{}");
        var queued = HistoricalImportJob.Refresh();
        jobs.AddRange(ended, queued);
        await Context.SaveChangesAsync();
        var claim = await HistoricalImportJob.ClaimAsync(jobs, ended.HistoricalImportJobID, At);
        Assert.That(claim, Is.Not.Null, "claimed");
        var finished = await HistoricalImportJob.FinishAsync(jobs, ended.HistoricalImportJobID, claim!.Value, true, "{}", null, At.AddMinutes(4));
        Assert.That(finished, Is.True);

        var atTheCutOff = await jobs.DeleteEndedAsync(At.AddMinutes(4));
        var afterIt = await jobs.DeleteEndedAsync(At.AddMinutes(5));

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((atTheCutOff, afterIt), Is.EqualTo((0, 1)), "ended at the cut-off: kept; before it: deleted");
            Assert.That(await jobs.Select(j => j.HistoricalImportJobID).ToListAsync(), Is.EqualTo(new[] { queued.HistoricalImportJobID }),
                "a job still queued is never deleted");
        }
    }
}
