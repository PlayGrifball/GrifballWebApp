using GrifballWebApp.Database.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Historical;

/// <summary>
/// The historical import: sysadmin only, like the other data pulls in AdminController (the gamertag claims it leaves
/// for a decision are GamertagClaimsController's). The archive is uploaded rather than read from disk: the data/ folder is not in the image,
/// and an upload works the same on every environment. The import runs in the background (HistoricalImportWorker): the
/// upload returns 202 with the job's ID, and <see cref="Job"/> reports its progress and result. Running it again with
/// the same files changes nothing.
/// </summary>
[Route("[controller]/[action]")]
[ApiController]
public class HistoricalImportController : ControllerBase
{
    private readonly HistoricalImportJobs _jobs;

    public HistoricalImportController(HistoricalImportJobs jobs)
    {
        _jobs = jobs;
    }

    /// <param name="seasons">compiled/unified-historical-seasons.json</param>
    /// <param name="championships">compiled/agla-champions.json (optional)</param>
    /// <param name="waypointMatches">compiled/waypoint-matches.json (optional): Halo Infinite seasons' match IDs or
    /// URLs by HistoricalSourceID, <c>{ "2023-da-wl23-stats/Halo Infinite Stats": { "regular": ["…"], "playoffs": ["…"] } }</c></param>
    /// <param name="catalog">compiled/catalog.json (optional): each workbook's link, stored on its seasons.</param>
    /// <param name="ct"></param>
    [HttpPost(Name = "ImportHistoricalArchive")]
    [Authorize(Roles = "Sysadmin")]
    [RequestSizeLimit(64 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 64 * 1024 * 1024)]
    [ProducesResponseType(typeof(HistoricalImportQueued), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Import(IFormFile seasons, IFormFile? championships, IFormFile? waypointMatches, IFormFile? catalog, CancellationToken ct)
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

    private static async Task<T> Read<T>(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await HistoricalArchiveInput.ReadAsync<T>(stream, ct);
    }
}

public record HistoricalImportQueued(int JobID);
