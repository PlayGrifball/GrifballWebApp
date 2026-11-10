using GrifballWebApp.Server.Matchmaking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using GrifballWebApp.Application.Matchmaking;

namespace GrifballWebApp.Server.Test;

/// <summary>QueueControllerTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QueueControllerUnitTests
{
    [Test]
    public void JoinAndLeave_NeedASignIn_StatusDoesNot()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControllerTestHelpers.ClassAuthorize(typeof(QueueController)), Is.Null);
            Assert.That(ControllerTestHelpers.ActionAuthorize(typeof(QueueController), nameof(QueueController.Status)), Is.Null);
            Assert.That(ControllerTestHelpers.ActionAuthorize(typeof(QueueController), nameof(QueueController.Join)), Is.Not.Null);
            Assert.That(ControllerTestHelpers.ActionAuthorize(typeof(QueueController), nameof(QueueController.Leave)), Is.Not.Null);
        }
    }
}
