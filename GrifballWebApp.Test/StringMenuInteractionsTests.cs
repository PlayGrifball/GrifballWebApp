using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Outbox;
using GrifballWebApp.Server.Services;
using Microsoft.Extensions.Options;
using NSubstitute;
using DiscordUser = GrifballWebApp.Database.Models.DiscordUser;
using GrifballWebApp.Server.Extensions;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class StringMenuInteractionsTests
{
    private GrifballContext _context;
    private IDiscordRestClient _discordClient;
    private QueueService _queueService;
    private MatchmakingStringMenuHandler _handler;
    private IDiscordStringMenuInteractionContext _discordContext;
    private IQueueNotifier _notifier;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _discordClient = Substitute.For<IDiscordRestClient>();
        var options = Substitute.For<IOptions<DiscordOptions>>();
        options.Value.Returns(new DiscordOptions { QueueChannel = 123456 });
        var queueRepo = Substitute.For<IQueueRepository>();
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<QueueService>>();
        var dataPull = Substitute.For<IDataPullService>();
        _queueService = new QueueService(logger, options, queueRepo, _discordClient, _context, dataPull, new ContextOutbox(_context), Substitute.For<IQueueNotifier>());
        _handler = new MatchmakingStringMenuHandler(_context, _discordClient, _queueService, _notifier = Substitute.For<IQueueNotifier>());
        _discordContext = Substitute.For<IDiscordStringMenuInteractionContext>();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task VoteToKick_Should_Reject_If_User_Has_No_Gamertag()
    {
        // Arrange
        _discordContext.User.Id.Returns(123UL);
        // Act
        await _handler.VoteToKick(_discordContext, 1);
        // Assert
        await _discordContext.AssertSendResponse("You must set your gamertag first");
    }

    [Test]
    public async Task VoteToKick_Should_Reject_If_Match_Not_Found()
    {
        // Arrange
        var user = new GrifballWebApp.Database.Models.User { Id = 1, DiscordUser = new DiscordUser() { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { Gamertag = "GT" } };
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();
        _discordContext.User.Id.Returns(123UL);
        _discordContext.SelectedValues.Returns(new List<string> { "2" });
        // Act
        await _handler.VoteToKick(_discordContext, 999); // No such match
        // Assert
        await _discordContext.AssertSendResponse("This match does not exist");
    }

    [Test]
    public async Task VoteToKick_Should_Reject_If_Match_Is_Over()
    {
        // Arrange
        var user = new GrifballWebApp.Database.Models.User { Id = 1, DiscordUser = new DiscordUser() { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { Gamertag = "GT" } };
        var match = MatchedMatch.Start(new MatchedTeam([]), new MatchedTeam([])).WithId(1).Canceled();
        _context.Users.Add(user);
        await _context.SaveChangesWithoutConstraints();
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutConstraints();
        _discordContext.User.Id.Returns(123UL);
        _discordContext.SelectedValues.Returns(new List<string> { "2" });
        // Act
        await _handler.VoteToKick(_discordContext, 1);
        // Assert
        await _discordContext.AssertSendResponse("This match is over");
    }

    [Test]
    public async Task VoteToKick_Should_Reject_If_User_Not_In_Match()
    {
        // Arrange
        var user = new User { Id = 1, DiscordUser = new DiscordUser() { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "GT" } };
        var otherUser = new User { Id = 2, DiscordUser = new DiscordUser() { DiscordUserID = 456, DiscordUsername = "456" }, XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "GT2" } };
        var match = MatchedMatch.Start(new MatchedTeam([]), new MatchedTeam([])).WithId(1);
        _context.Users.AddRange(user, otherUser);
        await _context.SaveChangesWithoutConstraints();
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutConstraints();
        _discordContext.User.Id.Returns(123UL);
        _discordContext.SelectedValues.Returns(new List<string> { "2" });
        // Act
        await _handler.VoteToKick(_discordContext, 1);
        // Assert
        await _discordContext.AssertSendResponse("You are not in this match");
    }
    /// <summary>The users, then match 1 (active) of these players: made by the match, as the queue does.</summary>
    private async Task<MatchedMatch> SeedMatch(User[] users, MatchedPlayer[] home, MatchedPlayer[] away)
    {
        _context.Users.AddRange(users);
        await _context.SaveChangesWithoutConstraints();
        var match = MatchedMatch.Start(new MatchedTeam(home), new MatchedTeam(away)).WithId(1);
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutConstraints();
        return match;
    }

    [Test]
    public async Task VoteToKick_Should_Reject_If_User_Kicked()
    {
        // Arrange
        var user = new User { Id = 1, DiscordUser = new DiscordUser() { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "GT" } };
        var otherUser = new User { Id = 2, DiscordUser = new DiscordUser() { DiscordUserID = 456, DiscordUsername = "456" }, XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "GT2" } };
        var me = new MatchedPlayer { UserID = 1, Kicked = true, User = user };
        var them = new MatchedPlayer { UserID = 2, Kicked = false, User = otherUser };
        await SeedMatch([user, otherUser], [me], [them]);

        _discordContext.User.Id.Returns(123UL);
        _discordContext.SelectedValues.Returns(new List<string> { "2" });
        // Act
        await _handler.VoteToKick(_discordContext, 1);
        // Assert
        await _discordContext.AssertSendResponse("You have been kicked. You cannot vote.");
    }

    [Test]
    public async Task VoteToKick_Should_Reject_If_Target_Not_In_Match()
    {
        // Arrange
        var user = new User { Id = 1, DiscordUser = new DiscordUser() { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { Gamertag = "GT" } };
        var me = new MatchedPlayer { UserID = 1, Kicked = false, User = user };
        await SeedMatch([user], [me], []);
        _discordContext.User.Id.Returns(123UL);
        _discordContext.SelectedValues.Returns(new List<string> { "999" }); // Not in match
        // Act
        await _handler.VoteToKick(_discordContext, 1);
        // Assert
        await _discordContext.AssertSendResponse("They are not in this match");
    }

    [Test]
    public async Task VoteToKick_Should_Reject_If_Target_Already_Kicked()
    {
        // Arrange
        var user = new User { Id = 1, DiscordUser = new DiscordUser() { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "GT" } };
        var otherUser = new User { Id = 2, DiscordUser = new DiscordUser() { DiscordUserID = 456, DiscordUsername = "456" }, XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "GT2" }, DisplayName = "TargetUser" };
        var me = new MatchedPlayer { UserID = 1, Kicked = false, User = user };
        var them = new MatchedPlayer { UserID = 2, Kicked = true, User = otherUser };
        await SeedMatch([user, otherUser], [me], [them]);
        _discordContext.User.Id.Returns(123UL);
        _discordContext.SelectedValues.Returns(new List<string> { "2" });
        // Act
        await _handler.VoteToKick(_discordContext, 1);
        // Assert
        await _discordContext.AssertSendResponse($"{them.ToDisplayName()} has already been kicked");
    }

    [Test]
    public async Task VoteToKick_Should_Succeed_On_First_Vote()
    {
        // Arrange
        var user = new User { Id = 1, DiscordUser = new DiscordUser() { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "GT" }, DisplayName = "Voter" };
        var otherUser = new User { Id = 2, DiscordUser = new DiscordUser() { DiscordUserID = 456, DiscordUsername = "456" }, XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "GT2" }, DisplayName = "TargetUser" };
        var me = new MatchedPlayer { UserID = 1, Kicked = false, User = user };
        var them = new MatchedPlayer { UserID = 2, Kicked = false, User = otherUser };
        await SeedMatch([user, otherUser], [me], [them]);

        _discordContext.User.Id.Returns(123UL);
        _discordContext.SelectedValues.Returns(new List<string> { "2" });
        // Act
        await _handler.VoteToKick(_discordContext, 1);
        // Assert
        await _discordContext.AssertSendResponse($"{me.ToDisplayName()} has voted to kick {them.ToDisplayName()}");
    }

    [Test]
    public async Task VoteToKick_Should_Reject_If_Already_Voted()
    {
        // Arrange
        var user = new User { Id = 1, DiscordUser = new DiscordUser() { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "GT" }, DisplayName = "Voter" };
        var otherUser = new User { Id = 2, DiscordUser = new DiscordUser() { DiscordUserID = 456, DiscordUsername = "456" }, XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "GT2" }, DisplayName = "TargetUser" };
        var me = new MatchedPlayer { UserID = 1, Kicked = false, User = user };
        var them = new MatchedPlayer { UserID = 2, Kicked = false, User = otherUser };
        await SeedMatch([user, otherUser], [me], [them]);

        _context.MatchedKickVotes.Add(new MatchedKickVote { MatchId = 1, VoterMatchedPlayerId = me.Id, KickMatchedPlayerId = them.Id });
        await _context.SaveChangesWithoutConstraints();

        _discordContext.User.Id.Returns(123UL);
        _discordContext.SelectedValues.Returns(new List<string> { "2" });

        // Act
        await _handler.VoteToKick(_discordContext, 1);
        // Assert
        await _discordContext.AssertSendResponse($"You have already voted to kick {them.ToDisplayName()}");
    }

    [Test]
    public async Task VoteToKick_Should_Kick_Target_When_Threshold_Reached()
    {
        // Arrange
        var user = new User { Id = 1, DiscordUser = new DiscordUser { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "GT" }, DisplayName = "Voter" };
        var otherUser = new User { Id = 2, DiscordUser = new DiscordUser { DiscordUserID = 456, DiscordUsername = "456" }, XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "GT2" }, DisplayName = "TargetUser" };
        var thirdUser = new User { Id = 3, DiscordUser = new DiscordUser { DiscordUserID = 789, DiscordUsername = "789" }, XboxUser = new XboxUser { XboxUserID = 3, Gamertag = "GT3" }, DisplayName = "Voter2" };
        var me = new MatchedPlayer { UserID = 1, Kicked = false, User = user };
        var them = new MatchedPlayer { UserID = 2, Kicked = false, User = otherUser };
        var voter2 = new MatchedPlayer { UserID = 3, Kicked = false, User = thirdUser };
        await SeedMatch([user, otherUser, thirdUser], [me, voter2], [them]);
        // Add a vote from voter2 (so threshold will be reached with this vote)
        _context.MatchedKickVotes.Add(new MatchedKickVote { MatchId = 1, VoterMatchedPlayerId = voter2.Id, KickMatchedPlayerId = them.Id });
        await _context.SaveChangesWithoutConstraints();
        _discordContext.User.Id.Returns(123UL); // user 1
        _discordContext.SelectedValues.Returns(new List<string> { "2" });
        // Act
        await _handler.VoteToKick(_discordContext, 1);
        // Assert: Target should be kicked
        var kicked = await _context.MatchedPlayers.FindAsync(them.Id);
        Assert.That(kicked, Is.Not.Null);
        Assert.That(kicked.Kicked, Is.True);
        await _discordContext.AssertSendResponse($"{them.ToDisplayName()} has been kicked");
        // Out of the match: their home page offers the queue again
        await _notifier.Received(1).SendStatusChanged(Arg.Is<IReadOnlyCollection<int>>(x => x.SequenceEqual(new[] { 2 })));
    }

    [Test]
    public async Task VoteToKick_A_Player_May_Vote_To_Kick_Two_Others_Each_Saved()
    {
        // Four players: one vote against each of two is no majority, and both are kept (the key has the target)
        var users = Enumerable.Range(1, 4).Select(i => new User
        {
            Id = i,
            DiscordUser = new DiscordUser { DiscordUserID = 100 + i, DiscordUsername = $"d{i}" },
            XboxUser = new XboxUser { XboxUserID = i, Gamertag = $"GT{i}" },
            DisplayName = $"Player{i}",
        }).ToArray();
        var players = users.Select(u => new MatchedPlayer { UserID = u.Id, User = u }).ToArray();
        await SeedMatch(users, [players[0], players[1]], [players[2], players[3]]);
        _discordContext.User.Id.Returns(101UL);

        // Each vote in a handler of its own, as each Discord interaction has
        foreach (var target in new[] { "3", "4" })
        {
            await using var ctx = _context.NewContextLike();
            var handler = new MatchmakingStringMenuHandler(ctx, _discordClient, _queueService, _notifier);
            _discordContext.SelectedValues.Returns(new List<string> { target });
            await handler.VoteToKick(_discordContext, 1);
        }

        await using var fresh = _context.NewContextLike();
        var votes = fresh.MatchedKickVotes.Select(v => new { v.VoterMatchedPlayerId, v.KickMatchedPlayerId }).ToList();
        Assert.Multiple(async () =>
        {
            Assert.That(votes, Is.EquivalentTo(new[]
            {
                new { VoterMatchedPlayerId = players[0].Id, KickMatchedPlayerId = players[2].Id },
                new { VoterMatchedPlayerId = players[0].Id, KickMatchedPlayerId = players[3].Id },
            }));
            Assert.That(fresh.MatchedPlayers.Any(p => p.Kicked), Is.False);
            await _discordContext.AssertSendResponse("GT1 has voted to kick GT3");
            await _discordContext.AssertSendResponse("GT1 has voted to kick GT4");
        });
    }
}
