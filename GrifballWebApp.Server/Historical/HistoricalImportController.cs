using GrifballWebApp.Server.Historical.Sources;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Historical;

/// <summary>
/// The historical import: sysadmin only, like the other data pulls in AdminController (the gamertag claims it leaves
/// for a decision are GamertagClaimsController's). The app downloads the archive's sources itself (<see cref="Refresh"/>,
/// and on a schedule, HistoricalRefreshJob) and imports their snapshots: <see cref="Preview"/> shows what an import would remove, then
/// <see cref="ApplyPreview"/> imports it. A compiled archive can still be uploaded (<see cref="Import"/>). Everything runs
/// in the background (HistoricalImportWorker, in the worker role): each action returns 202 with the job's ID, and
/// <see cref="Job"/> reports its progress and result. Running an import again with the same data changes nothing.
/// </summary>
[Route("[controller]/[action]")]
[ApiController]
public class HistoricalImportController : ControllerBase
{
    private readonly HistoricalImportJobs _jobs;
    private readonly HistoricalSourceService _sources;

    public HistoricalImportController(HistoricalImportJobs jobs, HistoricalSourceService sources)
    {
        _jobs = jobs;
        _sources = sources;
    }

    /// <param name="seasons">compiled/unified-historical-seasons.json</param>
    /// <param name="championships">compiled/agla-champions.json (optional)</param>
    /// <param name="waypointMatches">compiled/waypoint-matches.json (optional): Halo Infinite seasons' match IDs or
    /// URLs by HistoricalSourceID, <c>{ "2023-da-wl23-stats/Halo Infinite Stats": { "regular": ["…"], "playoffs": ["…"] } }</c></param>
    /// <param name="catalog">compiled/catalog.json (optional): each workbook's link, stored on its seasons.</param>
    /// <param name="ct"></param>
    /// <param name="players">compiled/late-league-master.json (optional): its players list's Discord IDs and locations.</param>
    [HttpPost(Name = "ImportHistoricalArchive")]
    [Authorize(Roles = "Sysadmin")]
    [RequestSizeLimit(64 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 64 * 1024 * 1024)]
    [ProducesResponseType(typeof(HistoricalImportQueued), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Import(IFormFile seasons, IFormFile? championships, IFormFile? waypointMatches, IFormFile? catalog, CancellationToken ct, IFormFile? players = null)
    {
        HistoricalArchiveInput input;
        try
        {
            input = new HistoricalArchiveInput
            {
                Seasons = await Read<UnifiedArchive>(seasons, ct),
                Championships = championships is null ? null : await Read<ChampionsArchive>(championships, ct),
                WaypointMatches = waypointMatches is null ? null : await Read<Dictionary<string, WaypointSeasonMatches>>(waypointMatches, ct),
                Catalog = catalog is null ? null : await Read<ArchiveCatalog>(catalog, ct),
                Players = players is null ? null : (await Read<LateLeagueMasterFile>(players, ct)).ToPlayers(),
            };
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidDataException)
        {
            return BadRequest($"Could not read the archive: {e.Message}");
        }
        // Not the request's token: the job outlives the request
        var jobId = await _jobs.Enqueue(input, CancellationToken.None);
        return AcceptedAtAction(nameof(Job), new { jobId }, new HistoricalImportQueued(jobId));
    }

    [HttpGet("{jobId:int}", Name = "GetHistoricalImportJob")]
    [Authorize(Roles = "Sysadmin")]
    public async Task<ActionResult<HistoricalImportJobDto>> Job([FromRoute] int jobId, CancellationToken ct)
        => await _jobs.Get(jobId, ct) is { } job ? job : NotFound();

    [HttpGet(Name = "GetHistoricalSources")]
    [Authorize(Roles = "Sysadmin")]
    public async Task<ActionResult<List<HistoricalSourceDto>>> Sources(CancellationToken ct) => await _sources.List(ct);

    /// <summary>A disabled source is no longer downloaded; the import keeps reading its last snapshot. A dead source can't
    /// be enabled (409).</summary>
    [HttpPost("{sourceId}", Name = "SetHistoricalSourceEnabled")]
    [Authorize(Roles = "Sysadmin")]
    public async Task<IActionResult> SetSourceEnabled([FromRoute] string sourceId, [FromBody] SetEnabledRequest request, CancellationToken ct)
        => await _sources.SetEnabled(sourceId, request.Enabled, ct) switch
        {
            null => NotFound(),
            HistoricalSourceSetEnabledResult.Dead => Conflict("That source is dead: it is never downloaded, so it can't be enabled."),
            _ => NoContent(),
        };

    /// <summary>Downloads every enabled source, keeping each content that changed (no import).</summary>
    [HttpPost(Name = "RefreshHistoricalSources")]
    [Authorize(Roles = "Sysadmin")]
    [ProducesResponseType(typeof(HistoricalImportQueued), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
        => Queued(await _jobs.Enqueue(HistoricalImportJob.Refresh(), CancellationToken.None));

    /// <summary>A dry run of importing every source's latest snapshot: the job lists each season's removals.</summary>
    [HttpPost(Name = "PreviewHistoricalImport")]
    [Authorize(Roles = "Sysadmin")]
    [ProducesResponseType(typeof(HistoricalImportQueued), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Preview(CancellationToken ct)
        => Queued(await _jobs.Enqueue(HistoricalImportJob.Preview(), CancellationToken.None));

    /// <summary>Imports what a finished preview read.</summary>
    [HttpPost("{jobId:int}", Name = "ApplyHistoricalImportPreview")]
    [Authorize(Roles = "Sysadmin")]
    [ProducesResponseType(typeof(HistoricalImportQueued), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ApplyPreview([FromRoute] int jobId, CancellationToken ct)
        => Decided(await _jobs.ApplyPreview(jobId, CancellationToken.None));

    /// <summary>Imports a season the guardrail stopped (queues its import).</summary>
    [HttpPost("{reviewId:int}", Name = "ApproveHistoricalSeasonReview")]
    [Authorize(Roles = "Sysadmin")]
    [ProducesResponseType(typeof(HistoricalImportQueued), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ApproveReview([FromRoute] int reviewId, CancellationToken ct)
        => Decided(await _jobs.Decide(reviewId, approve: true, CancellationToken.None));

    /// <summary>Leaves a season the guardrail stopped as it is.</summary>
    [HttpPost("{reviewId:int}", Name = "SkipHistoricalSeasonReview")]
    [Authorize(Roles = "Sysadmin")]
    public async Task<IActionResult> SkipReview([FromRoute] int reviewId, CancellationToken ct)
        => Decided(await _jobs.Decide(reviewId, approve: false, CancellationToken.None));

    /// <summary>The latest jobs, newest first.</summary>
    [HttpGet(Name = "GetHistoricalImportJobs")]
    [Authorize(Roles = "Sysadmin")]
    public async Task<ActionResult<List<HistoricalImportJobSummaryDto>>> Jobs(CancellationToken ct) => await _jobs.List(25, ct);

    [HttpGet(Name = "GetHistoricalImportSchedule")]
    [Authorize(Roles = "Sysadmin")]
    public async Task<ActionResult<HistoricalImportScheduleDto>> Schedule([FromServices] HistoricalImportSchedule schedule, CancellationToken ct)
        => await schedule.Get(ct);

    /// <summary>
    /// Paused, the schedule queues nothing; manual runs still work. 409 before a worker has made the schedule's trigger
    /// (it does as it starts).
    /// </summary>
    [HttpPost(Name = "SetHistoricalImportSchedulePaused")]
    [Authorize(Roles = "Sysadmin")]
    public async Task<ActionResult<HistoricalImportScheduleDto>> SetSchedulePaused([FromServices] HistoricalImportSchedule schedule, [FromBody] SetPausedRequest request, CancellationToken ct)
    {
        if (!await schedule.SetPaused(request.Paused, User?.Identity?.Name, ct))
            return Conflict();
        return await schedule.Get(ct);
    }

    private AcceptedAtActionResult Queued(int jobId) => AcceptedAtAction(nameof(Job), new { jobId }, new HistoricalImportQueued(jobId));

    private IActionResult Decided((HistoricalJobDecision Decision, int? JobID) result) => result.Decision switch
    {
        HistoricalJobDecision.NotFound => NotFound(),
        HistoricalJobDecision.Conflict => Conflict(),
        _ => result.JobID is int jobId ? Queued(jobId) : NoContent(),
    };

    private static async Task<T> Read<T>(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await HistoricalArchiveInput.ReadAsync<T>(stream, ct);
    }
}

public record HistoricalImportQueued(int JobID);

public record SetEnabledRequest(bool Enabled);

public record SetPausedRequest(bool Paused);
