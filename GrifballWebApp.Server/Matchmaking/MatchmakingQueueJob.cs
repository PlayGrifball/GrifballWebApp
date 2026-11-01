using GrifballWebApp.Server.Jobs;
using Quartz;

namespace GrifballWebApp.Server.Matchmaking;

/// <summary>
/// The matchmaking queue's tick (<see cref="QueueService.Go"/>), every 10 seconds by default: drops queued players who
/// are in a match, makes matches from the rest and queues their announcements. Discord only: off (no trigger) while
/// Discord is.
/// </summary>
public sealed class MatchmakingQueueJob : IJob
{
    public static readonly JobKey Key = new("matchmaking-queue", RecurringJobs.Group);

    public static RecurringJob Definition(bool enabled) => new()
    {
        Key = Key,
        Description = "Makes matches from the matchmaking queue and announces them",
        Enabled = enabled,
        // Ticks missed while no worker was up are skipped: the next one does the same work
        DefaultSchedule = (trigger, now) => trigger
            .StartAt(now)
            .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromSeconds(10)).RepeatForever().WithMisfireInstruction(SimpleTriggerMisfireInstruction.NextWithRemainingCount)),
    };

    private readonly QueueService _queue;

    public MatchmakingQueueJob(QueueService queue)
    {
        _queue = queue;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct = default) => await _queue.Go(ct);
}
