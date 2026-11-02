using Microsoft.AspNetCore.Authorization;
using GrifballWebApp.Server.Jobs;
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

    [Authorize(Roles = "Commissioner,Sysadmin")]
    [HttpGet(Name = "DefaultSheetInfo")]
    public IActionResult DefaultSheetInfo()
    {
        return Ok(_e.GetDefaultInfo());
    }

    /// <summary>
    /// Queues the export of every match in the copy sheet to <paramref name="sheetInfo"/> (<see cref="SheetExportJob"/>,
    /// on the worker); nothing more while one is queued or running. Returns where it is.
    /// </summary>
    [Authorize(Roles = "Commissioner,Sysadmin")]
    [HttpPost(Name = "ExportAll")]
    public Task<OnDemandJobStatusDto> ExportAll([FromBody] SheetInfo sheetInfo, [FromServices] OnDemandJobs jobs, CancellationToken ct)
        => jobs.Enqueue<SheetExportJob, SheetInfo>(SheetExportJob.RunName, sheetInfo, sheetInfo.SheetName, ct: ct);

    /// <summary>Where the export of every match is.</summary>
    [Authorize(Roles = "Commissioner,Sysadmin")]
    [HttpGet(Name = "ExportAllStatus")]
    public Task<OnDemandJobStatusDto> ExportAllStatus([FromServices] OnDemandJobs jobs, CancellationToken ct)
        => jobs.Status<SheetExportJob>(SheetExportJob.RunName, ct);

    [Authorize(Roles = "Commissioner,Sysadmin")]
    [HttpPost(Name = "AppendMatch")]
    public async Task<IActionResult> AppendMatch([FromBody] SheetInfo sheetInfo, [FromQuery] Guid matchID)
    {
        await _e.AppendMatch(sheetInfo, matchID);
        return Ok();
    }
}
