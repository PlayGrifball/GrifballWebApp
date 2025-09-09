using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ButtonInteractionsTests
{
    private GrifballContext _context;
    private IQueueRepository _queueRepository;
    private IPublisher _publisher;
    private IDiscordRestClient _discordClient;
    private IOptions<DiscordOptions> _discordOptions;
    private QueueService _queueService;
    private ButtonInteractions _buttonInteractions;
    private ILogger<QueueService> _logger;
    private IDataPullService _dataPullService;
    private IDiscordButtonInteractionContext _discordContext;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _queueRepository = Substitute.For<IQueueRepository>();
        _publisher = Substitute.For<IPublisher>();
        _discordClient = Substitute.For<IDiscordRestClient>();
        _discordOptions = Substitute.For<IOptions<DiscordOptions>>();
        _discordOptions.Value.Returns(new DiscordOptions { QueueChannel = 123456 });
        _logger = Substitute.For<ILogger<QueueService>>();
        _dataPullService = Substitute.For<IDataPullService>();
        _queueService = new QueueService(_logger, _discordOptions, _queueRepository, _discordClient, _context, _dataPullService);
        _buttonInteractions = new ButtonInteractions(_queueRepository, _publisher, _context, _discordClient, _discordOptions, _queueService);

        _discordContext = Substitute.For<IDiscordButtonInteractionContext>();
        var type = _buttonInteractions.GetType();
        var field = type.GetField("_discordContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field!.SetValue(_buttonInteractions, _discordContext);
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
        await _buttonInteractions.SetGamertag();

        // Assert
        await _discordContext.Received(1).Interaction.SendResponseAsync(Arg.Is<InteractionCallback>(cb =>
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
        await _buttonInteractions.JoinQueue();

        // Assert
        await _discordContext.AssertSendResponse("You must set your gamertag first");
    }

    [Test]
    public async Task JoinQueue_Should_Reject_If_Already_In_Queue()
    {
        // Arrange
        var discordUser = new Database.Models.DiscordUser { DiscordUserID = 123, DiscordUsername = "TestUser" };
        var user = new User { Id = 1, DiscordUser = discordUser, XboxUser = new XboxUser { Gamertag = "TestGT" } };
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(123UL);

        _queueRepository.GetQueuePlayer(user.Id).Returns(new QueuedPlayer { User = user });

        // Act
        await _buttonInteractions.JoinQueue();

        // Assert
        await _discordContext.AssertSendResponse("You are already in the queue!");
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
        var user = new User { Id = 2, DiscordUser = discordUser, XboxUser = new XboxUser { Gamertag = "TestGT2" } };
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(456UL);

        _queueRepository.GetQueuePlayer(user.Id).Returns((QueuedPlayer)null);
        _queueRepository.IsInMatch(user.Id).Returns(false);

        // Act
        await _buttonInteractions.JoinQueue();

        // Assert
        await _queueRepository.Received(1).AddPlayerToQueue(user.Id);
        await _discordContext.AssertSendResponse("You have joined the queue! 🎉");
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
        var user = new User { Id = 3, DiscordUser = discordUser, XboxUser = new XboxUser { Gamertag = "TestGT3" } };
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(789UL);

        _queueRepository.GetQueuePlayer(user.Id).Returns((QueuedPlayer)null);

        // Act
        await _buttonInteractions.LeaveQueue();

        // Assert
        await _discordContext.AssertSendResponse("You are not in the matchmaking queue.");
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
        var user = new User { Id = 4, DiscordUser = discordUser, XboxUser = new XboxUser { Gamertag = "TestGT4" } };
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(1011UL);

        _queueRepository.GetQueuePlayer(user.Id).Returns(new QueuedPlayer { User = user });

        // Act
        await _buttonInteractions.LeaveQueue();

        // Assert
        await _queueRepository.Received(1).RemovePlayerToQueue(user.Id);
        await _discordContext.AssertSendResponse("You have left the queue");
    }

    [TestCase(WinnerVote.Home)]
    [TestCase(WinnerVote.Away)]
    [TestCase(WinnerVote.Cancel)]
    public async Task VoteForWinner_Should_SaveVote_And_Respond_When_Valid(WinnerVote voteFor)
    {
        // Arrange
        var discordUserId = 2000UL;
        var user = new User { Id = 10, DiscordUser = new Database.Models.DiscordUser { DiscordUserID = (long)discordUserId, DiscordUsername = "winner" }, XboxUser = new XboxUser { Gamertag = "GT" } };
        var homeTeam = new MatchedTeam();
        var awayTeam = new MatchedTeam();
        var matchedPlayer = new MatchedPlayer { Id = 100, UserID = user.Id, User = user, Kicked = false };
        var match = new MatchedMatch
        {
            Id = 99,
            HomeTeam = homeTeam,
            AwayTeam = awayTeam,
            Active = true,
        };
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();
        _context.MatchedTeams.Add(homeTeam);
        _context.MatchedTeams.Add(awayTeam);
        await _context.SaveChangesAsync();
        matchedPlayer.MatchedTeamID = homeTeam.MatchedTeamId;
        _context.MatchedPlayers.Add(matchedPlayer);
        await _context.SaveChangesWithoutConstraints();
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(discordUserId);

        // Act
        await _buttonInteractions.VoteForWinner(match.Id, voteFor.ToString());

        // Assert
        // Vote is saved
        var vote = _context.MatchedWinnerVotes.FirstOrDefault(x => x.MatchId == match.Id && x.MatchedPlayerId == matchedPlayer.Id);
        Assert.That(vote, Is.Not.Null, "Vote was not saved to the database.");
        Assert.That(vote.WinnerVote, Is.EqualTo(voteFor));

        // User receives thanks message
        await _discordContext.AssertSendResponse($"Thanks for voting! You voted for {voteFor}");
    }

    [Test]
    public async Task VoteForWinner_Should_HandleKickedFromMatch()
    {
        // Arrange
        var discordUserId = 2000UL;
        var user = new User { Id = 10, DiscordUser = new Database.Models.DiscordUser { DiscordUserID = (long)discordUserId, DiscordUsername = "winner" }, XboxUser = new XboxUser { Gamertag = "GT" } };
        var homeTeam = new MatchedTeam();
        var awayTeam = new MatchedTeam();
        var matchedPlayer = new MatchedPlayer { Id = 100, UserID = user.Id, User = user, Kicked = true };
        var match = new MatchedMatch
        {
            Id = 99,
            HomeTeam = homeTeam,
            AwayTeam = awayTeam,
            Active = true,
        };
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();
        _context.MatchedTeams.Add(homeTeam);
        _context.MatchedTeams.Add(awayTeam);
        await _context.SaveChangesAsync();
        matchedPlayer.MatchedTeamID = homeTeam.MatchedTeamId;
        _context.MatchedPlayers.Add(matchedPlayer);
        await _context.SaveChangesWithoutConstraints();
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(discordUserId);

        // Act
        await _buttonInteractions.VoteForWinner(match.Id, WinnerVote.Home.ToString());

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
        var user = new User { Id = 10, DiscordUser = new Database.Models.DiscordUser { DiscordUserID = (long)discordUserId, DiscordUsername = "winner" }, XboxUser = new XboxUser { Gamertag = "GT" } };
        var homeTeam = new MatchedTeam();
        var awayTeam = new MatchedTeam();
        var match = new MatchedMatch
        {
            Id = 99,
            HomeTeam = homeTeam,
            AwayTeam = awayTeam,
            Active = true,
        };
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();
        _context.MatchedTeams.Add(homeTeam);
        _context.MatchedTeams.Add(awayTeam);
        await _context.SaveChangesAsync();
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(discordUserId);

        // Act
        await _buttonInteractions.VoteForWinner(match.Id, WinnerVote.Home.ToString());

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
        var user = new User { Id = 10, DiscordUser = new Database.Models.DiscordUser { DiscordUserID = (long)discordUserId, DiscordUsername = "winner" }, XboxUser = new XboxUser { Gamertag = "GT" } };
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(discordUserId);

        // Act
        await _buttonInteractions.VoteForWinner(11, WinnerVote.Home.ToString());

        // Assert
        // Vote is not saved
        var vote = _context.MatchedWinnerVotes.FirstOrDefault();
        Assert.That(vote, Is.Null, "Vote was saved to the database for some reason.");

        // User receives thanks message
        await _discordContext.AssertSendResponse("Match does not exist or it is no longer active");
    }
}
