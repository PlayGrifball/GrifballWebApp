using GrifballWebApp.Application.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GrifballWebApp.Server.Scheduling;

/// <summary>A team's own timeslots, from its season's, in place of its roster's.</summary>
public sealed class TeamAvailabilityDto
{
    public required List<int> AvailabilityOptionIDs { get; init; }
}

/// <summary>
/// The schedule helper: a match's time checked against its season's rules and the season's timeslots suggested for it; a
/// team's timeslots, worked out from its roster unless its captain (or staff) set them; a season's scheduling rules.
/// </summary>
[ApiController]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
public class SchedulingController(SchedulingService scheduling, TeamAvailabilityService teams, SeasonSchedulingRulesService rules) : ControllerBase
{
    private const string ProblemJson = "application/problem+json";
    private const string NoSuchMatch = "There's no such match with both its teams.";

    /// <summary>The most time suggestions one call gives.</summary>
    public const int MaxSuggestions = 20;

    /// <summary>How many suggestions to give for <paramref name="count"/> asked for: 1 to <see cref="MaxSuggestions"/>.</summary>
    public static int HowMany(int count) => Math.Clamp(count, 1, MaxSuggestions);

    /// <summary>
    /// The season's timeslots for a match, best first (no warnings, then the most players able to play, then the soonest), from
    /// now (or <paramref name="from"/>) to the season's end (or before <paramref name="to"/>); a time a team already plays, or
    /// outside the season, isn't among them.
    /// </summary>
    [HttpGet("season-matches/{seasonMatchID:int}/time-suggestions", Name = "GetTimeSuggestions")]
    [ProducesResponseType<List<TimeSuggestion>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> GetTimeSuggestions([FromRoute] int seasonMatchID, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int count = SchedulingService.DefaultSuggestions, CancellationToken ct = default)
        => await scheduling.SuggestAsync(seasonMatchID, from, to, HowMany(count), ct) is { } suggestions
            ? Ok(suggestions)
            : this.NotFoundProblem(NoSuchMatch);

    /// <summary>What is wrong with a match at <paramref name="at"/> (UTC): empty when nothing is.</summary>
    [HttpGet("season-matches/{seasonMatchID:int}/time-check", Name = "CheckMatchTime")]
    [ProducesResponseType<List<ScheduleConflict>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> CheckTime([FromRoute] int seasonMatchID, [FromQuery] DateTime at, CancellationToken ct)
        => await scheduling.CheckAsync(seasonMatchID, at, ct) is { } conflicts
            ? Ok(conflicts)
            : this.NotFoundProblem(NoSuchMatch);

    /// <summary>A team's timeslots, each of its season's: whether it's the team's, and how many of its roster can play it.</summary>
    [HttpGet("teams/{teamID:int}/availability", Name = "GetTeamAvailability")]
    [ProducesResponseType<TeamSlots>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> GetTeamAvailability([FromRoute] int teamID, CancellationToken ct)
        => await teams.ForTeamAsync(teamID, ct) is { } slots
            ? Ok(slots)
            : this.NotFoundProblem(TeamAvailabilityService.NoSuchTeam);

    /// <summary>The team's captain (or staff) set its own timeslots, from its season's, in place of its roster's.</summary>
    [HttpPost("teams/{teamID:int}/availability", Name = "SetTeamAvailability")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> SetTeamAvailability([FromRoute] int teamID, [FromBody] TeamAvailabilityDto dto, CancellationToken ct)
        => Answer(await teams.SetAsync(teamID, dto.AvailabilityOptionIDs, UserID, Staff, ct));

    /// <summary>The team's captain (or staff) reset its timeslots to its roster's.</summary>
    [HttpPost("teams/{teamID:int}/availability/reset", Name = "ResetTeamAvailability")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> ResetTeamAvailability([FromRoute] int teamID, CancellationToken ct)
        => Answer(await teams.ResetAsync(teamID, UserID, Staff, ct));

    /// <summary>A season's scheduling rules: the league's defaults until staff change them.</summary>
    [HttpGet("seasons/{seasonID:int}/scheduling-rules", Name = "GetSchedulingRules")]
    [ProducesResponseType<SeasonSchedulingRulesDto>(StatusCodes.Status200OK)]
    public Task<SeasonSchedulingRulesDto> GetSchedulingRules([FromRoute] int seasonID, CancellationToken ct)
        => rules.GetAsync(seasonID, ct);

    /// <summary>Staff change a season's scheduling rules.</summary>
    [HttpPost("seasons/{seasonID:int}/scheduling-rules", Name = "ChangeSchedulingRules")]
    [Authorize(Roles = "Commissioner,Sysadmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> ChangeSchedulingRules([FromRoute] int seasonID, [FromBody] SeasonSchedulingRulesDto dto, CancellationToken ct)
        => Answer(await rules.ChangeAsync(seasonID, dto, ct));

    private int UserID => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool Staff => User.IsInRole("Commissioner") || User.IsInRole("Sysadmin");

    /// <summary>A service's refusal as its problem (no such one: 404, not theirs to change: 403, the rest 400), else 204.</summary>
    private IActionResult Answer(string? refusal) => refusal switch
    {
        null => NoContent(),
        TeamAvailabilityService.NoSuchTeam or SeasonSchedulingRulesService.NoSuchSeason => this.NotFoundProblem(refusal),
        TeamAvailabilityService.NotTheCaptain => this.ForbiddenProblem(refusal),
        _ => this.BadRequestProblem(refusal),
    };
}
