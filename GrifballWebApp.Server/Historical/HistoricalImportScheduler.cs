using GrifballWebApp.Database.Models;
using Microsoft.Extensions.Options;

namespace GrifballWebApp.Server.Historical;

/// <summary>
/// Refreshes the historical sources on a schedule (HistoricalImport:Schedule:Interval, daily by default): when a run is
/// due it queues a scheduled refresh job, which queues a guarded import only when a source changed. Runs go through the
/// job queue, so one runs at a time and never alongside a manual one: a run that falls due while a job is queued or
/// running waits for it. Paused (a settings row a sysadmin sets), it queues nothing.
/// </summary>
public sealed class HistoricalImportScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<HistoricalImportScheduleOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<HistoricalImportScheduler> _logger;

    public HistoricalImportScheduler(IServiceScopeFactory scopes, IOptions<HistoricalImportScheduleOptions> options, TimeProvider time, ILogger<HistoricalImportScheduler> logger)
    {
        _scopes = scopes;
        _options = options;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Yield();
        try
        {
            await Task.Delay(_options.Value.StartupDelay, _time, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunDueAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The historical import schedule failed; trying again in {Delay}", _options.Value.CheckInterval);
            }
            try
            {
                await Task.Delay(_options.Value.CheckInterval, _time, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Queues the scheduled refresh when one is due, it isn't paused and no job is queued or running. Returns the
    /// job's ID, or null when it queued none.</summary>
    public async Task<int?> RunDueAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<HistoricalImportJobs>();
        var context = scope.ServiceProvider.GetRequiredService<GrifballWebApp.Database.GrifballContext>();
        var settings = await jobs.Settings(ct);
        var now = _time.GetUtcNow().UtcDateTime;
        if (settings.SchedulePaused || (settings.LastScheduledRunAt is DateTime last && now < last + _options.Value.Interval))
            return null;
        if (await jobs.AnyActive(ct))
        {
            if (settings.LastScheduledRunNote is null)
            {
                settings.LastScheduledRunNote = "Waiting for a queued or running job to finish";
                await context.SaveChangesAsync(ct);
            }
            return null;
        }
        var jobId = await jobs.Enqueue(new HistoricalImportJob { Kind = HistoricalImportJobKind.Refresh, Trigger = HistoricalImportJobTrigger.Scheduled }, ct);
        settings.LastScheduledRunAt = now;
        settings.LastScheduledJobID = jobId;
        settings.LastScheduledRunNote = null;
        await context.SaveChangesAsync(ct);
        _logger.LogInformation("Scheduled historical refresh queued as job {JobID}; the next is due at {Next}", jobId, now + _options.Value.Interval);
        return jobId;
    }
}
