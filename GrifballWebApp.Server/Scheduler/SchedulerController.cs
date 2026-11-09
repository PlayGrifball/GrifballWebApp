using GrifballWebApp.Database;
using GrifballWebApp.Domain;
using GrifballWebApp.Server.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Scheduler;

/// <summary>
/// The schedule recommendations (organizers): a time for each of a season's regular matches, found by the genetic
/// algorithm in a job on the worker (<see cref="ScheduleRecommendationJob"/>), shown on the season's schedule and
/// applied from there.
/// </summary>
[Route("[controller]/[action]")]
[ApiController]
[Authorize(Roles = "Commissioner,Sysadmin")]
public class SchedulerController : ControllerBase
{
    private readonly GrifballContext _context;
    private readonly ScheduleRecommendations _recommendations;
    private readonly OnDemandJobs _jobs;

    public SchedulerController(GrifballContext context, ScheduleRecommendations recommendations, OnDemandJobs jobs)
    {
        _context = context;
        _recommendations = recommendations;
        _jobs = jobs;
    }

    /// <summary>The season's latest recommendation, and where its run is (queued, running with its progress, or idle).</summary>
    [HttpGet("{seasonID:int}", Name = "GetScheduleRecommendation")]
    public async Task<ScheduleRecommendationPageDto> Recommendation([FromRoute] int seasonID, CancellationToken ct)
        => new()
        {
            Run = await _jobs.Status<ScheduleRecommendationJob>(ScheduleRecommendationJob.RunName(seasonID), ct),
            Latest = await _recommendations.Latest(seasonID, ct),
        };

    /// <summary>Queues a run for the season (nothing more while one is queued or running). 404 for a season that isn't there.</summary>
    [HttpPost("{seasonID:int}", Name = "RecommendSchedule")]
    public async Task<ActionResult<OnDemandJobStatusDto>> Recommend([FromRoute] int seasonID, CancellationToken ct)
    {
        if (!await _context.Seasons.AnyAsync(s => s.SeasonID == seasonID, ct))
            return NotFound();
        return await _jobs.Enqueue<ScheduleRecommendationJob, RecommendSchedule>(ScheduleRecommendationJob.RunName(seasonID),
            new RecommendSchedule(seasonID, User?.Identity?.Name, JobUser.IdOf(User)), $"Season {seasonID}", ct: ct);
    }

    /// <summary>
    /// Gives each undecided match of the recommendation its proposed time. 404 for one that isn't there, 409 when it was
    /// applied already.
    /// </summary>
    [HttpPost("{recommendationID:int}", Name = "ApplyScheduleRecommendation")]
    public async Task<ActionResult<ApplyScheduleRecommendationResponse>> Apply([FromRoute] int recommendationID, CancellationToken ct)
    {
        if (!await _context.ScheduleRecommendations.AnyAsync(r => r.ScheduleRecommendationID == recommendationID, ct))
            return NotFound();
        var applied = await _recommendations.Apply(recommendationID, User?.Identity?.Name, ct);
        if (applied is null)
            return Conflict();
        return new ApplyScheduleRecommendationResponse(applied.Value);
    }
}

public record ApplyScheduleRecommendationResponse(int MatchesScheduled);
