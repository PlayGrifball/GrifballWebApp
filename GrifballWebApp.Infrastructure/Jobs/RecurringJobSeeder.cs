using Quartz;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GrifballWebApp.Infrastructure.Jobs;

/// <summary>
/// Gives each recurring job without a trigger its default one, and removes the triggers of those turned off, as the
/// worker starts: registered before Quartz's hosted service, so this runs on the scheduler before it starts firing. A
/// trigger that is there is left alone (<see cref="TriggerConflict.Keep"/>, decided under the store's lock, so two pods
/// starting at once make it once).
/// </summary>
public sealed partial class RecurringJobSeeder : IHostedService
{
    private readonly ISchedulerFactory _schedulers;
    private readonly RecurringJobs _jobs;
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;
    private readonly ILogger<RecurringJobSeeder> _logger;

    public RecurringJobSeeder(ISchedulerFactory schedulers, RecurringJobs jobs, IServiceScopeFactory scopes, TimeProvider time, ILogger<RecurringJobSeeder> logger)
    {
        _schedulers = schedulers;
        _jobs = jobs;
        _scopes = scopes;
        _time = time;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var scheduler = await _schedulers.GetScheduler(cancellationToken);
        foreach (var (_, job) in _jobs.All)
            await SeedAsync(scheduler, job, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Makes <paramref name="job"/>'s trigger when it has none (or removes it, turned off). Returns whether it made one.</summary>
    public async Task<bool> SeedAsync(IScheduler scheduler, RecurringJob job, CancellationToken ct)
    {
        // Off, or run from the Jobs page only: no trigger (one made before it was either is removed)
        if (!job.Enabled || job.ManualOnly)
        {
            if (await scheduler.UnscheduleJob(job.TriggerKey, ct))
                LogTriggerRemoved(_logger, job.Key);
            return false;
        }

        var result = await scheduler.ScheduleTrigger(job.DefaultTrigger(_time.GetUtcNow()), TriggerConflict.Keep, ct);
        if (result.Outcome != ScheduleOutcome.Created)
            return false;

        LogScheduled(_logger, job.Key, result.NextFireTimeUtc);
        if (job.OnCreated is not null)
        {
            await using var scope = _scopes.CreateAsyncScope();
            await job.OnCreated(scheduler, scope.ServiceProvider, ct);
        }
        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Recurring job {Job} is off or manual only: its trigger was removed")]
    private static partial void LogTriggerRemoved(ILogger logger, JobKey job);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recurring job {Job} scheduled from its default, first at {Next}")]
    private static partial void LogScheduled(ILogger logger, JobKey job, DateTimeOffset? next);
}
