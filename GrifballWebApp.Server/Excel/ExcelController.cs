using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Excel;

[Route("[controller]/[action]")]
[ApiController]
public class ExcelController : ControllerBase
{
    private readonly ExcelService _e;
    public ExcelController(ExcelService e)
    {
        _e = e;
    }

    [Authorize(Roles = "EventOrganizer,Sysadmin")]
    [HttpGet(Name = "DefaultSheetInfo")]
    public IActionResult DefaultSheetInfo()
    {
        return Ok(_e.GetDefaultInfo());
    }

    [Authorize(Roles = "EventOrganizer,Sysadmin")]
    [HttpPost(Name = "ExportAll")]
    public async Task<IActionResult> ExportAll([FromBody] SheetInfo sheetInfo)
    {
        await _e.ExportAll(sheetInfo);
        return Ok();
    }

    [Authorize(Roles = "EventOrganizer,Sysadmin")]
    [HttpPost(Name = "AppendMatch")]
    public async Task<IActionResult> AppendMatch([FromBody] SheetInfo sheetInfo, [FromQuery] Guid matchID)
    {
        await _e.AppendMatch(sheetInfo, matchID);
        return Ok();
    }
}
