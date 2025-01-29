using GrifballWebApp.Database;
using GrifballWebApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Controllers;

[Route("[controller]/[action]")]
[ApiController]
public class SeedController : ControllerBase
{
    private readonly ILogger<SeedController> _logger;
    private readonly GrifballContext _context;
    private readonly DataPullService _dataPullService;

    public SeedController(ILogger<SeedController> logger, GrifballContext grifballContext, DataPullService dataPullService)
    {
        _logger = logger;
        _context = grifballContext;
        _dataPullService = dataPullService;
    }

    [Authorize(Roles = "Sysadmin")]
    [HttpGet(Name = "Medals")]
    public async Task Medals()
    {
        await _dataPullService.DownloadMedals();
    }
}
