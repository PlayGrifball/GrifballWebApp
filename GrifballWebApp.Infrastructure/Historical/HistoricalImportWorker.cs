using GrifballWebApp.Domain.Archive;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GrifballWebApp.Infrastructure.Historical;

/// <summary>
/// Runs queued historical imports one at a time, outside any request: its cancellation is the host's shutdown. A job
/// is claimed by flipping it from Queued to Running in one statement, so two replicas never claim the same one. A job
/// stopped by a shutdown stays Running until its heartbeat is stale, then is queued again and runs from the start
/// (the import is idempotent). The run's moves carry its claim: a run whose job was queued again under it (a heartbeat
/// that went stale) has its beats, progress and finish refused, stops at its next beat, and its report is dropped.
/// </summary>
public sealed class HistoricalImportWorker : BackgroundService
{
    private readonly XuidLookupSignal _lookups;
    private readonly IServiceScopeFactory _scopes;
    private readonly HistoricalImportSignal _signal;
    private readonly IOptions<HistoricalImportWorkerOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<HistoricalImportWorker> _logger;

    public HistoricalImportWorker(IServiceScopeFactory scopes, HistoricalImportSignal signal, IOptions<HistoricalImportWorkerOptions> options, TimeProvider time, ILogger<HistoricalImportWorker> logger, XuidLookupSignal lookups)
    {
        _lookups = lookups;
        _scopes = scopes;
        _signal = signal;
        _options = options;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (await RunNextAsync(stoppingToken))
                {
                }
                await _signal.WaitAsync(_options.Value.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The historical import worker failed; trying again in {Delay}", _options.Value.PollInterval);
                try { await Task.Delay(_options.Value.PollInterval, _time, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <summary>Runs the oldest queued job, after queueing again any left Running by a stopped process. False when none was queued.</summary>
    public async Task<bool> RunNextAsync(CancellationToken ct)
    {
        int jobId;
        DateTime claim;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
            var now = _time.GetUtcNow().UtcDateTime;
            var stale = now - _options.Value.StaleAfter;
            // Retention: finished jobs older than KeepFinishedFor go
            var expired = now - _options.Value.KeepFinishedFor;
            await context.HistoricalImportJobs.DeleteEndedAsync(expired, ct);
            await HistoricalImportJob.RequeueStaleAsync(context.HistoricalImportJobs, stale, ct);

            var next = await context.HistoricalImportJobs.Where(j => j.Status == HistoricalImportJobStatus.Queued)
                .OrderBy(j => j.HistoricalImportJobID).Select(j => (int?)j.HistoricalImportJobID).FirstOrDefaultAsync(ct);
            if (next is null)
                return false;
            jobId = next.Value;
            if (await HistoricalImportJob.ClaimAsync(context.HistoricalImportJobs, jobId, now, ct) is not { } claimed)
            {
                // Another replica took it: on to the next. Still queued, it last started at or after now (the clock went
                // back): it waits for a later pass rather than being tried again at once
                return !await context.HistoricalImportJobs.AnyAsync(j => j.HistoricalImportJobID == jobId && j.Status == HistoricalImportJobStatus.Queued, ct);
            }
            claim = claimed;
        }

        await RunAsync(jobId, claim, ct);
        return true;
    }

    private async Task RunAsync(int jobId, DateTime claim, CancellationToken ct)
    {
        // The heartbeat, on a timer while the job runs: progress alone can be further apart than StaleAfter. A beat refused
        // (the job was queued again under this run, and maybe claimed by another) stops the run: it is no longer this one's
        using var lost = new CancellationTokenSource();
        using var running = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeat = HeartbeatAsync(jobId, claim, lost, running.Token);
        try
        {
            await RunJobAsync(jobId, claim, ct, lost.Token);
        }
        finally
        {
            await running.CancelAsync();
            await heartbeat;
        }
    }

    /// <summary>Beats the job's heartbeat every third of StaleAfter until <paramref name="ct"/> is cancelled (the job is
    /// done), or until a beat is refused: then <paramref name="lost"/> is cancelled, which stops the run.</summary>
    private async Task HeartbeatAsync(int jobId, DateTime claim, CancellationTokenSource lost, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromTicks(Math.Max(_options.Value.StaleAfter.Ticks / 3, TimeSpan.TicksPerMillisecond)), _time);
        // The job done: the timer stops, and the wait for its next tick ends (false)
        using var stop = ct.Register(timer.Dispose);
        while (await timer.WaitForNextTickAsync(CancellationToken.None))
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
                var now = _time.GetUtcNow().UtcDateTime;
                // Not cancelled with the job: one short update
                if (!await HistoricalImportJob.BeatAsync(context.HistoricalImportJobs, jobId, claim, now, CancellationToken.None))
                {
                    _logger.LogWarning("Historical import job {JobID} was queued again under this run; the run stops", jobId);
                    await lost.CancelAsync();
                    return;
                }
            }
            catch (Exception ex)
            {
                // Tried again at the next tick: two missed in a row still leave a third before StaleAfter
                _logger.LogWarning(ex, "Couldn't beat historical import job {JobID}'s heartbeat", jobId);
            }
        }
    }

