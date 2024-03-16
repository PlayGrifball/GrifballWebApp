using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Controllers;

[Route("api/[controller]/[action]")]
[ApiController]
public class EventsController : ControllerBase
{
    private readonly ILogger<EventsController> _logger;
    private readonly GrifballContext _context;
    private readonly BracketService _bracketService;

    public EventsController(ILogger<EventsController> logger, GrifballContext grifballContext, BracketService bracketService)
    {
        _logger = logger;
        _context = grifballContext;
        _bracketService = bracketService;
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

    [HttpGet(Name = "CreateBracket")]
    public async Task<ActionResult> CreateBracket([FromQuery] int participantsCount, [FromQuery] int seasonID, [FromQuery] bool doubleElimination)
    {
        if (participantsCount <= 0)
            return BadRequest("Please provide participantsCount");
        if (seasonID <= 0)
            return BadRequest("Please provide seasonID");

        await _bracketService.CreateBracket(participantsCount, seasonID, doubleElimination);
        return Ok();
    }

    [HttpGet(Name = "GetBracket")]
    public async Task<ActionResult<BracketDto>> GetBracket([FromQuery] int seasonID)
    {
        if (seasonID <= 0)
            return BadRequest("Please provide seasonID");

        var bracketDto = await _bracketService.GetBracketsAsync(seasonID);
        return Ok(bracketDto);
    }
}
