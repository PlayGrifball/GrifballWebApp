using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Outbox;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ButtonInteractionsTests
{
    private GrifballContext _context;
    private IQueueRepository _queueRepository;
    private IPublisher _publisher;
    private RecordingLogger<MatchmakingButtonHandler> _handlerLogger;
    private IDiscordRestClient _discordClient;
    private IOptions<DiscordOptions> _discordOptions;
    private QueueService _queueService;
    private MatchmakingButtonHandler _handler;
    private ILogger<QueueService> _logger;
    private IDataPullService _dataPullService;
    private IDiscordButtonInteractionContext _discordContext;
    private IQueueNotifier _notifier;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _queueRepository = Substitute.For<IQueueRepository>();
        // The user, gamertag and match as the database has them, unless a test says otherwise
        _queueRepository.PlayerStateByDiscord(default, default).ReturnsForAnyArgs(ci => new QueueRepository(_context).PlayerStateByDiscord(ci.ArgAt<long>(0), ci.ArgAt<CancellationToken>(1)));
        _publisher = Substitute.For<IPublisher>();
        _discordClient = Substitute.For<IDiscordRestClient>();
        _discordOptions = Substitute.For<IOptions<DiscordOptions>>();
        _discordOptions.Value.Returns(new DiscordOptions { QueueChannel = 123456 });
        _logger = Substitute.For<ILogger<QueueService>>();
        _dataPullService = Substitute.For<IDataPullService>();
        _queueService = new QueueService(_logger, _discordOptions, _queueRepository, _discordClient, _context, _dataPullService, new ContextOutbox(_context), Substitute.For<IQueueNotifier>());
        _notifier = Substitute.For<IQueueNotifier>();
        _handler = new MatchmakingButtonHandler(new QueueMembership(_queueRepository, _context, _notifier), new ServiceCollection().AddScoped(_ => _publisher).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), _context, _discordClient, _queueService, _handlerLogger = new RecordingLogger<MatchmakingButtonHandler>());

        _discordContext = Substitute.For<IDiscordButtonInteractionContext>();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task SetGamertag_Should_SendModal()
    {
        // Act
        await _handler.SetGamertag(_discordContext);

        // Assert
        await _discordContext.Received(1).Interaction.SendResponseAsync(Arg.Is<InteractionCallbackProperties>(cb =>
            cb.Type == InteractionCallbackType.Modal
        ));
    }

    [Test]
    public async Task JoinQueue_Should_Reject_If_User_Has_No_Gamertag()
    {
        // Arrange
        _discordContext.User.Id.Returns(123UL);

        // No user in DB
        // Act
        await _handler.JoinQueue(_discordContext);

        // Assert
        await _discordContext.AssertSendResponse("You must set your gamertag first");
    }

    [Test]
    public async Task JoinQueue_Should_Reject_If_Already_In_Queue()
    {
        // Arrange
        var discordUser = new Database.Models.DiscordUser { DiscordUserID = 123, DiscordUsername = "TestUser" };
        var user = User.Seed(id: 1, discordUser: discordUser, xboxUser: new XboxUser { Gamertag = "TestGT" });
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(123UL);

        _queueRepository.AddPlayerToQueue(user.Id).Returns(false);

        // Act
        await _handler.JoinQueue(_discordContext);

        // Assert
        await _discordContext.AssertSendResponse("You are already in the Late League queue!");
    }

    [Test]
    public async Task JoinQueue_Should_Succeed_If_Not_In_Queue_Or_Match()
    {
        // Arrange
        var discordUser = new Database.Models.DiscordUser()
        {
            DiscordUserID = 456,
            DiscordUsername = "456",
        };
        var user = User.Seed(id: 2, discordUser: discordUser, xboxUser: new XboxUser { Gamertag = "TestGT2" });
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(456UL);

        _queueRepository.AddPlayerToQueue(user.Id).Returns(true);

        // Act
        await _handler.JoinQueue(_discordContext);

        // Assert
        await _queueRepository.Received(1).AddPlayerToQueue(user.Id);
        await _notifier.Received(1).SendStatusChanged(Arg.Is<IReadOnlyCollection<int>>(x => x.SequenceEqual(new[] { user.Id })));
        await _notifier.Received(1).Send(Arg.Any<int>());
        await _discordContext.AssertSendResponse("You have joined the Late League queue! 🎉");
    }

    [Test]
    public async Task LeaveQueue_Should_Reject_If_Not_In_Queue()
    {
        // Arrange
        var discordUser = new Database.Models.DiscordUser()
        {
            DiscordUserID = 789,
            DiscordUsername = "789",
        };
        var user = User.Seed(id: 3, discordUser: discordUser, xboxUser: new XboxUser { Gamertag = "TestGT3" });
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(789UL);

        _queueRepository.RemovePlayerToQueue(user.Id).Returns(false);

        // Act
        await _handler.LeaveQueue(_discordContext);

        // Assert
        await _discordContext.AssertSendResponse("You are not in the Late League queue.");
    }

    [Test]
    public async Task LeaveQueue_Should_Succeed_If_In_Queue()
    {
        // Arrange
        var discordUser = new Database.Models.DiscordUser()
        {
            DiscordUserID = 1011,
            DiscordUsername = "1011",
        };
        var user = User.Seed(id: 4, discordUser: discordUser, xboxUser: new XboxUser { Gamertag = "TestGT4" });
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(1011UL);

        _queueRepository.RemovePlayerToQueue(user.Id).Returns(true);

        // Act
        await _handler.LeaveQueue(_discordContext);

        // Assert
        await _queueRepository.Received(1).RemovePlayerToQueue(user.Id);
        await _notifier.Received(1).Send(Arg.Any<int>());
        await _discordContext.AssertSendResponse("You have left the Late League queue");
    }

    [Test]
    public async Task JoinQueue_Should_Reject_If_In_A_Match()
    {
        var user = User.Seed(id: 5, discordUser: new Database.Models.DiscordUser { DiscordUserID = 1213, DiscordUsername = "1213" }, xboxUser: new XboxUser { Gamertag = "TestGT5" });
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();
        _discordContext.User.Id.Returns(1213UL);
        _queueRepository.PlayerStateByDiscord(1213, Arg.Any<CancellationToken>()).Returns(new QueuePlayerState(user.Id, HasGamertag: true, InQueue: false, InMatch: true, QueueCount: 0));

        await _handler.JoinQueue(_discordContext);

        await _queueRepository.DidNotReceive().AddPlayerToQueue(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().Send(Arg.Any<int>());
        await _publisher.DidNotReceive().Publish(Arg.Any<UpdateDisplayNotification>(), Arg.Any<CancellationToken>());
        await _discordContext.AssertSendResponse("You are already in a Late League match!");
    }

    [Test]
    public async Task LeaveQueue_Without_A_Gamertag_Still_Leaves()
    {
        // Unlinked while queued: nobody should be stuck in the queue
        _context.Users.Add(User.Seed(id: 6, discordUser: new Database.Models.DiscordUser { DiscordUserID = 1415, DiscordUsername = "1415" }));
        await _context.SaveChangesWithoutConstraints();
        _discordContext.User.Id.Returns(1415UL);
        _queueRepository.RemovePlayerToQueue(6, Arg.Any<CancellationToken>()).Returns(true);

        await _handler.LeaveQueue(_discordContext);

        await _queueRepository.Received(1).RemovePlayerToQueue(6, Arg.Any<CancellationToken>());
        await _discordContext.AssertSendResponse("You have left the Late League queue");
    }

    [Test]
    public async Task LeaveQueue_Should_Reject_If_Not_Registered()
    {
        _discordContext.User.Id.Returns(1617UL);

        await _handler.LeaveQueue(_discordContext);

        await _queueRepository.DidNotReceive().RemovePlayerToQueue(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _discordContext.AssertSendResponse("You are not in the Late League queue.");
    }

    [TestCase(WinnerVote.Home)]
    [TestCase(WinnerVote.Away)]
    [TestCase(WinnerVote.Cancel)]
    public async Task VoteForWinner_Should_SaveVote_And_Respond_When_Valid(WinnerVote voteFor)
    {
        // Arrange
        var discordUserId = 2000UL;
        var user = User.Seed(id: 10, discordUser: new Database.Models.DiscordUser { DiscordUserID = (long)discordUserId, DiscordUsername = "winner" }, xboxUser: new XboxUser { Gamertag = "GT" });
        var matchedPlayer = new MatchedPlayer { UserID = user.Id, User = user, Kicked = false };
        var match = MatchedMatch.Start(new MatchedTeam([matchedPlayer]), new MatchedTeam([])).WithId(99);
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(discordUserId);

        // Act
        await _handler.VoteForWinner(_discordContext, match.Id, voteFor.ToString());

        // Assert
        // Vote is saved
        var vote = _context.MatchedWinnerVotes.FirstOrDefault(x => x.MatchId == match.Id && x.MatchedPlayerId == matchedPlayer.Id);
        Assert.That(vote, Is.Not.Null, "Vote was not saved to the database.");
        Assert.That(vote.WinnerVote, Is.EqualTo(voteFor));

        // User receives thanks message
        await _discordContext.AssertSendResponse($"Thanks for voting! You voted for {voteFor}");
    }

    [Test]
    public async Task VoteForWinner_A_Second_Vote_Changes_The_First()
    {
        var discordUserId = 2000UL;
        var user = User.Seed(id: 10, discordUser: new Database.Models.DiscordUser { DiscordUserID = (long)discordUserId, DiscordUsername = "winner" }, xboxUser: new XboxUser { Gamertag = "GT" });
        var match = MatchedMatch.Start(new MatchedTeam([new MatchedPlayer { UserID = user.Id, User = user }]), new MatchedTeam([])).WithId(99);
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutConstraints();
        _discordContext.User.Id.Returns(discordUserId);

        await _handler.VoteForWinner(_discordContext, match.Id, nameof(WinnerVote.Home));
        await _handler.VoteForWinner(_discordContext, match.Id, nameof(WinnerVote.Away));

        await using var fresh = _context.NewContextLike();
        Assert.That(fresh.MatchedWinnerVotes.Select(v => v.WinnerVote).ToList(), Is.EqualTo(new[] { WinnerVote.Away }), "One vote, changed");
    }

    [Test]
    public async Task VoteForWinner_Should_HandleKickedFromMatch()
    {
        // Arrange
        var discordUserId = 2000UL;
        var user = User.Seed(id: 10, discordUser: new Database.Models.DiscordUser { DiscordUserID = (long)discordUserId, DiscordUsername = "winner" }, xboxUser: new XboxUser { Gamertag = "GT" });
        var matchedPlayer = new MatchedPlayer { UserID = user.Id, User = user, Kicked = true };
        var match = MatchedMatch.Start(new MatchedTeam([matchedPlayer]), new MatchedTeam([])).WithId(99);
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(discordUserId);

        // Act
        await _handler.VoteForWinner(_discordContext, match.Id, WinnerVote.Home.ToString());

        // Assert
        // Vote is saved
        var vote = _context.MatchedWinnerVotes.FirstOrDefault();
        Assert.That(vote, Is.Null, "Vote was saved to the database for some reason.");

        // User receives thanks message
        await _discordContext.AssertSendResponse("You are not allowed to vote since you have been kicked");
    }

    [Test]
    public async Task VoteForWinner_Should_HandleNotInMatch()
    {
        // Arrange
        var discordUserId = 2000UL;
        var user = User.Seed(id: 10, discordUser: new Database.Models.DiscordUser { DiscordUserID = (long)discordUserId, DiscordUsername = "winner" }, xboxUser: new XboxUser { Gamertag = "GT" });
        var match = MatchedMatch.Start(new MatchedTeam([]), new MatchedTeam([])).WithId(99);
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(discordUserId);

        // Act
        await _handler.VoteForWinner(_discordContext, match.Id, WinnerVote.Home.ToString());

        // Assert
        // Vote is saved
        var vote = _context.MatchedWinnerVotes.FirstOrDefault();
        Assert.That(vote, Is.Null, "Vote was saved to the database for some reason.");

        // User receives thanks message
        await _discordContext.AssertSendResponse("You are not allowed to vote since you are not in this match");
    }

    [Test]
    public async Task VoteForWinner_Should_HandleNoMatch()
    {
        // Arrange
        var discordUserId = 2000UL;
        var user = User.Seed(id: 10, discordUser: new Database.Models.DiscordUser { DiscordUserID = (long)discordUserId, DiscordUsername = "winner" }, xboxUser: new XboxUser { Gamertag = "GT" });
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(discordUserId);

        // Act
        await _handler.VoteForWinner(_discordContext, 11, WinnerVote.Home.ToString());

        // Assert
        // Vote is not saved
        var vote = _context.MatchedWinnerVotes.FirstOrDefault();
        Assert.That(vote, Is.Null, "Vote was saved to the database for some reason.");

        // User receives thanks message
        await _discordContext.AssertSendResponse("Match does not exist or it is no longer active");
    }

    [Test]
    public async Task RefreshQueueDisplay_PublishesInItsOwnScope()
    {
        await _handler.RefreshQueueDisplay();

        await _publisher.Received(1).Publish(Arg.Any<UpdateDisplayNotification>(), Arg.Any<CancellationToken>());
        Assert.That(_handlerLogger.Entries, Is.Empty);
    }

    [Test]
    public async Task RefreshQueueDisplay_LogsFailures_InsteadOfThrowing()
    {
        _publisher.Publish(Arg.Any<UpdateDisplayNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("discord down")));

        await _handler.RefreshQueueDisplay();

        var entry = _handlerLogger.Entries.Single();
        Assert.Multiple(() =>
        {
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Exception, Is.TypeOf<InvalidOperationException>());
        });
    }
}
