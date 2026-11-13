using GrifballWebApp.Server.Historical;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Historical;

namespace GrifballWebApp.Server.Test;

/// <summary>HistoricalImportControllerTests's reflection and signal unit tests: no database, host, file or network.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportControllerAttributeTests
{
    private static readonly string[] SysadminOnly = ["Sysadmin"];

    [TestCase(nameof(HistoricalImportController.Import))]
    [TestCase(nameof(HistoricalImportController.Job))]
    [TestCase(nameof(HistoricalImportController.Sources))]
    [TestCase(nameof(HistoricalImportController.SetSourceEnabled))]
    [TestCase(nameof(HistoricalImportController.Refresh))]
    [TestCase(nameof(HistoricalImportController.Preview))]
    [TestCase(nameof(HistoricalImportController.ApplyPreview))]
    [TestCase(nameof(HistoricalImportController.ApproveReview))]
    [TestCase(nameof(HistoricalImportController.SkipReview))]
    [TestCase(nameof(HistoricalImportController.Jobs))]
    [TestCase(nameof(HistoricalImportController.Schedule))]
    [TestCase(nameof(HistoricalImportController.SetSchedulePaused))]
    public void EveryAction_IsSysadminOnly(string action)
    {
        var method = typeof(HistoricalImportController).GetMethod(action)!;
        var roles = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Select(a => a.Roles).ToList();
        Assert.That(roles, Is.EqualTo(SysadminOnly), "Approving a claim merges users, which only a sysadmin does");
    }

    [Test]
    public void TheClaimsController_IsSysadminOnly()
    {
        var roles = typeof(GamertagClaimsController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Select(a => a.Roles).ToList();
        Assert.That(roles, Is.EqualTo(SysadminOnly), "Approving a claim merges users, which only a sysadmin does");
    }

    [Test]
    public async Task TheSignal_WakesOneWaiter_AndDoesntPileUp()
    {
        var signal = new HistoricalImportSignal();
        signal.Notify();
        signal.Notify();
        Assert.That(await signal.WaitAsync(TimeSpan.Zero, CancellationToken.None), Is.True);
        Assert.That(await signal.WaitAsync(TimeSpan.Zero, CancellationToken.None), Is.False);
    }
}

/// <summary>HistoricalImportControllerTests's unit tests: a real GrifballContext (temp-file SQLite, #402) for
/// HistoricalImportJobs, which needs nothing else. Its other tests stay in GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportControllerUnitTests : SqliteGrifballContextFixture
{
    // Refused before HistoricalSourceService (the archive's own HTTP fetcher) is ever asked
    private HistoricalImportController Controller() => new(new HistoricalImportJobs(Db, new HistoricalImportSignal()), null!);

    [Test]
    public async Task Jobs_ReturnsNone_WhenThereAreNoImportJobs()
    {
        var result = await Controller().Jobs(CancellationToken.None);

        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public async Task Job_RepliesNotFound_ForANonExistentJob()
    {
        var result = await Controller().Job(999, CancellationToken.None);

        Assert.That(result.Result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task Refresh_QueuesAJob()
    {
        var result = await Controller().Refresh(CancellationToken.None);

        var jobID = ((HistoricalImportQueued)((AcceptedAtActionResult)result).Value!).JobID;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControllerTestHelpers.StatusOf(result), Is.EqualTo(StatusCodes.Status202Accepted));
            var kind = await Db.HistoricalImportJobs.Where(j => j.HistoricalImportJobID == jobID).Select(j => j.Kind).SingleAsync();
            Assert.That(kind, Is.EqualTo(GrifballWebApp.Domain.Archive.HistoricalImportJobKind.Refresh));
        }
    }

    [Test]
    public async Task Preview_QueuesAJob()
    {
        var result = await Controller().Preview(CancellationToken.None);

        var jobID = ((HistoricalImportQueued)((AcceptedAtActionResult)result).Value!).JobID;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControllerTestHelpers.StatusOf(result), Is.EqualTo(StatusCodes.Status202Accepted));
            var kind = await Db.HistoricalImportJobs.Where(j => j.HistoricalImportJobID == jobID).Select(j => j.Kind).SingleAsync();
            Assert.That(kind, Is.EqualTo(GrifballWebApp.Domain.Archive.HistoricalImportJobKind.Preview));
        }
    }

    [Test]
    public async Task ApplyPreview_RepliesNotFound_ForANonExistentJob()
    {
        var result = await Controller().ApplyPreview(999, CancellationToken.None);

        Assert.That(ControllerTestHelpers.StatusOf(result), Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task ApproveReview_RepliesNotFound_ForANonExistentReview()
    {
        var result = await Controller().ApproveReview(999, CancellationToken.None);

        Assert.That(ControllerTestHelpers.StatusOf(result), Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task SkipReview_RepliesNotFound_ForANonExistentReview()
    {
        var result = await Controller().SkipReview(999, CancellationToken.None);

        Assert.That(ControllerTestHelpers.StatusOf(result), Is.EqualTo(StatusCodes.Status404NotFound));
    }
}
