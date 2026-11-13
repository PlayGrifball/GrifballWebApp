using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Server.Excel;
using Microsoft.AspNetCore.Mvc;
using Quartz;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// ExcelController's export of every match, queued on an in-memory Quartz (<see cref="InMemoryQuartz"/>): once at a time,
/// nothing written until the worker runs it (ExcelService is never asked, so it stays null). And SheetDto both ways.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ExcelControllerExportUnitTests
{
    private InMemoryQuartz? _quartz;

    [TearDown]
    public async Task TearDownQuartz()
    {
        if (_quartz is not null)
            await _quartz.DisposeAsync();
    }

    private static SheetDto Target() => new() { Name = "Season 9", SpreadsheetID = "sheet-1", SheetName = "Matches" };

    [Test]
    public async Task ExportAll_QueuesTheExport_OnceAtATime()
    {
        _quartz = await InMemoryQuartz.Create();
        var controller = new ExcelController(null!).WithUser("7", "commish", "Commissioner");

        var idle = await controller.ExportAllStatus(_quartz.OnDemand, CancellationToken.None);
        var queued = await controller.ExportAll(Target(), _quartz.OnDemand, CancellationToken.None);
        var again = await controller.ExportAll(Target(), _quartz.OnDemand, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(idle.State, Is.EqualTo(OnDemandJobState.Idle));
            Assert.That(((OnDemandJobStatusDto)((AcceptedResult)queued.Result!).Value!).State, Is.EqualTo(OnDemandJobState.Queued));
            Assert.That(((OnDemandJobStatusDto)((AcceptedResult)again.Result!).Value!).State, Is.EqualTo(OnDemandJobState.Queued));
            Assert.That((await controller.ExportAllStatus(_quartz.OnDemand, CancellationToken.None)).State, Is.EqualTo(OnDemandJobState.Queued));
            Assert.That(await _quartz.Scheduler.GetTriggersOfJob(OnDemandJobs.JobKeyOf(typeof(SheetExportJob))), Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void SheetDto_RoundTripsASheet_WithWhoAskedFromTheServer()
    {
        var sheet = new SheetInfo { Name = "Season 9", SpreadsheetID = "sheet-1", SheetName = "Matches", RequestedByUserID = 99 };

        var info = SheetDto.From(sheet).ToInfo(requestedByUserID: 7);

        Assert.That((info.Name, info.SpreadsheetID, info.SheetName, info.RequestedByUserID), Is.EqualTo(("Season 9", "sheet-1", "Matches", (int?)7)));
    }
}
