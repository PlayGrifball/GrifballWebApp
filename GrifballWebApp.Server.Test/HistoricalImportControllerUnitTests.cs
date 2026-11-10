using GrifballWebApp.Server.Historical;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text;
using GrifballWebApp.Application.Historical;

namespace GrifballWebApp.Server.Test;

/// <summary>HistoricalImportControllerTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportControllerUnitTests
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
