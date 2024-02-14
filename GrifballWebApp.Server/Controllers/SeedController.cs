using GrifballWebApp.Database;
using GrifballWebApp.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Util;

namespace GrifballWebApp.Server.Controllers;

[Route("[controller]/[action]")]
[ApiController]
public class SeedController : ControllerBase
{
    private readonly ILogger<SeedController> _logger;
    private readonly GrifballContext _context;
    private readonly DataPullService _dataPullService;
    private readonly EventsController _eventsController;

    public SeedController(ILogger<SeedController> logger, GrifballContext grifballContext, DataPullService dataPullService, EventsController eventsController)
    {
        _logger = logger;
        _context = grifballContext;
        _dataPullService = dataPullService;
        _eventsController = eventsController;
    }

    [HttpGet(Name = "Medals")]
    public async Task Medals()
    {
        await _dataPullService.DownloadMedals();
    }

    [HttpGet(Name = "SeedSeason")]
    public async Task SeedSeason()
    {
        var result = await _eventsController.CreateSeason("FirstSeason");

        var seasonID = result.Value;


    }
}
