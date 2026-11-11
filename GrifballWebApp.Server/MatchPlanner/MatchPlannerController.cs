using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.MatchPlanner;

namespace GrifballWebApp.Server.MatchPlanner;

[Route("[controller]/[action]")]
[ApiController]
public class MatchPlannerController : ControllerBase
{
    private readonly MatchPlannerService _matchPlannerService;

    public MatchPlannerController(ILogger<MatchPlannerController> logger, MatchPlannerService matchPlannerService)
    {
        _matchPlannerService = matchPlannerService;
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost(Name = "CreateSeasonMatches")]
    public async Task<ActionResult> CreateSeasonMatches([FromQuery] int seasonID, [FromQuery] int homeMatchesPerTeam, [FromQuery] int bestOf, CancellationToken ct)
    {
        if (seasonID <= 0)
            return BadRequest("Please provide seasonID");

        if (homeMatchesPerTeam <= 0)
            return BadRequest("homeMatchesPerTeam should be at least 1");

        if (bestOf <= 0)
            return BadRequest("bestOf should be at least 1");

        await _matchPlannerService.CreateSeasonMatches(seasonID: seasonID, homeMatchesPerTeam: homeMatchesPerTeam, bestOf: bestOf, ct);
        return Ok();
    }

    [AllowAnonymous]
    [HttpGet("{seasonID:int}", Name = "GetUnscheduledMatches")]
    public async Task<ActionResult> GetUnscheduledMatches([FromRoute] int seasonID)
    {
        if (seasonID <= 0)
            return BadRequest("Please provide seasonID");

        var result = await _matchPlannerService.GetUnscheduledMatches(seasonID);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("{seasonID:int}", Name = "GetScheduledMatches")]
    public async Task<ActionResult> GetScheduledMatches([FromRoute] int seasonID)
    {
        if (seasonID <= 0)
            return BadRequest("Please provide seasonID");

        var result = await _matchPlannerService.GetScheduledMatches(seasonID);
        return Ok(result);
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost(Name = "UpdateMatchTime")]
    public Task UpdateMatchTime([FromBody] UpdateMatchTimeDto dto)
    {
        return _matchPlannerService.UpdateMatchTime(dto);
    }
}
