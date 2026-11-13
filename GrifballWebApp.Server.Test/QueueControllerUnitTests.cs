using Microsoft.AspNetCore.Http;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Domain.LateLeague;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using GrifballWebApp.Application.Matchmaking;
using NSubstitute;

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
            (await _controller.Join(CancellationToken.None)).AssertProblem(StatusCodes.Status401Unauthorized);
            (await _controller.Leave(CancellationToken.None)).AssertProblem(StatusCodes.Status401Unauthorized);
        }
    }

    [Test]
    public async Task Status_ForAnAnonymousUser_ReturnsTheQueuesSizeAndNotSignedIn()
    {
        var queue = Substitute.For<IQueueRepository>();
        queue.Count(Arg.Any<CancellationToken>()).Returns(3);
        var controller = new QueueController(null!, queue, new ConfigurationBuilder().Build()).WithAnonymousUser();

        var status = await controller.Status(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(status.Count, Is.EqualTo(3));
            Assert.That(status.SignedIn, Is.False);
            Assert.That(status.InQueue, Is.False);
        }
    }

    [Test]
    public async Task Status_ForASignedInPlayer_ReturnsTheirMembership()
    {
        var queue = Substitute.For<IQueueRepository>();
        queue.PlayerState(5, Arg.Any<CancellationToken>()).Returns(new QueuePlayerState(5, true, InQueue: true, InMatch: false, QueueCount: 7));
        var controller = new QueueController(null!, queue, new ConfigurationBuilder().Build()).WithUser("5");

        var status = await controller.Status(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(status.Count, Is.EqualTo(7));
            Assert.That(status.SignedIn, Is.True);
            Assert.That(status.InQueue, Is.True);
            Assert.That(status.HasGamertag, Is.True);
        }
    }

    [Test]
    public async Task Join_RepliesConflict_WhenMatchmakingIsOff()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Preview:Enabled"] = "true" }).Build();
        // Refused before the membership is asked: preview mode without a Discord token turns the matchmaking tick off
        var controller = new QueueController(null!, Substitute.For<IQueueRepository>(), config).WithUser("5");

        var result = await controller.Join(CancellationToken.None);

        result.AssertProblem(StatusCodes.Status409Conflict, QueueMessages.LateLeagueOff);
    }

    [Test]
    public async Task Join_JoinsTheQueue_ForASignedInPlayerWithAGamertagNotAlreadyIn()
    {
        var queue = Substitute.For<IQueueRepository>();
        queue.PlayerState(5, Arg.Any<CancellationToken>()).Returns(new QueuePlayerState(5, true, InQueue: false, InMatch: false, QueueCount: 0));
        queue.AddPlayerToQueue(Arg.Any<QueuedPlayer>(), Arg.Any<CancellationToken>()).Returns(true);
        queue.Count(Arg.Any<CancellationToken>()).Returns(1);
        var membership = new QueueMembership(queue, null!, Substitute.For<IQueueNotifier>());
        var controller = new QueueController(membership, queue, new ConfigurationBuilder().Build()).WithUser("5");

        var result = await controller.Join(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Value!.InQueue, Is.True);
            Assert.That(result.Value!.Count, Is.EqualTo(1));
        }
    }
}
