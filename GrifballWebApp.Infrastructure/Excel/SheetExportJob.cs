using Quartz;

namespace GrifballWebApp.Infrastructure.Excel;

/// <summary>
/// Writes every match listed in the copy sheet to a sheet (<see cref="ExcelService.ExportAll"/>), downloading the ones
/// the database doesn't have yet: one Halo call a match, so minutes for a season. Started from the Excel page
/// (<see cref="OnDemandJobs"/>); one export at a time.
/// </summary>
[DisallowConcurrentExecution]
public sealed class SheetExportJob : IJob<SheetInfo>
{
    public const string Description = "Writes every match in the copy sheet to a Google sheet (from the Excel page)";
    /// <summary>The run's name: one export queued or running at a time, whichever sheet it writes.</summary>
    public const string RunName = "export-all";

    private readonly ExcelService _excel;

    public SheetExportJob(ExcelService excel)
    {
        _excel = excel;
    }

    public async ValueTask Execute(IJobExecutionContext context, SheetInfo input, CancellationToken cancellationToken = default)
    {
        context.ReportProgress(0, $"Exporting to {input.SheetName}");
        await _excel.ExportAll(input, cancellationToken);
    }
}
