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
[Route("[controller]/[action]")]
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

    [HttpGet(Name = "GetJobs")]
    public async Task<ActionResult<JobsOverviewDto>> List(CancellationToken ct) => await _jobs.Overview(ct);

    /// <summary>A job's latest runs, newest first (a week of them at most, and 200 per job: JobsServiceCollectionExtensions).</summary>
    [HttpGet(Name = "GetJobHistory")]
    public async Task<ActionResult<List<JobRunDto>>> History([FromQuery] string group, [FromQuery] string name, [FromQuery] int take = 50, CancellationToken ct = default)
        => await _jobs.History(new JobKey(name, group), Math.Clamp(take, 1, MaxHistory), ct);

    [HttpPost(Name = "RunJob")]
    public async Task<IActionResult> Run([FromBody] JobKeyRequest request, CancellationToken ct)
        => Changed(await _jobs.RunNow(request.Key, JobUser.IdOf(User), ct));

    [HttpPost(Name = "SetJobPaused")]
    public async Task<IActionResult> SetPaused([FromBody] SetJobPausedRequest request, CancellationToken ct)
        => Changed(await _jobs.SetPaused(request.Key, request.Paused, User?.Identity?.Name, ct));

    [HttpPost(Name = "SetJobInterval")]
    public async Task<IActionResult> SetInterval([FromBody] SetJobIntervalRequest request, CancellationToken ct)
    {
        if (request.IntervalSeconds < JobsService.MinInterval.TotalSeconds || request.IntervalSeconds > JobsService.MaxInterval.TotalSeconds)
            return BadRequest($"The interval must be between {JobsService.MinInterval.TotalSeconds:0} seconds and {JobsService.MaxInterval.TotalDays:0} days");
        return Changed(await _jobs.SetInterval(request.Key, TimeSpan.FromSeconds(request.IntervalSeconds), ct));
    }

    [HttpPost(Name = "ResetJobSchedule")]
    public async Task<IActionResult> ResetSchedule([FromBody] JobKeyRequest request, CancellationToken ct)
        => Changed(await _jobs.ResetSchedule(request.Key, ct));

    /// <summary>Asks a running job to stop. 404 when it isn't running any more.</summary>
    [HttpPost(Name = "InterruptJob")]
    public async Task<IActionResult> Interrupt([FromBody] InterruptJobRequest request, CancellationToken ct)
        => await _jobs.Interrupt(request.FireInstanceID, ct) ? NoContent() : NotFound();

    private IActionResult Changed(JobChange change) => change switch
    {
        JobChange.NotFound => NotFound(),
        JobChange.Conflict => Conflict(),
        _ => NoContent(),
    };
}

public record JobKeyRequest(string Group, string Name)
{
    [JsonIgnore]
    public JobKey Key => new(Name, Group);
}

public record SetJobPausedRequest(string Group, string Name, [property: JsonRequired] bool Paused) : JobKeyRequest(Group, Name);

public record SetJobIntervalRequest(string Group, string Name, [property: JsonRequired] long IntervalSeconds) : JobKeyRequest(Group, Name);

public record InterruptJobRequest(string FireInstanceID);
