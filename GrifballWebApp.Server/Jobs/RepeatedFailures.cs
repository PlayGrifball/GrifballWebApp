using System.Collections.Concurrent;
using Quartz;
using Quartz.Extensibility;

namespace GrifballWebApp.Server.Jobs;

/// <summary>
/// Keeps a job that runs every few seconds from filling the run history with its failures. Quartz's per-job cap
/// (<see cref="JobsServiceCollectionExtensions.HistoryPerJob"/>) doesn't count failures, so a 10 s job failing on every
/// run (Discord down) would fill the scheduler's <see cref="JobsServiceCollectionExtensions.HistoryPerScheduler"/> rows
/// in hours and push every other job's runs out. A failure is recorded as one (it throws) at most every
/// <see cref="RecordEvery"/> per job; the ones in between are logged and recorded as skipped runs, with the error in
/// their summary, which the per-job cap does count. Successes in between don't reset it: a job failing every other run
/// (Discord flapping) would otherwise record half its runs as failures.
/// The last failure recorded is read from the shared run history too (the job's run status), so with several workers
/// it's every <see cref="RecordEvery"/> across all of them, not on each.
/// </summary>
public sealed class RepeatedFailures
{
    public static readonly TimeSpan RecordEvery = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<JobKey, DateTimeOffset> _lastRecorded = new();
    private readonly TimeProvider _time;
    private readonly ILogger<RepeatedFailures> _logger;
    private readonly IExecutionHistoryStore? _history;

    public RepeatedFailures(TimeProvider time, ILogger<RepeatedFailures> logger, IExecutionHistoryStore? history = null)
    {
        _time = time;
        _logger = logger;
        _history = history;
    }

    /// <summary>Runs <paramref name="run"/> as <paramref name="context"/>'s job: a failure throws, or (within <see cref="RecordEvery"/> of the last that did) is reported as skipped.</summary>
    public async ValueTask RunAsync(IJobExecutionContext context, Func<CancellationToken, Task> run, CancellationToken ct)
    {
        try
        {
            await run(ct);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            var key = context.JobDetail.Key;
            var now = _time.GetUtcNow();
            if (await RecordedSinceAsync(context, key, now - RecordEvery, ct))
            {
                _logger.LogError(ex, "Job {Job} failed again; its run history records a failure at most every {Every}", key, RecordEvery);
                context.Result = JobRunReport.Skipped($"Failed again (recorded as a failure at most every {RecordEvery.TotalMinutes:0} minutes): {ex.Message}");
                return;
            }
            _lastRecorded[key] = now;
            throw;
        }
    }

    /// <summary>Whether a failure of <paramref name="key"/> was recorded after <paramref name="since"/>: here, or by any pod in the shared history.</summary>
    private async ValueTask<bool> RecordedSinceAsync(IJobExecutionContext context, JobKey key, DateTimeOffset since, CancellationToken ct)
    {
        if (_lastRecorded.TryGetValue(key, out var last) && last > since)
            return true;
        if (_history is null)
            return false;
        try
        {
            var status = await _history.GetJobRunStatus(context.Scheduler.SchedulerName, key, ct);
            if (status?.LastFailedAtUtc is { } failed && failed > since)
            {
                _lastRecorded[key] = failed;
                return true;
            }
        }
        catch (Exception e) when (!(e is OperationCanceledException && ct.IsCancellationRequested))
        {
            // The history can't be read (the database is what's failing): this pod's own record decides.
            _logger.LogWarning(e, "Couldn't read job {Job}'s last recorded failure from the run history", key);
        }
        return false;
    }
}
