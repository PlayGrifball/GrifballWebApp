using Microsoft.Extensions.Options;
using Quartz;
using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Infrastructure.Jobs;

namespace GrifballWebApp.Infrastructure.Scheduler;

public sealed record RecommendSchedule(int SeasonID, string? RequestedBy, int? RequestedByUserID = null) : IRequestedByUser;

/// <summary>
/// Runs the genetic algorithm for a season's regular matches (<see cref="ScheduleService.Recommend"/>) on the worker and
/// keeps its best schedule (<see cref="ScheduleRecommendation"/>): started from the season's schedule
/// (<see cref="OnDemandJobs"/>), reporting the generation it is on and the best fitness so far.
/// </summary>
[DisallowConcurrentExecution]
public sealed class ScheduleRecommendationJob : IJob<RecommendSchedule>
{
    public const string Description = "Recommends a time for each of a season's regular matches (from its schedule)";

    /// <summary>A season's run: one queued or running at a time.</summary>
    public static string RunName(int seasonID) => $"season-{seasonID}";

    private readonly ScheduleService _schedule;
    private readonly ScheduleRecommendations _recommendations;
    private readonly ScheduleRunOptions _options;
    private readonly TimeProvider _time;

    public ScheduleRecommendationJob(ScheduleService schedule, ScheduleRecommendations recommendations, IOptions<ScheduleRunOptions> options, TimeProvider time)
    {
        _schedule = schedule;
        _recommendations = recommendations;
        _options = options.Value;
        _time = time;
    }

    public async ValueTask Execute(IJobExecutionContext context, RecommendSchedule input, CancellationToken cancellationToken = default)
    {
        var started = _time.GetTimestamp();
        var run = await _schedule.Recommend(input.SeasonID, _options, (generation, fitness) =>
            context.ReportProgress(Percent(generation, _time.GetElapsedTime(started)), $"Generation {generation}, best fitness {fitness:0.000}"), cancellationToken);
        var saved = await _recommendations.Save(input.SeasonID, run, input.RequestedBy, cancellationToken);
        context.Result = JobRunReport.Succeeded($"Fitness {run.Fitness:0.000} after {run.Generations} generations; {saved.ConflictCount} of {saved.Matches.Count} matches with a conflict");
    }

    /// <summary>How far along it is: generations or time, whichever limit is nearer (it can end sooner, at a perfect schedule).</summary>
    public int Percent(int generation, TimeSpan elapsed)
    {
        var byGenerations = (double)generation / Math.Max(1, _options.MaxGenerations);
        var byTime = _options.TimeBudget is { } budget && budget > TimeSpan.Zero ? elapsed / budget : 0;
        return (int)Math.Clamp(Math.Max(byGenerations, byTime) * 100, 0, 99);
    }
}
