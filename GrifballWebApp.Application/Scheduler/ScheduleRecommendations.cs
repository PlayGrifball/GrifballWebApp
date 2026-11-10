using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Jobs;

namespace GrifballWebApp.Application.Scheduler;

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

/// <summary>The season's schedule page: its recommendation and its run (<c>ScheduleRecommendationJob</c>).</summary>
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
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _time;

    public ScheduleRecommendations(IApplicationDbContext context, TimeProvider time)
    {
        _context = context;
        _time = time;
    }

    public async Task<ScheduleRecommendation> Save(int seasonID, ScheduleRun run, string? requestedBy, CancellationToken ct = default)
    {
        var recommendation = ScheduleRecommendation.Propose(seasonID, requestedBy, run.Fitness, run.Generations, run.Genes.Select(g => new ProposedMatchTime(
            g.SeasonMatchID, g.ScheduledTime, g.AnyOverlap(run.Genes), g.HomeTeamAvailable(run.Availability), g.AwayTeamAvailable(run.Availability))));
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
        var matches = (await _context.SeasonMatches.AsNoTracking()
            .Include(sm => sm.HomeTeam)
            .Include(sm => sm.AwayTeam)
            .Where(sm => ids.Contains(sm.SeasonMatchID))
            .ToListAsync(ct))
            .ToDictionary(sm => sm.SeasonMatchID, sm => new
            {
                HomeTeam = sm.HomeTeam!.TeamName,
                AwayTeam = sm.AwayTeam!.TeamName,
                sm.ScheduledTime,
                Decided = sm.IsDecided,
            });

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
        // The recommendation says whether it can be applied, and stamps itself
        if (recommendation.Apply(by, _time.GetUtcNow().UtcDateTime) is not ScheduleRecommendationApply.Applied)
            return null;

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        // Its stamp written by one statement that only matches it unapplied: two clicks at once apply it once, the other gets null
        var claimed = await _context.ScheduleRecommendations
            .Where(r => r.ScheduleRecommendationID == recommendationID)
            .Where(ScheduleRecommendation.Unapplied)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.AppliedAt, recommendation.AppliedAt).SetProperty(r => r.AppliedBy, recommendation.AppliedBy), ct);
        if (claimed == 0)
            return null;

        var times = recommendation.ProposedTimes();
        var ids = times.Keys.ToList();
        var matches = await _context.SeasonMatches
            .Where(sm => ids.Contains(sm.SeasonMatchID))
            .Where(SeasonMatch.Undecided)
            .ToListAsync(ct);
        foreach (var match in matches)
            match.ScheduleAt(times[match.SeasonMatchID]);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return matches.Count;
    }
}