    private async Task RunJobAsync(int jobId, DateTime claim, CancellationToken ct, CancellationToken lost)
    {
        using var run = CancellationTokenSource.CreateLinkedTokenSource(ct, lost);
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
            var job = await context.HistoricalImportJobs.AsNoTracking().Where(j => j.HistoricalImportJobID == jobId)
                .Select(j => new { j.Kind, j.Payload }).FirstAsync(run.Token);
            HistoricalImportReport report;
            if (job.Kind == HistoricalImportJobKind.Import && job.Payload is not null)
            {
                // An uploaded archive
                var input = JsonSerializer.Deserialize<HistoricalArchiveInput>(job.Payload, HistoricalArchiveInput.JsonOptions)
                    ?? throw new InvalidDataException("The job's archive is empty");
                var import = scope.ServiceProvider.GetRequiredService<IHistoricalImportService>();
                report = await import.Import(input, progress => Progress(jobId, claim, progress, run.Token), run.Token);
            }
            else
            {
                // The sources: a refresh, a preview, an import of their snapshots
                var runner = scope.ServiceProvider.GetRequiredService<HistoricalImportRunner>();
                report = await runner.Run(jobId, claim, progress => Progress(jobId, claim, progress, run.Token), run.Token);
            }
            Cap(report, _options.Value.ReportLinesKept);
            if (!await Finish(jobId, claim, succeeded: true, JsonSerializer.Serialize(report, HistoricalArchiveInput.JsonOptions), null, ct))
                return;
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Historical import job {JobID} finished", jobId);
            // The import's new placeholders get their XUIDs looked up
            _lookups.Notify();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Historical import job {JobID} was stopped by a shutdown; it runs again once its heartbeat is stale", jobId);
            throw;
        }
        catch (OperationCanceledException) when (lost.IsCancellationRequested)
        {
            // Logged by the heartbeat: the job is another run's (or queued for one)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Historical import job {JobID} failed", jobId);
            await Finish(jobId, claim, succeeded: false, null, ex.GetBaseException().Message, CancellationToken.None);
        }
    }

    /// <summary>Keeps each of the report's lists to <paramref name="lines"/> lines, the last saying how many more there were.</summary>
    public static void Cap(HistoricalImportReport report, int lines)
    {
        foreach (var list in new[] { report.Skipped, report.Warnings, report.MissingMatches, report.Errors })
        {
            if (list.Count <= lines)
                continue;
            var more = list.Count - lines;
            list.RemoveRange(lines, more);
            list.Add($"... and {more} more");
        }
    }

    private async Task Progress(int jobId, DateTime claim, HistoricalImportProgress progress, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
        var now = _time.GetUtcNow().UtcDateTime;
        await HistoricalImportJob.ReportProgressAsync(context.HistoricalImportJobs, jobId, claim, progress.Stage, progress.SeasonsDone, progress.SeasonsTotal, now, ct);
    }

    /// <summary>Ends the job with this run's result; false (logged) when it was queued again under this run, and the result
    /// is dropped.</summary>
    private async Task<bool> Finish(int jobId, DateTime claim, bool succeeded, string? report, string? error, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
        var now = _time.GetUtcNow().UtcDateTime;
        // The archive is no longer needed once the job ends
        if (await HistoricalImportJob.FinishAsync(context.HistoricalImportJobs, jobId, claim, succeeded, report, error, now, ct))
            return true;
        _logger.LogWarning("Historical import job {JobID} was queued again under this run; its report is dropped", jobId);
        return false;
    }
}
