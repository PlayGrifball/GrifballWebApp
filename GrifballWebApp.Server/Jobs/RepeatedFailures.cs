using System.Collections.Concurrent;
using Quartz;

namespace GrifballWebApp.Server.Jobs;

/// <summary>
/// Keeps a job that runs every few seconds from filling the run history with its failures. Quartz's per-job cap
/// (<see cref="JobsServiceCollectionExtensions.HistoryPerJob"/>) doesn't count failures, so a 10 s job failing on every
/// run (Discord down) would fill the scheduler's <see cref="JobsServiceCollectionExtensions.HistoryPerScheduler"/> rows
/// in hours and push every other job's runs out. A failure is recorded as one (it throws) at most every
/// <see cref="RecordEvery"/> per job; the ones in between are logged and recorded as skipped runs, with the error in
/// their summary, which the per-job cap does count.
/// </summary>
public sealed class RepeatedFailures
{
    public static readonly TimeSpan RecordEvery = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<JobKey, DateTimeOffset> _lastRecorded = new();
    private readonly TimeProvider _time;
    private readonly ILogger<RepeatedFailures> _logger;

    public RepeatedFailures(TimeProvider time, ILogger<RepeatedFailures> logger)
    {
        _time = time;
        _logger = logger;
    }

    /// <summary>Runs <paramref name="run"/> as <paramref name="context"/>'s job: a failure throws, or (within <see cref="RecordEvery"/> of the last that did) is reported as skipped.</summary>
    public async ValueTask RunAsync(IJobExecutionContext context, Func<CancellationToken, Task> run, CancellationToken ct)
    {
        try
        {
            await run(ct);
            // A success ends the run of failures: the next failure is recorded as one
            _lastRecorded.TryRemove(context.JobDetail.Key, out _);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            var key = context.JobDetail.Key;
            var now = _time.GetUtcNow();
            if (_lastRecorded.TryGetValue(key, out var last) && now - last < RecordEvery)
            {
                _logger.LogError(ex, "Job {Job} failed again; its run history records a failure at most every {Every}", key, RecordEvery);
                context.Result = JobRunReport.Skipped($"Failed again (recorded as a failure at most every {RecordEvery.TotalMinutes:0} minutes): {ex.Message}");
                return;
            }
            _lastRecorded[key] = now;
            throw;
        }
    }
}
