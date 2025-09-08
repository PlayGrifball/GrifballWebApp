using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Services;
using Microsoft.Extensions.Options;
using NSubstitute;
using DiscordUser = GrifballWebApp.Database.Models.DiscordUser;
using GrifballWebApp.Server.Extensions;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class StringMenuInteractionsTests
{
    private GrifballContext _context;
    private IDiscordRestClient _discordClient;
    private QueueService _queueService;
    private StringMenuInteractions _stringMenuInteractions;
    private IDiscordStringMenuInteractionContext _discordContext;

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
        _queueService = new QueueService(logger, options, queueRepo, _discordClient, _context, dataPull);
        _stringMenuInteractions = new StringMenuInteractions(_context, _discordClient, _queueService);
        _discordContext = Substitute.For<IDiscordStringMenuInteractionContext>();
        var type = _stringMenuInteractions.GetType();
        var field = type.GetField("_discordContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field!.SetValue(_stringMenuInteractions, _discordContext);
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
        await _stringMenuInteractions.VoteToKick(1);
        // Assert
        await _discordContext.AssertSendResponse("You must set your gamertag first");
    }

    [Test]
    public async Task VoteToKick_Should_Reject_If_Match_Not_Found()
    {
        // Arrange
        var user = new GrifballWebApp.Database.Models.User { Id = 1, DiscordUser = new DiscordUser() { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { Gamertag = "GT" } };
        _context.Users.Add(user);
        await _context.SaveChangesWithoutContraints();
        _discordContext.User.Id.Returns(123UL);
        _discordContext.SelectedValues.Returns(new List<string> { "2" });
        // Act
        await _stringMenuInteractions.VoteToKick(999); // No such match
        // Assert
        await _discordContext.AssertSendResponse("This match does not exist");
    }

    [Test]
    public async Task VoteToKick_Should_Reject_If_Match_Is_Over()
    {
        // Arrange
        var user = new GrifballWebApp.Database.Models.User { Id = 1, DiscordUser = new DiscordUser() { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { Gamertag = "GT" } };
        var match = new MatchedMatch { Id = 1, Active = false, HomeTeam = new MatchedTeam { Players = [] }, AwayTeam = new MatchedTeam { Players = [] } };
        _context.Users.Add(user);
        await _context.SaveChangesWithoutContraints();
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutContraints();
        _discordContext.User.Id.Returns(123UL);
        _discordContext.SelectedValues.Returns(new List<string> { "2" });
        // Act
        await _stringMenuInteractions.VoteToKick(1);
        // Assert
        await _discordContext.AssertSendResponse("This match is over");
    }

    [Test]
    public async Task VoteToKick_Should_Reject_If_User_Not_In_Match()
    {
        // Arrange
        var user = new User { Id = 1, DiscordUser = new DiscordUser() { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "GT" } };
        var otherUser = new User { Id = 2, DiscordUser = new DiscordUser() { DiscordUserID = 456, DiscordUsername = "456" }, XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "GT2" } };
        var match = new MatchedMatch { Id = 1, Active = true, HomeTeam = new MatchedTeam { Players = [] }, AwayTeam = new MatchedTeam { Players = [] } };
        _context.Users.AddRange(user, otherUser);
        await _context.SaveChangesWithoutContraints();
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutContraints();
        _discordContext.User.Id.Returns(123UL);
        _discordContext.SelectedValues.Returns(new List<string> { "2" });
        // Act
        await _stringMenuInteractions.VoteToKick(1);
        // Assert
        await _discordContext.AssertSendResponse("You are not in this match");
    }

    [Test]
    public async Task VoteToKick_Should_Reject_If_User_Kicked()
    {
        // Arrange
        var user = new User { Id = 1, DiscordUser = new DiscordUser() { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "GT" } };
        var otherUser = new User { Id = 2, DiscordUser = new DiscordUser() { DiscordUserID = 456, DiscordUsername = "456" }, XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "GT2" } };
        
        var match = new MatchedMatch { Id = 1, Active = true, HomeTeam = new MatchedTeam { Players = [] }, AwayTeam = new MatchedTeam { Players = [] } };
        
        _context.Users.AddRange(user, otherUser);
        await _context.SaveChangesWithoutContraints();

        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutContraints();

        var me = new MatchedPlayer { Id = 10, UserID = 1, Kicked = true, User = user };
        var them = new MatchedPlayer { Id = 20, UserID = 2, Kicked = false, User = otherUser };
        match.HomeTeam.Players.Add(me);
        match.AwayTeam.Players.Add(them);
        _context.MatchedPlayers.AddRange(me, them);

        _discordContext.User.Id.Returns(123UL);
        _discordContext.SelectedValues.Returns(new List<string> { "2" });
        // Act
        await _stringMenuInteractions.VoteToKick(1);
        // Assert
        await _discordContext.AssertSendResponse("You have been kicked. You cannot vote.");
    }

    [Test]
    public async Task VoteToKick_Should_Reject_If_Target_Not_In_Match()
    {
        // Arrange
        var user = new User { Id = 1, DiscordUser = new DiscordUser() { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { Gamertag = "GT" } };
        var me = new MatchedPlayer { Id = 10, UserID = 1, Kicked = false, User = user };
        var match = new MatchedMatch { Id = 1, Active = true, HomeTeam = new MatchedTeam { Players = [] }, AwayTeam = new MatchedTeam { Players = [] } };
        _context.Users.Add(user);
        await _context.SaveChangesWithoutContraints();
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutContraints();
        _context.MatchedPlayers.Add(me);
        match.HomeTeam.Players.Add(me);
        await _context.SaveChangesWithoutContraints();
        _discordContext.User.Id.Returns(123UL);
        _discordContext.SelectedValues.Returns(new List<string> { "999" }); // Not in match
        // Act
        await _stringMenuInteractions.VoteToKick(1);
        // Assert
        await _discordContext.AssertSendResponse("They are not in this match");
    }

    [Test]
    public async Task VoteToKick_Should_Reject_If_Target_Already_Kicked()
    {
        // Arrange
        var user = new User { Id = 1, DiscordUser = new DiscordUser() { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "GT" } };
        var otherUser = new User { Id = 2, DiscordUser = new DiscordUser() { DiscordUserID = 456, DiscordUsername = "456" }, XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "GT2" }, DisplayName = "TargetUser" };
        
        var match = new MatchedMatch { Id = 1, Active = true, HomeTeam = new MatchedTeam { Players = [] }, AwayTeam = new MatchedTeam { Players = [] } };
        _context.Users.AddRange(user, otherUser);
        await _context.SaveChangesWithoutContraints();
        
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutContraints();

        var me = new MatchedPlayer { Id = 10, UserID = 1, Kicked = false, User = user };
        var them = new MatchedPlayer { Id = 20, UserID = 2, Kicked = true, User = otherUser };
        match.HomeTeam.Players.Add(me);
        match.AwayTeam.Players.Add(them);
        _context.MatchedPlayers.AddRange(me, them);
        await _context.SaveChangesWithoutContraints();
        _discordContext.User.Id.Returns(123UL);
        _discordContext.SelectedValues.Returns(new List<string> { "2" });
        // Act
        await _stringMenuInteractions.VoteToKick(1);
        // Assert
        await _discordContext.AssertSendResponse($"{them.ToDisplayName()} has already been kicked");
    }

    [Test]
    public async Task VoteToKick_Should_Succeed_On_First_Vote()
    {
        // Arrange
        var user = new User { Id = 1, DiscordUser = new DiscordUser() { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "GT" }, DisplayName = "Voter" };
        var otherUser = new User { Id = 2, DiscordUser = new DiscordUser() { DiscordUserID = 456, DiscordUsername = "456" }, XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "GT2" }, DisplayName = "TargetUser" };
        
        var match = new MatchedMatch { Id = 1, Active = true, HomeTeam = new MatchedTeam { Players = [] }, AwayTeam = new MatchedTeam { Players = [] } };

        _context.Users.AddRange(user, otherUser);
        await _context.SaveChangesWithoutContraints();

        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutContraints();

        var me = new MatchedPlayer { Id = 10, UserID = 1, Kicked = false, User = user };
        var them = new MatchedPlayer { Id = 20, UserID = 2, Kicked = false, User = otherUser };
        match.HomeTeam.Players.Add(me);
        match.AwayTeam.Players.Add(them);
        _context.MatchedPlayers.AddRange(me, them);
        await _context.SaveChangesWithoutContraints();

        _discordContext.User.Id.Returns(123UL);
        _discordContext.SelectedValues.Returns(new List<string> { "2" });
        // Act
        await _stringMenuInteractions.VoteToKick(1);
        // Assert
        await _discordContext.Interaction.ReceivedWithAnyArgs().SendResponseAsync(default!);
    }

    [Test]
    public async Task VoteToKick_Should_Reject_If_Already_Voted()
    {
        // Arrange
        var user = new User { Id = 1, DiscordUser = new DiscordUser() { DiscordUserID = 123, DiscordUsername = "123" }, XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "GT" }, DisplayName = "Voter" };
        var otherUser = new User { Id = 2, DiscordUser = new DiscordUser() { DiscordUserID = 456, DiscordUsername = "456" }, XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "GT2" }, DisplayName = "TargetUser" };
        var match = new MatchedMatch { Id = 1, Active = true, HomeTeam = new MatchedTeam { Players = [] }, AwayTeam = new MatchedTeam { Players = [] } };
        var vote = new MatchedKickVote { MatchId = 1, VoterMatchedPlayerId = 10, KickMatchedPlayerId = 20 };

        _context.Users.AddRange(user, otherUser);
        await _context.SaveChangesWithoutContraints();
        
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesWithoutContraints();

        var me = new MatchedPlayer { Id = 10, UserID = 1, Kicked = false, User = user };
        var them = new MatchedPlayer { Id = 20, UserID = 2, Kicked = false, User = otherUser };
        match.HomeTeam.Players.Add(me);
        match.AwayTeam.Players.Add(them);
        _context.MatchedPlayers.AddRange(me, them);
        await _context.SaveChangesWithoutContraints();

        _context.MatchedKickVotes.Add(vote);
        await _context.SaveChangesWithoutContraints();

        _discordContext.User.Id.Returns(123UL);
        _discordContext.SelectedValues.Returns(new List<string> { "2" });

        // Act
        await _stringMenuInteractions.VoteToKick(1);
        // Assert
        await _discordContext.AssertSendResponse($"You have already voted to kick {them.ToDisplayName()}");
    }
}
