using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Teams;
using MediatR;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TeamServiceTests
{
    private GrifballContext _context;
    private IPublisher _publisher;
    private TeamService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _publisher = Substitute.For<IPublisher>();
        _service = new TeamService(_context, _publisher);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task GetTeams_Should_ReturnEmptyList_When_NoTeamsExist()
    {
        // Arrange
        const int seasonId = 1;

        // Act
        var result = await _service.GetTeams(seasonId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetPlayerPool_Should_ReturnEmptyList_When_NoSignupsExist()
    {
        // Arrange
        const int seasonId = 1;

        // Act
        var result = await _service.GetPlayerPool(seasonId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetPlayerPool_Should_ReturnSignedUpPlayers_When_PlayersAreSignedUp()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var user1 = new User { UserName = "user1", DisplayName = "User 1" };
        var user2 = new User { UserName = "user2", DisplayName = "User 2" };
        var xboxUser1 = new XboxUser { Gamertag = "Gamer1", XboxUserID = 111 };
        var xboxUser2 = new XboxUser { Gamertag = "Gamer2", XboxUserID = 222 };

        await _context.Users.AddRangeAsync(user1, user2);
        await _context.SaveChangesAsync();

        user1.XboxUserID = xboxUser1.XboxUserID;
        user2.XboxUserID = xboxUser2.XboxUserID;
        await _context.XboxUsers.AddRangeAsync(xboxUser1, xboxUser2);
        await _context.SaveChangesAsync();

        var signup1 = new SeasonSignup { UserID = user1.Id, SeasonID = season.SeasonID, Timestamp = DateTime.UtcNow };
        var signup2 = new SeasonSignup { UserID = user2.Id, SeasonID = season.SeasonID, Timestamp = DateTime.UtcNow };

        await _context.SeasonSignups.AddRangeAsync(signup1, signup2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPlayerPool(season.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(result.Any(p => p.Name == "Gamer1"), Is.True);
            Assert.That(result.Any(p => p.Name == "Gamer2"), Is.True);
        });
    }

    [Test]
    public async Task GetPlayerPool_Should_ExcludePlayersOnTeams_When_PlayersAreAlreadyOnTeams()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var user1 = new User { UserName = "user1", DisplayName = "User 1" };
        var user2 = new User { UserName = "user2", DisplayName = "User 2" };
        var xboxUser1 = new XboxUser { Gamertag = "Gamer1", XboxUserID = 111 };
        var xboxUser2 = new XboxUser { Gamertag = "Gamer2", XboxUserID = 222 };

        await _context.Users.AddRangeAsync(user1, user2);
        await _context.SaveChangesAsync();

        user1.XboxUserID = xboxUser1.XboxUserID;
        user2.XboxUserID = xboxUser2.XboxUserID;
        await _context.XboxUsers.AddRangeAsync(xboxUser1, xboxUser2);
        await _context.SaveChangesAsync();

        var signup1 = new SeasonSignup { UserID = user1.Id, SeasonID = season.SeasonID, Timestamp = DateTime.UtcNow };
        var signup2 = new SeasonSignup { UserID = user2.Id, SeasonID = season.SeasonID, Timestamp = DateTime.UtcNow };

        await _context.SeasonSignups.AddRangeAsync(signup1, signup2);
        await _context.SaveChangesAsync();

        // Add user1 to a team
        var team = new Team { SeasonID = season.SeasonID, TeamName = "Test Team" };
        await _context.Teams.AddAsync(team);
        await _context.SaveChangesAsync();

        var teamPlayer = new TeamPlayer { UserID = user1.Id, TeamID = team.TeamID };
        await _context.TeamPlayers.AddAsync(teamPlayer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPlayerPool(season.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Gamer2"));
    }

    [Test]
    public async Task GetPlayerPool_Should_OnlyReturnPlayersFromSpecifiedSeason()
    {
        // Arrange
        var season1 = new Season
        {
            SeasonName = "Season 1",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        var season2 = new Season
        {
            SeasonName = "Season 2",
            SeasonStart = DateTime.UtcNow.AddDays(40),
            SeasonEnd = DateTime.UtcNow.AddDays(70)
        };

        await _context.Seasons.AddRangeAsync(season1, season2);
        await _context.SaveChangesAsync();

        var user1 = new User { UserName = "user1", DisplayName = "User 1" };
        var user2 = new User { UserName = "user2", DisplayName = "User 2" };
        var xboxUser1 = new XboxUser { Gamertag = "Gamer1", XboxUserID = 111 };
        var xboxUser2 = new XboxUser { Gamertag = "Gamer2", XboxUserID = 222 };

        await _context.Users.AddRangeAsync(user1, user2);
        await _context.SaveChangesAsync();

        user1.XboxUserID = xboxUser1.XboxUserID;
        user2.XboxUserID = xboxUser2.XboxUserID;
        await _context.XboxUsers.AddRangeAsync(xboxUser1, xboxUser2);
        await _context.SaveChangesAsync();

        var signup1 = new SeasonSignup { UserID = user1.Id, SeasonID = season1.SeasonID, Timestamp = DateTime.UtcNow };
        var signup2 = new SeasonSignup { UserID = user2.Id, SeasonID = season2.SeasonID, Timestamp = DateTime.UtcNow };

        await _context.SeasonSignups.AddRangeAsync(signup1, signup2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPlayerPool(season1.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Gamer1"));
    }

    [Test]
    public async Task GetPlayerPool_Should_UseDisplayName_When_NoXboxOrDiscordUser()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var user = new User { UserName = "testuser", DisplayName = "Display Name" };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var signup = new SeasonSignup { UserID = user.Id, SeasonID = season.SeasonID, Timestamp = DateTime.UtcNow };

        await _context.SeasonSignups.AddAsync(signup);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPlayerPool(season.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Display Name"));
    }

    [Test]
    public async Task GetPlayerPool_Should_UseDiscordUsername_When_NoXboxUser()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var user = new User { UserName = "testuser", DisplayName = "Display Name" };
        var discordUser = new DiscordUser { DiscordUserID = 12345, DiscordUsername = "DiscordUser" };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        user.DiscordUserID = discordUser.DiscordUserID;
        await _context.DiscordUsers.AddAsync(discordUser);
        await _context.SaveChangesAsync();

        var signup = new SeasonSignup { UserID = user.Id, SeasonID = season.SeasonID, Timestamp = DateTime.UtcNow };

        await _context.SeasonSignups.AddAsync(signup);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPlayerPool(season.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("DiscordUser"));
    }
}
