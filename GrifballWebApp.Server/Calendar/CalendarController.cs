using GrifballWebApp.Application.Calendar;
using GrifballWebApp.Server.Seasons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Calendar;

/// <summary>
/// The match calendars (iCalendar, RFC 5545) that calendar apps subscribe to by URL: a team's, a player's and a season's
/// (<see cref="MatchCalendarService"/>). A calendar app sends no sign-in, so each is open to anyone and holds only what a
/// signed-out visitor may see, whoever asks: a team or season of a season not announced yet is 404 to staff too, as one
/// that doesn't exist.
/// </summary>
[ApiController]
public class CalendarController : ControllerBase
{
    /// <summary>The 404's reason for a player there is none of.</summary>
    public const string NoSuchUser = "There is no such user";

    private const string CalendarMediaType = "text/calendar";

    private readonly MatchCalendarService _calendars;

    public CalendarController(MatchCalendarService calendars)
    {
        _calendars = calendars;
    }

    /// <summary>The team's scheduled matches, as a calendar to subscribe to. 404 when there is no such team.</summary>
    [AllowAnonymous]
    [HttpGet("teams/{teamID:int}/calendar.ics", Name = "TeamCalendar")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, CalendarMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> TeamCalendar([FromRoute] int teamID, CancellationToken ct)
        => Feed(await _calendars.TeamAsync(teamID, ct), SeasonVisibilityFilter.NoSuchTeam);

    /// <summary>The scheduled matches of the player's teams, as a calendar to subscribe to. 404 when there is no such user.</summary>
    [AllowAnonymous]
    [HttpGet("users/{userID:int}/calendar.ics", Name = "PlayerCalendar")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, CalendarMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> PlayerCalendar([FromRoute] int userID, CancellationToken ct)
        => Feed(await _calendars.PlayerAsync(userID, ct), NoSuchUser);

    /// <summary>The season's scheduled matches, as a calendar to subscribe to. 404 when there is no such season.</summary>
    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/calendar.ics", Name = "SeasonCalendar")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, CalendarMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> SeasonCalendar([FromRoute] int seasonID, CancellationToken ct)
        => Feed(await _calendars.SeasonAsync(seasonID, ct), SeasonVisibilityFilter.NoSuchSeason);

    /// <summary>The calendar as text/calendar, or a 404 with <paramref name="notFound"/> for none.</summary>
    private IActionResult Feed(string? ics, string notFound)
        => ics is null ? this.NotFoundProblem(notFound) : Content(ics, IcsWriter.ContentType);
}
