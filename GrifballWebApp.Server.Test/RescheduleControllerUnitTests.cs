using GrifballWebApp.Server.Reschedule;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Application.Outbox;
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

/// <summary>RescheduleControllerTests's database-backed unit tests: a real GrifballContext (temp-file SQLite, #402)
/// for RescheduleService, which needs nothing else (IOutbox substituted, never reached by these "not found" cases).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RescheduleControllerDatabaseUnitTests : SqliteGrifballContextFixture
{
    private RescheduleController Controller() => new(new RescheduleService(Db, Substitute.For<IOutbox>()));

    [Test]
    public async Task RequestReschedule_RepliesNotFound_ForANonExistentMatch()
    {
        var controller = Controller().WithUser("5");

        var result = await controller.RequestReschedule(999, new RescheduleRequestDto { Reason = "Can't make it" }, CancellationToken.None);

        result.AssertProblem(StatusCodes.Status404NotFound, "Season match not found");
    }

    [Test]
    public async Task ProcessReschedule_RepliesNotFound_ForANonExistentRequest()
    {
        var controller = Controller().WithUser("5");

        var result = await controller.ProcessReschedule(999, new ProcessRescheduleDto { Approved = true }, CancellationToken.None);

        result.AssertProblem(StatusCodes.Status404NotFound, "Reschedule request not found");
    }

    [Test]
    public async Task CreateDiscordThread_RepliesNotFound_ForANonExistentRequest()
    {
        var controller = Controller().WithUser("5");

        var result = await controller.CreateDiscordThread(999, CancellationToken.None);

        result.AssertProblem(StatusCodes.Status404NotFound, "Reschedule request not found");
    }

    [Test]
    public async Task GetPendingReschedules_ReturnsNone_WhenThereAreNoReschedules()
    {
        var result = await Controller().GetPendingReschedules(CancellationToken.None);

        Assert.That(result, Is.Empty);
    }
}
