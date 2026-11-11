using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Historical.Vods;

namespace GrifballWebApp.Server.Historical.Vods;

/// <summary>
/// The league's VODs: anyone sees those linked to a match, season, team or player; sysadmins decide the ones the matcher
/// wasn't sure of (the historical import page). Finding them is the worker's (the historical refresh), not a request's.
/// </summary>
[Route("[controller]/[action]")]
[ApiController]
public class VodController : ControllerBase
{
    private readonly VodService _vods;

    public VodController(VodService vods)
    {
        _vods = vods;
    }

    [AllowAnonymous]
    [HttpGet("{seasonMatchID:int}", Name = "GetSeasonMatchVods")]
    public Task<List<VodDto>> ForSeasonMatch([FromRoute] int seasonMatchID, CancellationToken ct) => _vods.ForSeasonMatch(seasonMatchID, ct);

    [AllowAnonymous]
    [HttpGet("{seasonID:int}", Name = "GetSeasonVods")]
    public Task<List<VodDto>> ForSeason([FromRoute] int seasonID, CancellationToken ct) => _vods.ForSeason(seasonID, ct);

    [AllowAnonymous]
    [HttpGet("{teamID:int}", Name = "GetTeamVods")]
    public Task<List<VodDto>> ForTeam([FromRoute] int teamID, CancellationToken ct) => _vods.ForTeam(teamID, ct);

    /// <summary>The VODs of the player's teams.</summary>
    [AllowAnonymous]
    [HttpGet("{userID:int}", Name = "GetPlayerVods")]
    public Task<List<VodDto>> ForPlayer([FromRoute] int userID, CancellationToken ct) => _vods.ForPlayer(userID, ct);

    /// <summary>The VODs waiting for a decision, with the matcher's suggestion.</summary>
    [HttpGet(Name = "GetVodReview")]
    [Authorize(Roles = "Sysadmin")]
    public Task<List<VodReviewDto>> Review(CancellationToken ct) => _vods.Review(ct);

    [HttpGet(Name = "GetVodLinkSeasons")]
    [Authorize(Roles = "Sysadmin")]
    public Task<List<VodLinkSeasonDto>> LinkSeasons(CancellationToken ct) => _vods.LinkSeasons(ct);

    [HttpGet("{seasonID:int}", Name = "GetVodLinkOptions")]
    [Authorize(Roles = "Sysadmin")]
    public async Task<ActionResult<VodLinkOptionsDto>> LinkOptions([FromRoute] int seasonID, CancellationToken ct)
        => await _vods.LinkOptions(seasonID, ct) is { } options ? options : NotFound();

    [HttpPost("{vodID:int}", Name = "LinkVod")]
    [Authorize(Roles = "Sysadmin")]
    public async Task<IActionResult> Link([FromRoute] int vodID, [FromBody] LinkVodRequest request, CancellationToken ct)
    {
        try
        {
            return await _vods.Link(vodID, request, ct) is { } error ? BadRequest(error) : NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Not a VOD of anything: hidden, and never suggested again.</summary>
    [HttpPost("{vodID:int}", Name = "DismissVod")]
    [Authorize(Roles = "Sysadmin")]
    public async Task<IActionResult> Dismiss([FromRoute] int vodID, CancellationToken ct)
        => await _vods.Dismiss(vodID, ct) ? NoContent() : NotFound();
}
