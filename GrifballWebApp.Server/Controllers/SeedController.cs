using GrifballWebApp.Application.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.HaloInfinite;

namespace GrifballWebApp.Server.Controllers;

[Route("[controller]/[action]")]
[ApiController]
public class SeedController : ControllerBase
{
    private readonly ILogger<SeedController> _logger;
    private readonly IApplicationDbContext _context;
    private readonly IDataPullService _dataPullService;

    public SeedController(ILogger<SeedController> logger, IApplicationDbContext grifballContext, IDataPullService dataPullService)
    {
        _logger = logger;
        _context = grifballContext;
        _dataPullService = dataPullService;
    }

    [Authorize(Roles = "Sysadmin")]
    [HttpPost(Name = "Medals")]
    public async Task Medals()
    {
        await _dataPullService.DownloadMedals();
    }
}
