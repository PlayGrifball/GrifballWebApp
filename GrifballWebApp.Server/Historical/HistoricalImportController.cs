using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Historical;

/// <summary>
/// Runs the historical import, sysadmin only, like the other data pulls (AdminController). The archive is uploaded
/// rather than read from disk: the data/ folder is not in the image, and an upload works the same on every
/// environment. Running it again with the same files changes nothing.
/// </summary>
[Route("[controller]/[action]")]
[ApiController]
[Authorize(Roles = "Sysadmin")]
public class HistoricalImportController : ControllerBase
{
    private readonly IHistoricalImportService _importService;

    public HistoricalImportController(IHistoricalImportService importService)
    {
        _importService = importService;
    }

    /// <param name="seasons">compiled/unified-historical-seasons.json</param>
    /// <param name="championships">compiled/agla-champions.json (optional)</param>
    /// <param name="waypointMatches">Halo Infinite seasons' Waypoint match IDs or URLs by HistoricalSourceID (optional):
    /// <c>{ "agla/winter-league-2023-da": { "regular": ["…"], "playoffs": ["…"] } }</c></param>
    [HttpPost(Name = "ImportHistoricalArchive")]
    [RequestSizeLimit(64 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 64 * 1024 * 1024)]
    public async Task<ActionResult<HistoricalImportReport>> Import(IFormFile seasons, IFormFile? championships, IFormFile? waypointMatches, CancellationToken ct)
    {
        HistoricalArchiveInput input;
        try
        {
            input = new HistoricalArchiveInput
            {
                Seasons = await Read<UnifiedArchive>(seasons, ct),
                Championships = championships is null ? null : await Read<ChampionsArchive>(championships, ct),
                WaypointMatches = waypointMatches is null ? null : await Read<Dictionary<string, WaypointSeasonMatches>>(waypointMatches, ct),
            };
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidDataException)
        {
            return BadRequest($"Could not read the archive: {e.Message}");
        }
        return await _importService.Import(input, ct);
    }

    private static async Task<T> Read<T>(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await HistoricalArchiveInput.ReadAsync<T>(stream, ct);
    }
}
