using GrifballWebApp.Server.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.MatchPlanner;

[Route("[controller]/[action]")]
[ApiController]
public class MatchPlannerController : ControllerBase
{
    private readonly ILogger<MatchPlannerController> _logger;
    private readonly MatchPlannerService _matchPlannerService;

    public MatchPlannerController(ILogger<MatchPlannerController> logger, MatchPlannerService matchPlannerService)
    {
        _logger = logger;
        _matchPlannerService = matchPlannerService;
    }

    [HttpGet(Name = "CreateSeasonMatches")]
    public async Task<ActionResult> CreateSeasonMatches([FromQuery] int seasonID)
    {
        if (seasonID <= 0)
            return BadRequest("Please provide seasonID");

        await _matchPlannerService.CreateSeasonMatches(seasonID);
        return Ok();
    }

    [HttpGet("{seasonID:int}", Name = "GetUnscheduledMatches")]
    public async Task<ActionResult> GetUnscheduledMatches([FromRoute] int seasonID)
    {
        if (seasonID <= 0)
            return BadRequest("Please provide seasonID");

        var result = await _matchPlannerService.GetUnscheduledMatches(seasonID);
        return Ok(result);
    }

    [HttpGet("{seasonID:int}", Name = "GetScheduledMatches")]
    public async Task<ActionResult> GetScheduledMatches([FromRoute] int seasonID)
    {
        if (seasonID <= 0)
            return BadRequest("Please provide seasonID");

        var result = await _matchPlannerService.GetScheduledMatches(seasonID);
        return Ok(result);
    }

    [HttpPost(Name = "UpdateMatchTime")]
    public Task UpdateMatchTime([FromBody] UpdateMatchTimeDto dto)
    {
        return _matchPlannerService.UpdateMatchTime(dto);
    }
}
