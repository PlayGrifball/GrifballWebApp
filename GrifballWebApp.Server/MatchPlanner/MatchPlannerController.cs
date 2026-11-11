using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.MatchPlanner;

namespace GrifballWebApp.Server.MatchPlanner;

/// <summary>A season's regular matches: made from its teams, listed scheduled and unscheduled, and given their times.</summary>
[ApiController]
public class MatchPlannerController : ControllerBase
{
    private readonly MatchPlannerService _matchPlannerService;

    public MatchPlannerController(MatchPlannerService matchPlannerService)
    {
        _matchPlannerService = matchPlannerService;
    }

    /// <summary>Makes the season's regular matches: each team plays the others this many times at home, best of this many.</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("seasons/{seasonID:int}/matches/generate", Name = "CreateSeasonMatches")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> CreateSeasonMatches([FromRoute] int seasonID, [FromQuery] int homeMatchesPerTeam, [FromQuery] int bestOf, CancellationToken ct)
    {
        if (homeMatchesPerTeam <= 0)
            return this.BadRequestProblem("homeMatchesPerTeam should be at least 1");
        if (bestOf <= 0)
            return this.BadRequestProblem("bestOf should be at least 1");

        await _matchPlannerService.CreateSeasonMatches(seasonID: seasonID, homeMatchesPerTeam: homeMatchesPerTeam, bestOf: bestOf, ct);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/matches/unscheduled", Name = "GetUnscheduledMatches")]
    [ProducesResponseType<List<UnscheduledMatchDto>>(StatusCodes.Status200OK)]
    public Task<List<UnscheduledMatchDto>> GetUnscheduledMatches([FromRoute] int seasonID, CancellationToken ct)
    {
        return _matchPlannerService.GetUnscheduledMatches(seasonID, ct);
    }

    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/matches/scheduled", Name = "GetScheduledMatches")]
    [ProducesResponseType<List<ScheduledMatchDto>>(StatusCodes.Status200OK)]
    public Task<List<ScheduledMatchDto>> GetScheduledMatches([FromRoute] int seasonID, CancellationToken ct)
    {
        return _matchPlannerService.GetScheduledMatches(seasonID, ct);
    }

    /// <summary>Sets a match's time (null: unscheduled again).</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("season-matches/{seasonMatchID:int}/time", Name = "UpdateMatchTime")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> UpdateMatchTime([FromRoute] int seasonMatchID, [FromBody] UpdateMatchTimeDto dto, CancellationToken ct)
    {
        dto.SeasonMatchID = seasonMatchID;
        await _matchPlannerService.UpdateMatchTime(dto, ct);
        return NoContent();
    }
}
