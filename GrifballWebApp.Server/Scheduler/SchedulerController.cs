using GrifballWebApp.Application.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.Scheduler;

namespace GrifballWebApp.Server.Scheduler;

/// <summary>
/// The schedule recommendations (organizers): a time for each of a season's regular matches, found by the genetic
/// algorithm in a job on the worker (<see cref="ScheduleRecommendationJob"/>), shown on the season's schedule and
/// applied from there.
/// </summary>
[ApiController]
[Authorize(Roles = "Commissioner,Sysadmin")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public class SchedulerController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ScheduleRecommendations _recommendations;
    private readonly IScheduleRecommendationRuns _runs;

    public SchedulerController(IApplicationDbContext context, ScheduleRecommendations recommendations, IScheduleRecommendationRuns runs)
    {
        _context = context;
        _recommendations = recommendations;
        _runs = runs;
    }

    /// <summary>The season's latest recommendation, and where its run is (queued, running with its progress, or idle).</summary>
    [HttpGet("seasons/{seasonID:int}/schedule-recommendation", Name = "GetScheduleRecommendation")]
    [ProducesResponseType<ScheduleRecommendationPageDto>(StatusCodes.Status200OK)]
    public async Task<ScheduleRecommendationPageDto> Recommendation([FromRoute] int seasonID, CancellationToken ct)
        => new()
        {
            Run = await _runs.Status(seasonID, ct),
            Latest = await _recommendations.Latest(seasonID, ct),
        };

    /// <summary>Queues a run for the season (nothing more while one is queued or running): a 202 with where it is. 404 for a season that isn't there.</summary>
    [HttpPost("seasons/{seasonID:int}/schedule-recommendation/run", Name = "RecommendSchedule")]
    [ProducesResponseType<OnDemandJobStatusDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<OnDemandJobStatusDto>> Recommend([FromRoute] int seasonID, CancellationToken ct)
    {
        if (!await _context.Seasons.AnyAsync(s => s.SeasonID == seasonID, ct))
            return this.NotFoundProblem("There is no such season");
        return Accepted(await _runs.Queue(seasonID, User?.Identity?.Name, JobUser.IdOf(User), ct));
    }

    /// <summary>
    /// Gives each undecided match of the recommendation its proposed time. 404 for one that isn't there, 409 when it was
    /// applied already.
    /// </summary>
    [HttpPost("schedule-recommendations/{recommendationID:int}/apply", Name = "ApplyScheduleRecommendation")]
    [ProducesResponseType<ApplyScheduleRecommendationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ApplyScheduleRecommendationResponse>> Apply([FromRoute] int recommendationID, CancellationToken ct)
    {
        if (!await _context.ScheduleRecommendations.AnyAsync(r => r.ScheduleRecommendationID == recommendationID, ct))
            return this.NotFoundProblem("There is no such schedule recommendation");
        var applied = await _recommendations.Apply(recommendationID, User?.Identity?.Name, ct);
        if (applied is null)
            return this.ConflictProblem("That recommendation was applied already");
        return new ApplyScheduleRecommendationResponse(applied.Value);
    }
}

public record ApplyScheduleRecommendationResponse(int MatchesScheduled);
