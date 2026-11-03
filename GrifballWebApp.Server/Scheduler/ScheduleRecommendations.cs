using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quartz;

namespace GrifballWebApp.Server.Scheduler;

public sealed record RecommendSchedule(int SeasonID, string? RequestedBy, int? RequestedByUserID = null) : GrifballWebApp.Server.Jobs.IRequestedByUser;

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

    public async ValueTask Execute(IJobExecutionContext context, RecommendSchedule input, CancellationToken ct = default)
    {
        var started = _time.GetTimestamp();
        var run = await _schedule.Recommend(input.SeasonID, _options, (generation, fitness) =>
            context.ReportProgress(Percent(generation, _time.GetElapsedTime(started)), $"Generation {generation}, best fitness {fitness:0.000}"), ct);
        var saved = await _recommendations.Save(input.SeasonID, run, input.RequestedBy, ct);
        context.Result = JobRunReport.Succeeded($"Fitness {run.Fitness:0.000} after {run.Generations} generations; {saved.Matches.Count(m => m.Overlaps || !m.HomeTeamAvailable || !m.AwayTeamAvailable)} of {saved.Matches.Count} matches with a conflict");
    }

    /// <summary>How far along it is: generations or time, whichever limit is nearer (it can end sooner, at a perfect schedule).</summary>
    public int Percent(int generation, TimeSpan elapsed)
    {
        var byGenerations = (double)generation / Math.Max(1, _options.MaxGenerations);
        var byTime = _options.TimeBudget is { } budget && budget > TimeSpan.Zero ? elapsed / budget : 0;
        return (int)Math.Clamp(Math.Max(byGenerations, byTime) * 100, 0, 99);
    }
}

public class ScheduleRecommendationDto
{
    public int ScheduleRecommendationID { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? RequestedBy { get; init; }
    public double Fitness { get; init; }
    public int Generations { get; init; }
    public DateTime? AppliedAt { get; init; }
    public string? AppliedBy { get; init; }
    /// <summary>In order of the proposed time.</summary>
    public required List<ScheduleRecommendationMatchDto> Matches { get; init; }
}

public class ScheduleRecommendationMatchDto
{
    public int SeasonMatchID { get; init; }
    public required string HomeTeam { get; init; }
    public required string AwayTeam { get; init; }
    public DateTime ScheduledTime { get; init; }
    /// <summary>The time it has now; null while unscheduled.</summary>
    public DateTime? CurrentTime { get; init; }
    public bool Overlaps { get; init; }
    public bool HomeTeamAvailable { get; init; }
    public bool AwayTeamAvailable { get; init; }
    /// <summary>A result is in: applying leaves its time alone.</summary>
    public bool Decided { get; init; }
}

/// <summary>The season's schedule page: its recommendation and its run (<see cref="ScheduleRecommendationJob"/>).</summary>
public class ScheduleRecommendationPageDto
{
    public required OnDemandJobStatusDto Run { get; init; }
    /// <summary>The latest recommendation; null before the first.</summary>
    public ScheduleRecommendationDto? Latest { get; init; }
}

/// <summary>
/// The schedule recommendations kept for a season: saved by the job, read by the schedule, and applied (each undecided
/// match gets its proposed time) by a commissioner.
/// </summary>
public class ScheduleRecommendations
{
    private readonly GrifballContext _context;
    private readonly TimeProvider _time;

    public ScheduleRecommendations(GrifballContext context, TimeProvider time)
    {
        _context = context;
        _time = time;
    }

    public async Task<ScheduleRecommendation> Save(int seasonID, ScheduleRun run, string? requestedBy, CancellationToken ct = default)
    {
        var recommendation = new ScheduleRecommendation
        {
            SeasonID = seasonID,
            RequestedBy = requestedBy,
            Fitness = run.Fitness,
            Generations = run.Generations,
            Matches = run.Genes.Select(g => new ScheduleRecommendationMatch
            {
                SeasonMatchID = g.SeasonMatchID,
                ScheduledTime = g.ScheduledTime,
                Overlaps = g.AnyOverlap(run.Genes),
                HomeTeamAvailable = g.HomeTeamAvailable(run.Availability),
                AwayTeamAvailable = g.AwayTeamAvailable(run.Availability),
            }).ToList(),
        };
        _context.ScheduleRecommendations.Add(recommendation);
        await _context.SaveChangesAsync(ct);
        return recommendation;
    }

