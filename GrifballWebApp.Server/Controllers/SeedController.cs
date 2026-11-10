using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.HaloInfinite;

namespace GrifballWebApp.Server.Controllers;

[Route("[controller]/[action]")]
[ApiController]
public class SeedController(IDataPullService dataPullService) : ControllerBase
{
    [Authorize(Roles = "Sysadmin")]
    [HttpPost(Name = "Medals")]
    public async Task Medals()
    {
        await dataPullService.DownloadMedals();
    }
}
