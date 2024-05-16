using GrifballWebApp.Database;
using GrifballWebApp.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models.HaloInfinite;
using Surprenant.Grunt.Util;

namespace GrifballWebApp.Server.Controllers;
[Route("[controller]/[action]")]
[ApiController]
public class AdminController : ControllerBase
{
    private readonly ILogger<AdminController> _logger;
    private readonly HaloInfiniteClientFactory _haloInfiniteClientFactory;
    private readonly IStateSeed _stateSeed;
    private readonly IAccountAuthorization _accountAuthorization;
    private readonly GrifballContext _context;
    private readonly DataPullService _dataPullService;

    public AdminController(ILogger<AdminController> logger, HaloInfiniteClientFactory haloInfiniteClientFactory,
        IAccountAuthorization accountAuthorization, IStateSeed stateSeed, GrifballContext grifballContext, DataPullService dataPullService)
    {
        _logger = logger;
        _haloInfiniteClientFactory = haloInfiniteClientFactory;
        _accountAuthorization = accountAuthorization;
        _stateSeed = stateSeed;
        _context = grifballContext;
        _dataPullService = dataPullService;
    }

    

    [HttpGet(Name = "MatchStats")]
    public async Task Get([FromQuery] string game)
    {
        if (!Guid.TryParse(game, out var guid))
        {
            throw new Exception("Not valid guid");
        }
        await _dataPullService.GetAndSaveMatch(guid);
        //var foo = new GrifballWebApp.Database.Models.XboxUser();
        ////foo.
        //var c = await _haloInfiniteClientFactory.CreateAsync();

        //var response = await c.StatsGetMatchStats("1fde4c2a-7935-4fb0-9706-e226f4d13683");

        //var playerIDs = response.Result.Players.Select(x => x.PlayerId.Replace("xuid(", "").Replace(")", "")).ToList();
        //var users = await c.Users(playerIDs);

        //return response.Result;
    }

    [HttpGet(Name = "SetCode")]
    public async Task<ActionResult> SetCode([FromQuery] string code, [FromQuery] string state)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Missing code");

        if (string.IsNullOrWhiteSpace(state))
            return BadRequest("Missing state");

        if (state != _stateSeed.State.ToString())
            return BadRequest("Invalid state");

        await _accountAuthorization.SetCodeAsync(code);

        return Ok("Code saved");
    }
}