    public async Task<ScheduleRecommendationDto?> Latest(int seasonID, CancellationToken ct = default)
    {
        var latest = await _context.ScheduleRecommendations.AsNoTracking()
            .Where(r => r.SeasonID == seasonID)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.ScheduleRecommendationID)
            .Include(r => r.Matches)
            .FirstOrDefaultAsync(ct);
        if (latest is null)
            return null;

        var ids = latest.Matches.Select(m => m.SeasonMatchID).ToList();
        var matches = await _context.SeasonMatches.AsNoTracking()
            .Where(sm => ids.Contains(sm.SeasonMatchID))
            .Select(sm => new
            {
                sm.SeasonMatchID,
                HomeTeam = sm.HomeTeam!.TeamName,
                AwayTeam = sm.AwayTeam!.TeamName,
                sm.ScheduledTime,
                Decided = sm.HomeTeamResult != null || sm.AwayTeamResult != null,
            })
            .ToDictionaryAsync(sm => sm.SeasonMatchID, ct);

        return new ScheduleRecommendationDto
        {
            ScheduleRecommendationID = latest.ScheduleRecommendationID,
            CreatedAt = latest.CreatedAt,
            RequestedBy = latest.RequestedBy,
            Fitness = latest.Fitness,
            Generations = latest.Generations,
            AppliedAt = latest.AppliedAt,
            AppliedBy = latest.AppliedBy,
            // A match deleted since is left out
            Matches = latest.Matches.Where(m => matches.ContainsKey(m.SeasonMatchID)).OrderBy(m => m.ScheduledTime).Select(m => new ScheduleRecommendationMatchDto
            {
                SeasonMatchID = m.SeasonMatchID,
                HomeTeam = matches[m.SeasonMatchID].HomeTeam,
                AwayTeam = matches[m.SeasonMatchID].AwayTeam,
                ScheduledTime = m.ScheduledTime,
                CurrentTime = matches[m.SeasonMatchID].ScheduledTime,
                Overlaps = m.Overlaps,
                HomeTeamAvailable = m.HomeTeamAvailable,
                AwayTeamAvailable = m.AwayTeamAvailable,
                Decided = matches[m.SeasonMatchID].Decided,
            }).ToList(),
        };
    }

    /// <summary>
    /// Gives each undecided match of <paramref name="recommendationID"/> its proposed time, in one transaction, and stamps
    /// it applied. Returns the matches it set; null when there is no such recommendation, or it was applied already.
    /// </summary>
    public async Task<int?> Apply(int recommendationID, string? by, CancellationToken ct = default)
    {
        var recommendation = await _context.ScheduleRecommendations.AsNoTracking().Include(r => r.Matches)
            .FirstOrDefaultAsync(r => r.ScheduleRecommendationID == recommendationID, ct);
        if (recommendation is null)
            return null;

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        // Stamped by one statement that only matches it unapplied: two clicks at once apply it once, the other gets null
        var now = _time.GetUtcNow().UtcDateTime;
        var claimed = await _context.ScheduleRecommendations
            .Where(r => r.ScheduleRecommendationID == recommendationID && r.AppliedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.AppliedAt, now).SetProperty(r => r.AppliedBy, by), ct);
        if (claimed == 0)
            return null;

        var times = recommendation.Matches.ToDictionary(m => m.SeasonMatchID, m => m.ScheduledTime);
        var ids = times.Keys.ToList();
        var matches = await _context.SeasonMatches
            .Where(sm => ids.Contains(sm.SeasonMatchID))
            .Where(sm => sm.HomeTeamResult == null && sm.AwayTeamResult == null)
            .ToListAsync(ct);
        foreach (var match in matches)
            match.ScheduledTime = times[match.SeasonMatchID];
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return matches.Count;
    }
}
