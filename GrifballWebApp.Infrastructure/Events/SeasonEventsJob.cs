using Quartz;
using GrifballWebApp.Infrastructure.Jobs;

namespace GrifballWebApp.Infrastructure.Events;

/// <summary>
/// Keeps the events channel's season announcements up to date (<see cref="EventsService.Go"/>), every 10 seconds by
/// default. Discord only: off (no trigger) while Discord is.
/// </summary>
public sealed class SeasonEventsJob : IJob
{
    public static readonly JobKey Key = new("season-events", RecurringJobs.Group);

    public static RecurringJob Definition(bool enabled) => new()
    {
        Key = Key,
        Description = "Updates the season announcements in the Discord events channel",
        Enabled = enabled,
        // Ticks missed while no worker was up are skipped: the next one does the same work
        DefaultSchedule = (trigger, now) => trigger
            .StartAt(now)
            .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromSeconds(10)).RepeatForever().WithMisfireInstruction(SimpleTriggerMisfireInstruction.NextWithRemainingCount)),
    };

    private readonly EventsService _events;
    private readonly RepeatedFailures _failures;

    public SeasonEventsJob(EventsService events, RepeatedFailures failures)
    {
        _events = events;
        _failures = failures;
    }

    public ValueTask Execute(IJobExecutionContext context, CancellationToken ct = default)
    {
        // Every 10 seconds: not traced
        JobTracing.DontRecordThisRun();
        return _failures.RunAsync(context, _events.Go, ct);
    }
}
