using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.MatchDetails;

namespace GrifballWebApp.Server.MatchDetails;

[Route("[controller]/[action]")]
[ApiController]
public class MatchDetailsController : ControllerBase
{
    private readonly MatchDetailsService _matchDetails;

    public MatchDetailsController(MatchDetailsService matchDetails)
    {
        _matchDetails = matchDetails;
    }

    /// <summary>A Halo Infinite match by its Waypoint match ID, with every stat pulled for it; 404 for one not pulled.</summary>
    [HttpGet("{matchID:Guid}", Name = "GetMatchDetails")]
    public async Task<ActionResult<MatchDetailsDto>> GetMatchDetails([FromRoute] Guid matchID, CancellationToken ct)
    {
        var details = await _matchDetails.GetAsync(matchID, ct);
        return details is null ? NotFound() : details;
    }
}
