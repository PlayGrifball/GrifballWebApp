using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.HaloInfinite;

namespace GrifballWebApp.Server.Controllers;

[ApiController]
public class SeedController(IDataPullService dataPullService) : ControllerBase
{
    /// <summary>Downloads Halo Infinite's medals again.</summary>
    [Authorize(Roles = "Sysadmin")]
    [HttpPost("medals/refresh", Name = "Medals")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Medals()
    {
        await dataPullService.DownloadMedals();
        return NoContent();
    }
}
