using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using System.Text.Json.Serialization;
using GrifballWebApp.Infrastructure.Jobs;

namespace GrifballWebApp.Server.Jobs;

/// <summary>
/// The Jobs page (sysadmin only): the background jobs Quartz runs on the worker, their schedules and runs, and running,
/// pausing and rescheduling them (<see cref="JobsService"/>).
/// </summary>
[ApiController]
[Authorize(Roles = "Sysadmin")]
public class JobsController : ControllerBase
{
    /// <summary>The most runs one history request returns.</summary>
    public const int MaxHistory = 200;

    private readonly JobsService _jobs;

    public JobsController(JobsService jobs)
    {
        _jobs = jobs;
    }

    /// <summary>Every background job: its schedule, whether it's paused, and whether it's running now. Sysadmins only.</summary>
    [HttpGet("jobs", Name = "GetJobs")]
    [ProducesResponseType<JobsOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<JobsOverviewDto>> List(CancellationToken ct) => await _jobs.Overview(ct);

    /// <summary>A job's latest runs, newest first (a week of them at most, and 200 per job: JobsServiceCollectionExtensions).</summary>
    [HttpGet("jobs/{group}/{name}/runs", Name = "GetJobHistory")]
    [ProducesResponseType<List<JobRunDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<JobRunDto>>> History([FromRoute] string group, [FromRoute] string name, [FromQuery] int take = 50, CancellationToken ct = default)
        => await _jobs.History(new JobKey(name, group), Math.Clamp(take, 1, MaxHistory), ct);

    /// <summary>Runs a job now, outside its schedule. Sysadmins only.</summary>
    [HttpPost("jobs/{group}/{name}/run", Name = "RunJob")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Run([FromRoute] string group, [FromRoute] string name, CancellationToken ct)
        => Changed(await _jobs.RunNow(new JobKey(name, group), JobUser.IdOf(User), ct));

    /// <summary>Pauses or resumes a job's schedule. Sysadmins only.</summary>
    [HttpPost("jobs/{group}/{name}/paused", Name = "SetJobPaused")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> SetPaused([FromRoute] string group, [FromRoute] string name, [FromBody] JobPausedRequest request, CancellationToken ct)
        => Changed(await _jobs.SetPaused(new JobKey(name, group), request.Paused, User?.Identity?.Name, ct));

    /// <summary>Changes how often a job's schedule fires. Sysadmins only.</summary>
    [HttpPost("jobs/{group}/{name}/interval", Name = "SetJobInterval")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> SetInterval([FromRoute] string group, [FromRoute] string name, [FromBody] JobIntervalRequest request, CancellationToken ct)
    {
        if (request.IntervalSeconds < JobsService.MinInterval.TotalSeconds || request.IntervalSeconds > JobsService.MaxInterval.TotalSeconds)
            return this.BadRequestProblem($"The interval must be between {JobsService.MinInterval.TotalSeconds:0} seconds and {JobsService.MaxInterval.TotalDays:0} days");
        return Changed(await _jobs.SetInterval(new JobKey(name, group), TimeSpan.FromSeconds(request.IntervalSeconds), ct));
    }

    /// <summary>Resets a job's schedule to its default interval. Sysadmins only.</summary>
    [HttpPost("jobs/{group}/{name}/schedule/reset", Name = "ResetJobSchedule")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> ResetSchedule([FromRoute] string group, [FromRoute] string name, CancellationToken ct)
        => Changed(await _jobs.ResetSchedule(new JobKey(name, group), ct));

    /// <summary>Asks a running job to stop. 404 when it isn't running any more.</summary>
    [HttpPost("job-runs/{fireInstanceID}/interrupt", Name = "InterruptJob")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Interrupt([FromRoute] string fireInstanceID, CancellationToken ct)
        => await _jobs.Interrupt(fireInstanceID, ct) ? NoContent() : NotFound();

    private IActionResult Changed(JobChange change) => change switch
    {
        JobChange.NotFound => NotFound(),
        JobChange.Conflict => Conflict(),
        _ => NoContent(),
    };
}

/// <summary>Whether a job's schedule is paused.</summary>
public record JobPausedRequest([property: JsonRequired] bool Paused);

/// <summary>A job's new interval, in seconds.</summary>
public record JobIntervalRequest([property: JsonRequired] long IntervalSeconds);
