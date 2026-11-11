using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Matchmaking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using DiscordUser = GrifballWebApp.Domain.Players.DiscordUser;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Infrastructure.Matchmaking;
using GrifballWebApp.Application.Extensions;

namespace GrifballWebApp.Integration.Test;

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
    private static readonly int[] second = new[] { 2 };
    private static readonly int[] secondArray = new[] { 4 };

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
        _discordClient = Substitute.For<IDiscordRestClient>();
        var options = Substitute.For<IOptions<DiscordOptions>>();
        options.Value.Returns(new DiscordOptions { QueueChannel = 123456 });
        var queueRepo = Substitute.For<IQueueRepository>();
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<QueueService>>();
        var dataPull = Substitute.For<IDataPullService>();
        _queueService = new QueueService(logger, options, queueRepo, _discordClient, _context, dataPull, new ContextOutbox(_context), Substitute.For<IQueueNotifier>());
        _notifier = Substitute.For<IQueueNotifier>();
        _handler = new MatchmakingStringMenuHandler(_context, _discordClient, _queueService, _notifier);
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
        var user = GrifballWebApp.Domain.Players.User.Seed(id: 1, discordUser: DiscordUser.FromDiscord(123, "123"), xboxUser: XboxUser.FromXbox(0, "GT"));
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
        var user = GrifballWebApp.Domain.Players.User.Seed(id: 1, discordUser: DiscordUser.FromDiscord(123, "123"), xboxUser: XboxUser.FromXbox(0, "GT"));
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
        var user = User.Seed(id: 1, discordUser: DiscordUser.FromDiscord(123, "123"), xboxUser: XboxUser.FromXbox(1, "GT"));
        var otherUser = User.Seed(id: 2, discordUser: DiscordUser.FromDiscord(456, "456"), xboxUser: XboxUser.FromXbox(2, "GT2"));
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
        var user = User.Seed(id: 1, discordUser: DiscordUser.FromDiscord(123, "123"), xboxUser: XboxUser.FromXbox(1, "GT"));
        var otherUser = User.Seed(id: 2, discordUser: DiscordUser.FromDiscord(456, "456"), xboxUser: XboxUser.FromXbox(2, "GT2"));
        var me = new MatchedPlayer(user).AlreadyKicked();
        var them = new MatchedPlayer(otherUser);
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
        var user = User.Seed(id: 1, discordUser: DiscordUser.FromDiscord(123, "123"), xboxUser: XboxUser.FromXbox(0, "GT"));
        var me = new MatchedPlayer(user);
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
        var user = User.Seed(id: 1, discordUser: DiscordUser.FromDiscord(123, "123"), xboxUser: XboxUser.FromXbox(1, "GT"));
        var otherUser = User.Seed(id: 2, discordUser: DiscordUser.FromDiscord(456, "456"), xboxUser: XboxUser.FromXbox(2, "GT2"), displayName: "TargetUser");
        var me = new MatchedPlayer(user);
        var them = new MatchedPlayer(otherUser).AlreadyKicked();
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
        var user = User.Seed(id: 1, discordUser: DiscordUser.FromDiscord(123, "123"), xboxUser: XboxUser.FromXbox(1, "GT"), displayName: "Voter");
        var otherUser = User.Seed(id: 2, discordUser: DiscordUser.FromDiscord(456, "456"), xboxUser: XboxUser.FromXbox(2, "GT2"), displayName: "TargetUser");
        var me = new MatchedPlayer(user);
        var them = new MatchedPlayer(otherUser);
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
        var user = User.Seed(id: 1, discordUser: DiscordUser.FromDiscord(123, "123"), xboxUser: XboxUser.FromXbox(1, "GT"), displayName: "Voter");
        var otherUser = User.Seed(id: 2, discordUser: DiscordUser.FromDiscord(456, "456"), xboxUser: XboxUser.FromXbox(2, "GT2"), displayName: "TargetUser");
        var me = new MatchedPlayer(user);
        var them = new MatchedPlayer(otherUser);
        var match = await SeedMatch([user, otherUser], [me], [them]);

        Assert.That(match.VoteToKick(me.UserID, them.UserID).Result, Is.EqualTo(KickVoteResult.Voted));
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
        var user = User.Seed(id: 1, discordUser: DiscordUser.FromDiscord(123, "123"), xboxUser: XboxUser.FromXbox(1, "GT"), displayName: "Voter");
        var otherUser = User.Seed(id: 2, discordUser: DiscordUser.FromDiscord(456, "456"), xboxUser: XboxUser.FromXbox(2, "GT2"), displayName: "TargetUser");
        var thirdUser = User.Seed(id: 3, discordUser: DiscordUser.FromDiscord(789, "789"), xboxUser: XboxUser.FromXbox(3, "GT3"), displayName: "Voter2");
        var me = new MatchedPlayer(user);
        var them = new MatchedPlayer(otherUser);
        var voter2 = new MatchedPlayer(thirdUser);
        var match = await SeedMatch([user, otherUser, thirdUser], [me, voter2], [them]);
        // Add a vote from voter2 (so threshold will be reached with this vote)
        Assert.That(match.VoteToKick(voter2.UserID, them.UserID).Result, Is.EqualTo(KickVoteResult.Voted));
        await _context.SaveChangesWithoutConstraints();
        _discordContext.User.Id.Returns(123UL); // user 1
        _discordContext.SelectedValues.Returns(new List<string> { "2" });
        // Act
        await _handler.VoteToKick(_discordContext, 1);
        // Assert: Target should be kicked
        var kicked = await _context.Set<MatchedPlayer>().FindAsync(them.Id);
        Assert.That(kicked, Is.Not.Null);
        Assert.That(kicked.Kicked, Is.True);
        await _discordContext.AssertSendResponse($"{them.ToDisplayName()} has been kicked");
        // Out of the match: their home page offers the queue again
        await _notifier.Received(1).SendStatusChanged(Arg.Is<IReadOnlyCollection<int>>(x => x.SequenceEqual(second)));
    }

    [Test]
    public async Task VoteToKick_A_Player_May_Vote_To_Kick_Two_Others_Each_Saved()
    {
        // Four players: one vote against each of two is no majority, and both are kept (the key has the target)
        var users = Enumerable.Range(1, 4).Select(i => User.Seed(
            id: i,
            discordUser: DiscordUser.FromDiscord(100 + i, $"d{i}"),
            xboxUser: XboxUser.FromXbox(i, $"GT{i}"),
            displayName: $"Player{i}")).ToArray();
        var players = users.Select(u => new MatchedPlayer(u)).ToArray();
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
        var votes = fresh.Set<MatchedKickVote>().Select(v => new { v.VoterMatchedPlayerId, v.KickMatchedPlayerId }).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(votes, Is.EquivalentTo(new[]
            {
                new { VoterMatchedPlayerId = players[0].Id, KickMatchedPlayerId = players[2].Id },
                new { VoterMatchedPlayerId = players[0].Id, KickMatchedPlayerId = players[3].Id },
            }));
            Assert.That(fresh.Set<MatchedPlayer>().Any(p => p.Kicked), Is.False);
            await _discordContext.AssertSendResponse("GT1 has voted to kick GT3");
            await _discordContext.AssertSendResponse("GT1 has voted to kick GT4");
        }
    }

    [Test]
    public async Task VoteToKick_Two_Voters_At_Once_Each_Short_Of_A_Majority_The_Second_To_Save_Kicks()
    {
        // Four players; the first already voted to kick the fourth
        var users = Enumerable.Range(1, 4).Select(i => User.Seed(
            id: i,
            discordUser: DiscordUser.FromDiscord(100 + i, $"d{i}"),
            xboxUser: XboxUser.FromXbox(i, $"GT{i}"),
            displayName: $"Player{i}")).ToArray();
        var players = users.Select(u => new MatchedPlayer(u)).ToArray();
        var match = await SeedMatch(users, [players[0], players[1]], [players[2], players[3]]);
        Assert.That(match.VoteToKick(players[0].UserID, players[3].UserID).Result, Is.EqualTo(KickVoteResult.Voted));
        await _context.SaveChangesWithoutConstraints();

        // The third player's vote, loaded with the first vote only; as it saves, the second player's vote (in a handler of its
        // own, which loaded the same) is saved first. Each counts 2 of 4.
        var second = Substitute.For<IDiscordStringMenuInteractionContext>();
        second.User.Id.Returns(102UL);
        second.SelectedValues.Returns(new List<string> { "4" });
        var options = new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(TestDatabase.Provider, _context.Database.GetConnectionString()!).Options;
        await using (var third = new SavesAnotherVoteFirst(options, async () =>
        {
            await using var ctx = _context.NewContextLike();
            await new MatchmakingStringMenuHandler(ctx, _discordClient, _queueService, _notifier).VoteToKick(second, 1);
        }))
        {
            _discordContext.User.Id.Returns(103UL);
            _discordContext.SelectedValues.Returns(new List<string> { "4" });
            await new MatchmakingStringMenuHandler(third, _discordClient, _queueService, _notifier).VoteToKick(_discordContext, 1);
        }

        await using var fresh = _context.NewContextLike();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await fresh.Set<MatchedKickVote>().CountAsync(), Is.EqualTo(3));
            Assert.That(await fresh.Set<MatchedPlayer>().Where(p => p.Kicked).Select(p => p.UserID).ToListAsync(), Is.EqualTo(new List<int> { 4 }),
                "Decided again on the saved votes: 3 of 4");
            await second.AssertSendResponse("GT2 has voted to kick GT4");
            await _discordContext.AssertSendResponse($"{players[3].ToDisplayName()} has been kicked");
            await _notifier.Received(1).SendStatusChanged(Arg.Is<IReadOnlyCollection<int>>(x => x.SequenceEqual(secondArray)));
        }
    }

    /// <summary>A handler's context whose first save waits for another handler's to finish first: the two at once.</summary>
    private sealed class SavesAnotherVoteFirst(DbContextOptions<GrifballContext> options, Func<Task> meanwhile) : GrifballContext(options)
    {
        private Func<Task>? _meanwhile = meanwhile;

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _meanwhile, null) is { } meanwhile)
                await meanwhile();
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }
}
