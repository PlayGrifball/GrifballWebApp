using GrifballWebApp.Domain.Archive;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Database.Queries;

/// <summary>
/// The retention of <see cref="HistoricalImportJob"/>s: a plain delete, no state transition, so it isn't the model's.
/// The transitions (claim, beat, progress, finish, requeue) stay on the model as single conditional UPDATEs.
/// </summary>
public static class HistoricalImportJobQueries
{
    /// <summary>Deletes the jobs that ended before <paramref name="endedBefore"/> (their retention is over), with their reviews.</summary>
    public static Task<int> DeleteEndedAsync(this IQueryable<HistoricalImportJob> jobs, DateTime endedBefore, CancellationToken ct = default)
        => jobs.Where(HistoricalImportJob.Ended).Where(j => j.FinishedAt < endedBefore).ExecuteDeleteAsync(ct);
}
