using GrifballWebApp.Server.Matchmaking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using GrifballWebApp.Application.Matchmaking;

namespace GrifballWebApp.Server.Test;

/// <summary>QueueControllerTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QueueControllerUnitTests
{
    // Refused before the queue is asked
    private readonly QueueController _controller = new(null!, null!, new ConfigurationBuilder().Build());

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

    [Test]
    public async Task JoinAndLeave_WithoutAUser_AreUnauthorized()
    {
        _controller.WithAnonymousUser();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await _controller.Join(CancellationToken.None)).Result, Is.TypeOf<UnauthorizedResult>());
            Assert.That((await _controller.Leave(CancellationToken.None)).Result, Is.TypeOf<UnauthorizedResult>());
        }
    }
}
