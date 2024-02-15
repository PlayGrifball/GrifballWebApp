using GrifballWebApp.Database;
using GrifballWebApp.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Controllers;

[Route("api/[controller]/[action]")]
[ApiController]
public class SeedController : ControllerBase
{
    private readonly ILogger<SeedController> _logger;
    private readonly GrifballContext _context;
    private readonly DataPullService _dataPullService;
    private readonly EventsController _eventsController;

    public SeedController(ILogger<SeedController> logger, GrifballContext grifballContext, DataPullService dataPullService)
    {
        _logger = logger;
        _context = grifballContext;
        _dataPullService = dataPullService;
    }

    [HttpGet(Name = "Medals")]
    public async Task Medals()
    {
        await _dataPullService.DownloadMedals();
    }
}
