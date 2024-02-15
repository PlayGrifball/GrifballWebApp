using GrifballWebApp.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Controllers;

[Route("api/[controller]/[action]")]
[ApiController]
public class StatsController : ControllerBase
{
    private readonly ILogger<StatsController> _logger;
    private readonly GrifballContext _context;

    public StatsController(ILogger<StatsController> logger, GrifballContext grifballContext)
    {
        _logger = logger;
        _context = grifballContext;
    }

    [HttpGet(Name = "TopKills")]
    public async Task<IActionResult> TopKills()
    {
        var players = await _context.XboxUsers
            .Select(x => new
            {
                x.Gamertag,
                Kills = x.MatchParticipants.Select(x => x.Kills).Sum(),
            })
            .Where(x => x.Kills > 0)
            .OrderByDescending(x => x.Kills)
            .Take(10)
            .ToListAsync();
        return Ok(players.Select((x, i) =>
        {
            return new
            {
                Rank = i + 1,
                Gamertag = x.Gamertag,
                Kills = x.Kills,
            };
        }));
    }

}
