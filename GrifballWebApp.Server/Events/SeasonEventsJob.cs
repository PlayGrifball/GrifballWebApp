using GrifballWebApp.Server.Jobs;
using Quartz;

namespace GrifballWebApp.Server.Events;

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

    public SeasonEventsJob(EventsService events)
    {
        _events = events;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct = default) => await _events.Go(ct);
}
