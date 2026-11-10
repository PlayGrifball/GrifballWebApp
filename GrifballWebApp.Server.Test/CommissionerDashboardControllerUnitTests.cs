using GrifballWebApp.Server.Commissioner;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Application.Commissioner;
using GrifballWebApp.Application.Reschedule;

namespace GrifballWebApp.Server.Test;

/// <summary>CommissionerDashboardControllerTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
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
