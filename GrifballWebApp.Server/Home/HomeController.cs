using Microsoft.AspNetCore.Authorization;
using GrifballWebApp.Application.Home;
using GrifballWebApp.Application.Seasons;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Seasons;

[ApiController]
public class HomeController : ControllerBase
{
    private readonly HomeEventsService _events;

    public HomeController(HomeEventsService events)
    {
        _events = events;
    }

    /// <summary>Current and upcoming signups, drafts and seasons, for the home page's calendar.</summary>
    [AllowAnonymous]
    [HttpGet("events", Name = "CurrentAndFutureEvents")]
    [ProducesResponseType<Event[]>(StatusCodes.Status200OK)]
    public Task<Event[]> CurrentAndFutureEvents(CancellationToken ct) => _events.CurrentAndFuture(SeasonVisibility.For(User), ct);
}
