using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Infrastructure.Jobs;

namespace GrifballWebApp.Server.Excel;

[ApiController]
[Authorize(Roles = "Commissioner,Sysadmin")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public class ExcelController : ControllerBase
{
    private readonly ExcelService _e;
    public ExcelController(ExcelService e)
    {
        _e = e;
    }

    [HttpGet("sheets/default", Name = "DefaultSheetInfo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult DefaultSheetInfo()
    {
        return Ok(_e.GetDefaultInfo());
    }

    /// <summary>
    /// Queues the export of every match in the copy sheet to <paramref name="sheetInfo"/> (<see cref="SheetExportJob"/>,
    /// on the worker); nothing more while one is queued or running. Returns where it is.
    /// </summary>
    [HttpPost("sheets/export", Name = "ExportAll")]
    [ProducesResponseType<OnDemandJobStatusDto>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<OnDemandJobStatusDto>> ExportAll([FromBody] SheetInfo sheetInfo, [FromServices] OnDemandJobs jobs, CancellationToken ct)
    {
        // Runs as whoever queued it, never as someone the request names
        sheetInfo.RequestedByUserID = JobUser.IdOf(User);
        return Accepted(await jobs.Enqueue<SheetExportJob, SheetInfo>(SheetExportJob.RunName, sheetInfo, sheetInfo.SheetName, ct: ct));
    }

    /// <summary>Where the export of every match is.</summary>
    [HttpGet("sheets/export", Name = "ExportAllStatus")]
    [ProducesResponseType<OnDemandJobStatusDto>(StatusCodes.Status200OK)]
    public Task<OnDemandJobStatusDto> ExportAllStatus([FromServices] OnDemandJobs jobs, CancellationToken ct)
        => jobs.Status<SheetExportJob>(SheetExportJob.RunName, ct);

    [HttpPost("matches/{matchID:guid}/append-to-sheet", Name = "AppendMatch")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AppendMatch([FromRoute] Guid matchID, [FromBody] SheetInfo sheetInfo)
    {
        await _e.AppendMatch(sheetInfo, matchID);
        return NoContent();
    }
}
