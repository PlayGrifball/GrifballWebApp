using GrifballWebApp.Server.Reschedule;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using System.Reflection;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Application.Reschedule;

namespace GrifballWebApp.Server.Test;

/// <summary>RescheduleControllerTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RescheduleControllerUnitTests
{
    [Test]
    public void Authorization_IsConfigured()
    {
        var type = typeof(RescheduleController);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControllerTestHelpers.ClassAuthorize(type), Is.Not.Null, "controller requires authentication");
            Assert.That(ControllerTestHelpers.ActionAuthorize(type, nameof(RescheduleController.RequestReschedule)), Is.Null, "any logged in user can request");
        }
        foreach (var name in new[] { nameof(RescheduleController.ProcessReschedule), nameof(RescheduleController.GetPendingReschedules), nameof(RescheduleController.CreateDiscordThread) })
        {
            Assert.That(ControllerTestHelpers.ActionAuthorize(type, name)?.Roles, Is.EqualTo("Commissioner,Sysadmin"), name);
        }
    }
}
