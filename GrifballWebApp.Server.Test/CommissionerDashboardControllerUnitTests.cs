using GrifballWebApp.Server.Commissioner;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Commissioner;
using GrifballWebApp.Application.Reschedule;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>CommissionerDashboardControllerTests's unit tests: no database, host, file or network. Its other tests
/// stay in GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class CommissionerDashboardControllerUnitTests
{
    [Test]
    public void Controller_RequiresCommissionerOrSysadmin()
    {
        Assert.That(ControllerTestHelpers.ClassAuthorize(typeof(CommissionerDashboardController))!.Roles, Is.EqualTo("Commissioner,Sysadmin"));
    }
}

/// <summary>CommissionerDashboardControllerTests's database-backed unit tests: a real GrifballContext (temp-file
/// SQLite, #402) for RescheduleService, which needs nothing else for these reads.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class CommissionerDashboardControllerDatabaseUnitTests : SqliteGrifballContextFixture
{
    private CommissionerDashboardController Controller() => new(new RescheduleService(Db, Substitute.For<IOutbox>()));

    [Test]
    public async Task GetDashboardData_ReturnsEmptySummary_WhenThereIsNothingPendingOrOverdue()
    {
        var data = await Controller().GetDashboardData(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data.PendingReschedules, Is.Empty);
            Assert.That(data.OverdueMatches, Is.Empty);
            Assert.That(data.Summary.PendingRescheduleCount, Is.Zero);
            Assert.That(data.Summary.OverdueMatchCount, Is.Zero);
            Assert.That(data.Summary.CriticalOverdueCount, Is.Zero);
        }
    }

    [Test]
    public async Task GetOverdueMatches_ReturnsNone_WhenThereAreNoMatches()
    {
        var result = await Controller().GetOverdueMatches(CancellationToken.None);

        Assert.That(result, Is.Empty);
    }
}
