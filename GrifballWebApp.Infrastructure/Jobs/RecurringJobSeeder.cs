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
public sealed class RecurringJobSeeder : IHostedService
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
        if (!job.Enabled)
        {
            if (await scheduler.UnscheduleJob(job.TriggerKey, ct))
                _logger.LogInformation("Recurring job {Job} is off: its trigger was removed", job.Key);
            return false;
        }

        var result = await scheduler.ScheduleTrigger(job.DefaultTrigger(_time.GetUtcNow()), TriggerConflict.Keep, ct);
        if (result.Outcome != ScheduleOutcome.Created)
            return false;

        _logger.LogInformation("Recurring job {Job} scheduled from its default, first at {Next}", job.Key, result.NextFireTimeUtc);
        if (job.OnCreated is not null)
        {
            await using var scope = _scopes.CreateAsyncScope();
            await job.OnCreated(scheduler, scope.ServiceProvider, ct);
        }
        return true;
    }
}
