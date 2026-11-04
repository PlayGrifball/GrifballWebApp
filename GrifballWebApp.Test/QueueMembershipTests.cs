using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Matchmaking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QueueMembershipTests
{
    private GrifballContext _context;
    private IQueueCountNotifier _notifier;
    private QueueMembership _membership;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _notifier = Substitute.For<IQueueCountNotifier>();
        _membership = new QueueMembership(new QueueRepository(_context), _context, _notifier);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<User> AddUser(int id, bool gamertag = true)
    {
        var user = new User { Id = id, XboxUser = gamertag ? new XboxUser { XboxUserID = id, Gamertag = $"GT{id}" } : null };
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();
        return user;
    }

    [Test]
    public async Task Join_AddsThePlayer_AndSendsTheQueueSize()
    {
        await AddUser(1);
        await AddUser(2);

        Assert.That(await _membership.Join(1), Is.EqualTo(QueueJoinResult.Joined));
        Assert.That(await _membership.Join(2), Is.EqualTo(QueueJoinResult.Joined));

        Assert.That(await _context.QueuedPlayer.Select(x => x.UserID).OrderBy(x => x).ToListAsync(), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(await _membership.Count(), Is.EqualTo(2));
        Received.InOrder(() =>
        {
            _notifier.Send(1);
            _notifier.Send(2);
        });
    }

    [Test]
    public async Task Join_NeedsAGamertag()
    {
        await AddUser(1, gamertag: false);

        Assert.That(await _membership.Join(1), Is.EqualTo(QueueJoinResult.NoGamertag));
        Assert.That(await _membership.Join(404), Is.EqualTo(QueueJoinResult.NoGamertag));

        Assert.That(await _context.QueuedPlayer.AnyAsync(), Is.False);
        await _notifier.DidNotReceiveWithAnyArgs().Send(default);
    }

    [Test]
    public async Task Join_Twice_IsRefused()
    {
        await AddUser(1);
        await _membership.Join(1);
        _notifier.ClearReceivedCalls();

        Assert.That(await _membership.Join(1), Is.EqualTo(QueueJoinResult.AlreadyInQueue));
        Assert.That(await _context.QueuedPlayer.CountAsync(), Is.EqualTo(1));
        await _notifier.DidNotReceiveWithAnyArgs().Send(default);
    }

    [Test]
    public async Task Join_InAMatch_IsRefused()
    {
        await AddUser(1);
        var queue = Substitute.For<IQueueRepository>();
        queue.IsInMatch(1, Arg.Any<CancellationToken>()).Returns(true);

        var result = await new QueueMembership(queue, _context, _notifier).Join(1);

        Assert.That(result, Is.EqualTo(QueueJoinResult.InMatch));
        await queue.DidNotReceiveWithAnyArgs().AddPlayerToQueue(default, default);
    }

    [Test]
    public async Task Leave_RemovesThePlayer_AndSendsTheQueueSize()
    {
        await AddUser(1);
        await _membership.Join(1);

        Assert.That(await _membership.Leave(1), Is.EqualTo(QueueLeaveResult.Left));

        Assert.That(await _context.QueuedPlayer.AnyAsync(), Is.False);
        await _notifier.Received(1).Send(0);
    }

    [Test]
    public async Task Leave_WhenNotQueued_OrWithoutAGamertag_IsRefused()
    {
        await AddUser(1);
        await AddUser(2, gamertag: false);

        Assert.That(await _membership.Leave(1), Is.EqualTo(QueueLeaveResult.NotInQueue));
        Assert.That(await _membership.Leave(2), Is.EqualTo(QueueLeaveResult.NoGamertag));
        await _notifier.DidNotReceiveWithAnyArgs().Send(default);
    }

    [Test]
    public void Messages_AreTheDiscordButtonsOnes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(QueueMessages.For(QueueJoinResult.NoGamertag), Is.EqualTo("You must set your gamertag first"));
            Assert.That(QueueMessages.For(QueueJoinResult.AlreadyInQueue), Is.EqualTo("You are already in the queue!"));
            Assert.That(QueueMessages.For(QueueJoinResult.InMatch), Is.EqualTo("You are already in a match!"));
            Assert.That(QueueMessages.For(QueueJoinResult.Joined), Is.EqualTo("You have joined the queue! 🎉"));
            Assert.That(QueueMessages.For(QueueLeaveResult.NoGamertag), Is.EqualTo("You must set your gamertag first"));
            Assert.That(QueueMessages.For(QueueLeaveResult.NotInQueue), Is.EqualTo("You are not in the matchmaking queue."));
            Assert.That(QueueMessages.For(QueueLeaveResult.Left), Is.EqualTo("You have left the queue"));
        });
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QueueControllerTests
{
    private GrifballContext _context;
    private QueueController _controller;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var repository = new QueueRepository(_context);
        _controller = new QueueController(new QueueMembership(repository, _context, Substitute.For<IQueueCountNotifier>()), repository);
        _context.Users.AddRange(
            new User { Id = 1, XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "GT1" } },
            new User { Id = 2 },
            new User { Id = 3, XboxUser = new XboxUser { XboxUserID = 3, Gamertag = "GT3" } });
        _context.QueuedPlayer.Add(new QueuedPlayer { UserID = 3 });
        await _context.SaveChangesWithoutConstraints();
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task Status_ForEveryone_IsTheQueueSize()
    {
        var status = await _controller.WithAnonymousUser().Status(CancellationToken.None);

        Assert.That(status.Count, Is.EqualTo(1));
        Assert.That(status.SignedIn, Is.False);
        Assert.That(status.InQueue, Is.False);
    }

    [Test]
    public async Task Status_ForAPlayer_SaysWhetherTheyAreQueued()
    {
        var queued = await _controller.WithUser("3").Status(CancellationToken.None);
        var noGamertag = await _controller.WithUser("2").Status(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(queued.SignedIn, Is.True);
            Assert.That(queued.HasGamertag, Is.True);
            Assert.That(queued.InQueue, Is.True);
            Assert.That(queued.InMatch, Is.False);
            Assert.That(noGamertag.HasGamertag, Is.False);
            Assert.That(noGamertag.InQueue, Is.False);
        });
    }

    [Test]
    public async Task JoinAndLeave_ReturnTheNewStatus()
    {
        _controller.WithUser("1");

        var joined = (await _controller.Join(CancellationToken.None)).Value!;
        Assert.That(joined.InQueue, Is.True);
        Assert.That(joined.Count, Is.EqualTo(2));

        var left = (await _controller.Leave(CancellationToken.None)).Value!;
        Assert.That(left.InQueue, Is.False);
        Assert.That(left.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Refusals_AreConflicts_WithTheDiscordMessage()
    {
        var noGamertag = (await _controller.WithUser("2").Join(CancellationToken.None)).Result as ConflictObjectResult;
        var notQueued = (await _controller.WithUser("1").Leave(CancellationToken.None)).Result as ConflictObjectResult;
        var twice = (await _controller.WithUser("3").Join(CancellationToken.None)).Result as ConflictObjectResult;

        Assert.That(noGamertag?.Value, Is.EqualTo("You must set your gamertag first"));
        Assert.That(notQueued?.Value, Is.EqualTo("You are not in the matchmaking queue."));
        Assert.That(twice?.Value, Is.EqualTo("You are already in the queue!"));
    }

    [Test]
    public async Task JoinAndLeave_WithoutAUser_AreUnauthorized()
    {
        _controller.WithAnonymousUser();

        Assert.That((await _controller.Join(CancellationToken.None)).Result, Is.TypeOf<UnauthorizedResult>());
        Assert.That((await _controller.Leave(CancellationToken.None)).Result, Is.TypeOf<UnauthorizedResult>());
    }

    [Test]
    public void JoinAndLeave_NeedASignIn_StatusDoesNot()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ControllerTestHelpers.ClassAuthorize(typeof(QueueController)), Is.Null);
            Assert.That(ControllerTestHelpers.ActionAuthorize(typeof(QueueController), nameof(QueueController.Status)), Is.Null);
            Assert.That(ControllerTestHelpers.ActionAuthorize(typeof(QueueController), nameof(QueueController.Join)), Is.Not.Null);
            Assert.That(ControllerTestHelpers.ActionAuthorize(typeof(QueueController), nameof(QueueController.Leave)), Is.Not.Null);
        });
    }
}

