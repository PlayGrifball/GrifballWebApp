using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Historical.Vods;

namespace GrifballWebApp.Server.Historical.Vods;

/// <summary>
/// The league's VODs: anyone sees those linked to a match, season, team or player; sysadmins decide the ones the matcher
/// wasn't sure of (the historical import page). Finding them is the worker's (the historical refresh), not a request's.
/// </summary>
[ApiController]
public class VodController : ControllerBase
{
    private readonly VodService _vods;

    public VodController(VodService vods)
    {
        _vods = vods;
    }

    [AllowAnonymous]
    [HttpGet("season-matches/{seasonMatchID:int}/vods", Name = "GetSeasonMatchVods")]
    [ProducesResponseType<List<VodDto>>(StatusCodes.Status200OK)]
    public Task<List<VodDto>> ForSeasonMatch([FromRoute] int seasonMatchID, CancellationToken ct) => _vods.ForSeasonMatch(seasonMatchID, ct);

    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/vods", Name = "GetSeasonVods")]
    [ProducesResponseType<List<VodDto>>(StatusCodes.Status200OK)]
    public Task<List<VodDto>> ForSeason([FromRoute] int seasonID, CancellationToken ct) => _vods.ForSeason(seasonID, ct);

    [AllowAnonymous]
    [HttpGet("teams/{teamID:int}/vods", Name = "GetTeamVods")]
    [ProducesResponseType<List<VodDto>>(StatusCodes.Status200OK)]
    public Task<List<VodDto>> ForTeam([FromRoute] int teamID, CancellationToken ct) => _vods.ForTeam(teamID, ct);

    /// <summary>The VODs of the player's teams.</summary>
    [AllowAnonymous]
    [HttpGet("users/{userID:int}/vods", Name = "GetPlayerVods")]
    [ProducesResponseType<List<VodDto>>(StatusCodes.Status200OK)]
    public Task<List<VodDto>> ForPlayer([FromRoute] int userID, CancellationToken ct) => _vods.ForPlayer(userID, ct);

    /// <summary>The VODs waiting for a decision, with the matcher's suggestion.</summary>
    [HttpGet("vods/review", Name = "GetVodReview")]
    [ProducesResponseType<List<VodReviewDto>>(StatusCodes.Status200OK)]
    [Authorize(Roles = "Sysadmin")]
    public Task<List<VodReviewDto>> Review(CancellationToken ct) => _vods.Review(ct);

    [HttpGet("vods/link-seasons", Name = "GetVodLinkSeasons")]
    [ProducesResponseType<List<VodLinkSeasonDto>>(StatusCodes.Status200OK)]
    [Authorize(Roles = "Sysadmin")]
    public Task<List<VodLinkSeasonDto>> LinkSeasons(CancellationToken ct) => _vods.LinkSeasons(ct);

    [HttpGet("seasons/{seasonID:int}/vod-link-options", Name = "GetVodLinkOptions")]
    [ProducesResponseType<VodLinkOptionsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [Authorize(Roles = "Sysadmin")]
    public async Task<ActionResult<VodLinkOptionsDto>> LinkOptions([FromRoute] int seasonID, CancellationToken ct)
        => await _vods.LinkOptions(seasonID, ct) is { } options ? options : this.NotFoundProblem("There is no such season");

    [HttpPost("vods/{vodID:int}/link", Name = "LinkVod")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [Authorize(Roles = "Sysadmin")]
    public async Task<IActionResult> Link([FromRoute] int vodID, [FromBody] LinkVodRequest request, CancellationToken ct)
    {
        try
        {
            return await _vods.Link(vodID, request, ct) is { } error ? this.BadRequestProblem(error) : NoContent();
        }
        catch (KeyNotFoundException)
        {
            return this.NotFoundProblem("There is no such VOD");
        }
    }

    /// <summary>Not a VOD of anything: hidden, and never suggested again.</summary>
    [HttpPost("vods/{vodID:int}/dismiss", Name = "DismissVod")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [Authorize(Roles = "Sysadmin")]
    public async Task<IActionResult> Dismiss([FromRoute] int vodID, CancellationToken ct)
        => await _vods.Dismiss(vodID, ct) ? NoContent() : this.NotFoundProblem("There is no such VOD");
}
