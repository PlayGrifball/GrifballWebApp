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
    private readonly ExcelService _e;

    public SeedController(ILogger<SeedController> logger, GrifballContext grifballContext, DataPullService dataPullService, ExcelService e)
    {
        _logger = logger;
        _context = grifballContext;
        _dataPullService = dataPullService;
        _e = e;
    }

    [Authorize(Roles = "Sysadmin")]
    [HttpGet(Name = "Medals")]
    public async Task Medals()
    {
        //_bracketService.CreateBracket(5, 1);
        //await _bracketService.CreateBracket(8, 1, true);
        //var b = _bracketService.GetSeedMatchUps(16);
        await _dataPullService.DownloadMedals();
    }

    [Authorize(Roles = "Sysadmin")]
    [HttpGet(Name = "Excel")]
    public async Task Excel()
    {
        //_bracketService.CreateBracket(5, 1);
        //await _bracketService.CreateBracket(8, 1, true);
        //var b = _bracketService.GetSeedMatchUps(16);
        _e.CreateExcelPerMatch();
    }
}