[TestFixture]
public class QueueCountNotifierTests
{
    private IQueueHubClient _all;
    private RecordingLogger<QueueCountNotifier> _logger;
    private QueueCountNotifier _notifier;

    [SetUp]
    public void SetUp()
    {
        _all = Substitute.For<IQueueHubClient>();
        var hub = Substitute.For<IHubContext<QueueHub, IQueueHubClient>>();
        hub.Clients.All.Returns(_all);
        _notifier = new QueueCountNotifier(hub, _logger = new RecordingLogger<QueueCountNotifier>());
    }

    [Test]
    public async Task Send_TellsEveryone()
    {
        await _notifier.Send(3);
        await _notifier.Send(3);

        await _all.Received(2).QueueCount(3);
    }

    [Test]
    public async Task SendIfChanged_SkipsTheSizeSentLast()
    {
        await _notifier.SendIfChanged(2);
        await _notifier.SendIfChanged(2);
        await _notifier.Send(4);
        await _notifier.SendIfChanged(4);
        await _notifier.SendIfChanged(1);

        Received.InOrder(() =>
        {
            _all.QueueCount(2);
            _all.QueueCount(4);
            _all.QueueCount(1);
        });
        await _all.Received(3).QueueCount(Arg.Any<int>());
    }

    [Test]
    public async Task Send_LogsFailures_InsteadOfThrowing()
    {
        _all.QueueCount(Arg.Any<int>()).ThrowsAsync(new InvalidOperationException("backplane down"));

        await _notifier.Send(1);

        var entry = _logger.Entries.Single();
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
        Assert.That(entry.Exception, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void TheHub_HasNoMethodsToCall()
    {
        Assert.That(typeof(QueueHub).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly), Is.Empty);
    }
}
