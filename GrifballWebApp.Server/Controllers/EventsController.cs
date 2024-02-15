using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Controllers;

[Route("api/[controller]/[action]")]
[ApiController]
public class EventsController : ControllerBase
{
    private readonly ILogger<EventsController> _logger;
    private readonly GrifballContext _context;

    public EventsController(ILogger<EventsController> logger, GrifballContext grifballContext)
    {
        _logger = logger;
        _context = grifballContext;
    }

    [HttpGet(Name = "GetSeasons")]
    public async Task<IActionResult> GetSeasons()
    {
        return Ok(await _context.Seasons.ToListAsync());
    }

    [HttpGet(Name = "CreateSeason")]
    public async Task<ActionResult<int>> CreateSeason([FromQuery] string name)
    {
        if (name == null)
        {
            return BadRequest("You must provide name");
        }

        var season = new Season()
        {
            SeasonName = name,
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        return Ok(season.SeasonID);
    }

    [HttpGet(Name = "CreateTeam")]
    public async Task<ActionResult<int>> CreateTeam([FromQuery] string name)
    {
        if (name == null)
        {
            return BadRequest("You must provide name");
        }

        var team = new Team()
        {
            TeamName = name,
        };

        await _context.Teams.AddAsync(team);
        await _context.SaveChangesAsync();

        return Ok(team.TeamID);
    }
}
