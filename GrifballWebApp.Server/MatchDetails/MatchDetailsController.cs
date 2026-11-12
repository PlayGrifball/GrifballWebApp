using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.MatchDetails;

namespace GrifballWebApp.Server.MatchDetails;

[ApiController]
public class MatchDetailsController : ControllerBase
{
    private readonly MatchDetailsService _matchDetails;

    public MatchDetailsController(MatchDetailsService matchDetails)
    {
        _matchDetails = matchDetails;
    }

    /// <summary>A Halo Infinite match by its Waypoint match ID, with every stat pulled for it; 404 for one not pulled.</summary>
    [AllowAnonymous]
    [HttpGet("matches/{matchID:guid}", Name = "GetMatchDetails")]
    [ProducesResponseType<MatchDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MatchDetailsDto>> GetMatchDetails([FromRoute] Guid matchID, CancellationToken ct)
    {
        var details = await _matchDetails.GetAsync(matchID, ct);
        return details is null ? this.NotFoundProblem("That match has not been pulled") : details;
    }
}
