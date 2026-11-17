using GrifballWebApp.Application.Calendar;
using GrifballWebApp.Server.Seasons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

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
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> TeamCalendar([FromRoute] int teamID, CancellationToken ct)
        => Feed(await _calendars.TeamAsync(teamID, SiteUrl, ct), SeasonVisibilityFilter.NoSuchTeam);

    /// <summary>The scheduled matches of the player's teams, as a calendar to subscribe to. 404 when there is no such user.</summary>
    [AllowAnonymous]
    [HttpGet("users/{userID:int}/calendar.ics", Name = "PlayerCalendar")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, CalendarMediaType)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> PlayerCalendar([FromRoute] int userID, CancellationToken ct)
        => Feed(await _calendars.PlayerAsync(userID, SiteUrl, ct), ApiProblems.NoSuchUser);

    /// <summary>The season's scheduled matches, as a calendar to subscribe to. 404 when there is no such season.</summary>
    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/calendar.ics", Name = "SeasonCalendar")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, CalendarMediaType)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> SeasonCalendar([FromRoute] int seasonID, CancellationToken ct)
        => Feed(await _calendars.SeasonAsync(seasonID, SiteUrl, ct), SeasonVisibilityFilter.NoSuchSeason);

    /// <summary>
    /// The site the feed was asked for from, which its events link to: the request's scheme and host, as the forwarded
    /// headers give them (the API is under /api on the same host). Not BaseUrl, which not every host sets.
    /// </summary>
    private string SiteUrl => $"{Request.Scheme}://{Request.Host}";

    /// <summary>How long a calendar app or a proxy may keep a feed before asking again.</summary>
    public const string CacheControl = "public, max-age=900";

    /// <summary>
    /// The calendar as text/calendar, or a 404 with <paramref name="notFound"/> for none. The same matches give the same bytes,
    /// so their hash is its ETag: a poll that sends it back for an unchanged feed gets a 304, not the feed again.
    /// </summary>
    private IActionResult Feed(string? ics, string notFound)
    {
        if (ics is null)
            return this.NotFoundProblem(notFound);
        var etag = $"\"{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ics)))[..32]}\"";
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = CacheControl;
        if (Request.Headers.IfNoneMatch.ToString().Split(',').Any(tag => tag.Trim() == etag))
            return StatusCode(StatusCodes.Status304NotModified);
        return Content(ics, IcsWriter.ContentType);
    }
}
