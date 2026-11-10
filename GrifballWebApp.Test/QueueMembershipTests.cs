using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using GrifballWebApp.Application.Matchmaking;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QueueMembershipTests
{
    private GrifballContext _context;
    private IQueueNotifier _notifier;
    private QueueMembership _membership;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _notifier = Substitute.For<IQueueNotifier>();
        _membership = new QueueMembership(new QueueRepository(_context), _context, _notifier);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<User> AddUser(int id, bool gamertag = true)
    {
        var user = User.Seed(id: id, xboxUser: gamertag ? XboxUser.FromXbox(id, $"GT{id}") : null);
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();
        return user;
    }

    [Test]
    public async Task Join_AddsThePlayer_AndSendsTheQueueSize()
    {
        await AddUser(1);
        await AddUser(2);

        Assert.That(await _membership.Join(1), Is.EqualTo((QueueJoinResult.Joined, 1)));
        Assert.That(await _membership.Join(2), Is.EqualTo((QueueJoinResult.Joined, 2)));

        Assert.That(await _context.QueuedPlayer.Select(x => x.UserID).OrderBy(x => x).ToListAsync(), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(await _context.QueuedPlayer.CountAsync(), Is.EqualTo(2));
        var sent = _notifier.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IQueueNotifier.Send))
            .Select(c => (int)c.GetArguments()[0]!);
        Assert.That(sent, Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public async Task JoinAndLeave_DoNotWaitForTheSend()
    {
        // A backplane that never answers: a Discord button must still be answered within 3 seconds
        _notifier.Send(Arg.Any<int>()).Returns(new TaskCompletionSource().Task);
        await AddUser(1);

        var join = _membership.Join(1);
        Assert.That(await Task.WhenAny(join, Task.Delay(TimeSpan.FromSeconds(30))), Is.SameAs(join));
        var leave = _membership.Leave(1);
        Assert.That(await Task.WhenAny(leave, Task.Delay(TimeSpan.FromSeconds(30))), Is.SameAs(leave));

        Assert.That((await join).Result, Is.EqualTo(QueueJoinResult.Joined));
        Assert.That(await leave, Is.EqualTo((QueueLeaveResult.Left, 0)));
    }

    [Test]
    public async Task Join_WhenTheRowIsAlreadyThere_IsAlreadyInQueue()
    {
        // Another request added it after the status was read: the repository's insert decides
        var queue = Substitute.For<IQueueRepository>();
        queue.PlayerState(1, Arg.Any<CancellationToken>()).Returns(new QueuePlayerState(1, HasGamertag: true, InQueue: false, InMatch: false, QueueCount: 0));
        queue.AddPlayerToQueue(Arg.Is<QueuedPlayer>(x => x.UserID == 1), Arg.Any<CancellationToken>()).Returns(false);

        Assert.That((await new QueueMembership(queue, _context, _notifier).Join(1)).Result, Is.EqualTo(QueueJoinResult.AlreadyInQueue));
        await _notifier.DidNotReceiveWithAnyArgs().Send(default);
        await _notifier.DidNotReceiveWithAnyArgs().SendStatusChanged(default!);
    }

    [Test]
    public async Task JoinAndLeave_TellThePlayerTheirStatusChanged()
    {
        // Their page may be open elsewhere: it loads their status again
        await AddUser(7);

        await _membership.Join(7);
        await _membership.Leave(7);

        await _notifier.Received(2).SendStatusChanged(Arg.Is<IReadOnlyCollection<int>>(x => x.SequenceEqual(new[] { 7 })));
    }

    [Test]
    public async Task ByDiscord_JoinsAndLeavesAsTheLinkedUser()
    {
        _context.Users.Add(User.Seed(id: 8, discordUser: DiscordUser.FromDiscord(800, "d800"), xboxUser: XboxUser.FromXbox(8, "GT8")));
        await _context.SaveChangesWithoutConstraints();

        Assert.That(await _membership.JoinByDiscord(800), Is.EqualTo((QueueJoinResult.Joined, 1)));
        Assert.That((await _membership.JoinByDiscord(801)).Result, Is.EqualTo(QueueJoinResult.NoGamertag), "No such user");
        Assert.That(await _context.QueuedPlayer.Select(x => x.UserID).SingleAsync(), Is.EqualTo(8));
        Assert.That(await _membership.LeaveByDiscord(801), Is.EqualTo((QueueLeaveResult.NotInQueue, 0)), "No such user");
        Assert.That(await _membership.LeaveByDiscord(800), Is.EqualTo((QueueLeaveResult.Left, 0)));
        Assert.That(await _context.QueuedPlayer.AnyAsync(), Is.False);
    }

    [Test]
    public async Task Join_NeedsAGamertag()
    {
        await AddUser(1, gamertag: false);

        Assert.That((await _membership.Join(1)).Result, Is.EqualTo(QueueJoinResult.NoGamertag));
        Assert.That((await _membership.Join(404)).Result, Is.EqualTo(QueueJoinResult.NoGamertag));

        Assert.That(await _context.QueuedPlayer.AnyAsync(), Is.False);
        await _notifier.DidNotReceiveWithAnyArgs().Send(default);
    }

    [Test]
    public async Task Join_Twice_IsRefused()
    {
        await AddUser(1);
        await _membership.Join(1);
        _notifier.ClearReceivedCalls();

        Assert.That((await _membership.Join(1)).Result, Is.EqualTo(QueueJoinResult.AlreadyInQueue));
        Assert.That(await _context.QueuedPlayer.CountAsync(), Is.EqualTo(1));
        await _notifier.DidNotReceiveWithAnyArgs().Send(default);
    }

    [Test]
    public async Task Join_InAMatch_IsRefused()
    {
        var queue = Substitute.For<IQueueRepository>();
        queue.PlayerState(1, Arg.Any<CancellationToken>()).Returns(new QueuePlayerState(1, HasGamertag: true, InQueue: false, InMatch: true, QueueCount: 0));

        var (result, _) = await new QueueMembership(queue, _context, _notifier).Join(1);

        Assert.That(result, Is.EqualTo(QueueJoinResult.InMatch));
        await queue.DidNotReceiveWithAnyArgs().AddPlayerToQueue(default!, default);
    }

    [Test]
    public async Task Leave_RemovesThePlayer_AndSendsTheQueueSize()
    {
        await AddUser(1);
        await _membership.Join(1);

        Assert.That((await _membership.Leave(1)).Result, Is.EqualTo(QueueLeaveResult.Left));

        Assert.That(await _context.QueuedPlayer.AnyAsync(), Is.False);
        await _notifier.Received(1).Send(0);
    }

    [Test]
    public async Task Leave_WhenNotQueued_IsRefused()
    {
        await AddUser(1);

        Assert.That((await _membership.Leave(1)).Result, Is.EqualTo(QueueLeaveResult.NotInQueue));
        Assert.That((await _membership.Leave(404)).Result, Is.EqualTo(QueueLeaveResult.NotInQueue));
        await _notifier.DidNotReceiveWithAnyArgs().Send(default);
    }

    [Test]
    public async Task Leave_WithoutAGamertag_StillLeaves()
    {
        // Unlinked while queued: nobody should be stuck in the queue
        await AddUser(2, gamertag: false);
        _context.QueuedPlayer.Add(QueuedPlayer.Seed(2, DateTime.UtcNow));
        await _context.SaveChangesAsync();

        Assert.That(await _membership.Leave(2), Is.EqualTo((QueueLeaveResult.Left, 0)));
        Assert.That(await _context.QueuedPlayer.AnyAsync(), Is.False);
    }

    [Test]
    public void Messages_AreTheDiscordButtonsOnes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(QueueMessages.For(QueueJoinResult.NoGamertag), Is.EqualTo("You must set your gamertag first"));
            Assert.That(QueueMessages.For(QueueJoinResult.AlreadyInQueue), Is.EqualTo("You are already in the Late League queue!"));
            Assert.That(QueueMessages.For(QueueJoinResult.InMatch), Is.EqualTo("You are already in a Late League match!"));
            Assert.That(QueueMessages.For(QueueJoinResult.Joined), Is.EqualTo("You have joined the Late League queue! 🎉"));
            Assert.That(QueueMessages.For(QueueLeaveResult.NotInQueue), Is.EqualTo("You are not in the Late League queue."));
            Assert.That(QueueMessages.For(QueueLeaveResult.Left), Is.EqualTo("You have left the Late League queue"));
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
        _controller = Controller();
        _context.Users.AddRange(
            User.Seed(id: 1, xboxUser: XboxUser.FromXbox(1, "GT1")),
            User.Seed(id: 2),
            User.Seed(id: 3, xboxUser: XboxUser.FromXbox(3, "GT3")));
        _context.QueuedPlayer.Add(QueuedPlayer.Seed(3, DateTime.UtcNow));
        await _context.SaveChangesWithoutConstraints();
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private QueueController Controller(Dictionary<string, string?>? settings = null)
    {
        var repository = new QueueRepository(_context);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? new()).Build();
        return new QueueController(new QueueMembership(repository, _context, Substitute.For<IQueueNotifier>()), repository, configuration);
    }

    /// <summary>A preview without Discord: the matchmaking tick does not run.</summary>
    private QueueController DiscordOffController() => Controller(new() { ["Preview:Enabled"] = "true" });

    [Test]
    public async Task Status_ForEveryone_IsTheQueueSize()
    {
        var status = await _controller.WithAnonymousUser().Status(CancellationToken.None);

        Assert.That(status.Count, Is.EqualTo(1));
        Assert.That(status.Open, Is.True);
        Assert.That(status.SignedIn, Is.False);
        Assert.That(status.InQueue, Is.False);
    }

    [Test]
    public async Task WithDiscordOff_JoiningIsRefused_LeavingIsNot()
    {
        var controller = DiscordOffController();

        var status = await controller.WithUser("1").Status(CancellationToken.None);
        var join = (await controller.Join(CancellationToken.None)).Result as ConflictObjectResult;
        var queued = await _context.QueuedPlayer.AnyAsync(x => x.UserID == 1);
        var left = (await controller.WithUser("3").Leave(CancellationToken.None)).Value!;

        Assert.Multiple(() =>
        {
            Assert.That(status.Open, Is.False);
            Assert.That(join?.Value, Is.EqualTo("Late League is off here"));
            Assert.That(queued, Is.False);
            Assert.That(left.Open, Is.False);
            Assert.That(left.InQueue, Is.False);
        });
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
    public async Task Leave_WithoutAGamertag_ReturnsTheStatusAsItIs()
    {
        _context.QueuedPlayer.Add(QueuedPlayer.Seed(2, DateTime.UtcNow));
        await _context.SaveChangesAsync();

        var left = (await _controller.WithUser("2").Leave(CancellationToken.None)).Value!;

        Assert.Multiple(() =>
        {
            Assert.That(left.SignedIn, Is.True);
            Assert.That(left.HasGamertag, Is.False);
            Assert.That(left.InQueue, Is.False);
            Assert.That(left.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Status_ForAPlayerInAMatch_SaysSo()
    {
        _context.MatchedMatches.Add(MatchedMatch.Start(
            new MatchedTeam([new MatchedPlayer(1)]),
            new MatchedTeam([])));
        await _context.SaveChangesAsync();

        var status = await _controller.WithUser("1").Status(CancellationToken.None);

        Assert.That(status.InMatch, Is.True);
        Assert.That(status.InQueue, Is.False);
        Assert.That(status.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Status_ForAUserThatIsGone_IsTheQueueSize()
    {
        var status = await _controller.WithUser("404").Status(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(status.SignedIn, Is.True);
            Assert.That(status.HasGamertag, Is.False);
            Assert.That(status.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Refusals_AreConflicts_WithTheDiscordMessage()
    {
        var noGamertag = (await _controller.WithUser("2").Join(CancellationToken.None)).Result as ConflictObjectResult;
        var notQueued = (await _controller.WithUser("1").Leave(CancellationToken.None)).Result as ConflictObjectResult;
        var twice = (await _controller.WithUser("3").Join(CancellationToken.None)).Result as ConflictObjectResult;

        Assert.That(noGamertag?.Value, Is.EqualTo("You must set your gamertag first"));
        Assert.That(notQueued?.Value, Is.EqualTo("You are not in the Late League queue."));
        Assert.That(twice?.Value, Is.EqualTo("You are already in the Late League queue!"));
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
public class QueueNotifierTests
{
    private IQueueHubClient _all;
    private IQueueHubClient _players;
    private IHubContext<QueueHub, IQueueHubClient> _hub;
    private RecordingLogger<QueueNotifier> _logger;
    private QueueNotifier _notifier;

    [SetUp]
    public void SetUp()
    {
        _all = Substitute.For<IQueueHubClient>();
        _players = Substitute.For<IQueueHubClient>();
        _hub = Substitute.For<IHubContext<QueueHub, IQueueHubClient>>();
        _hub.Clients.All.Returns(_all);
        _hub.Clients.Users(Arg.Any<IReadOnlyList<string>>()).Returns(_players);
        _notifier = new QueueNotifier(_hub, _logger = new RecordingLogger<QueueNotifier>());
    }

    [Test]
    public async Task Send_TellsEveryone()
    {
        await _notifier.Send(3);
        await _notifier.Send(3);

        await _all.Received(2).QueueCount(3);
    }

    [Test]
    public async Task SendStatusChanged_TellsThosePlayers()
    {
        await _notifier.SendStatusChanged([4, 12]);

        await _players.Received(1).QueueStatusChanged();
        _hub.Clients.Received(1).Users(Arg.Is<IReadOnlyList<string>>(x => x.SequenceEqual(new[] { "4", "12" })));
    }

    [Test]
    public async Task SendStatusChanged_ToNobody_SendsNothing()
    {
        await _notifier.SendStatusChanged([]);

        _hub.Clients.DidNotReceiveWithAnyArgs().Users(default(IReadOnlyList<string>)!);
    }

    [Test]
    public async Task SendStatusChanged_LogsFailures_InsteadOfThrowing()
    {
        _players.QueueStatusChanged().ThrowsAsync(new InvalidOperationException("backplane down"));

        await _notifier.SendStatusChanged([1]);

        var entry = _logger.Entries.Single();
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
        Assert.That(entry.Exception, Is.TypeOf<InvalidOperationException>());
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
