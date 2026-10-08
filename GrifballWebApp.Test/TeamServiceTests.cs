using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Teams;
using MediatR;
using Microsoft.EntityFrameworkCore;
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

    [Test]
    public async Task RemoveCaptain_Should_ResequenceRemainingCaptains_And_ReturnTeamToPool_When_SecondOfFourIsRemoved()
    {
        // Arrange: four captains in draft order 1-4, and captain #2 has drafted one player.
        var season = new Season { SeasonName = "Test Season", SeasonStart = DateTime.UtcNow, SeasonEnd = DateTime.UtcNow.AddDays(30) };
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var users = Enumerable.Range(1, 5).Select(i => new User { UserName = $"user{i}", DisplayName = $"User {i}" }).ToList();
        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();
        await _context.SeasonSignups.AddRangeAsync(users.Select(u => new SeasonSignup { UserID = u.Id, SeasonID = season.SeasonID, Timestamp = DateTime.UtcNow, TeamName = $"Team {u.UserName}" }));
        await _context.SaveChangesAsync();

        for (var i = 0; i < 4; i++)
            await _service.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = users[i].Id, OrderNumber = i + 1 }, resortOnly: false);

        var secondTeam = _context.Teams.Single(t => t.TeamName == "Team user2");
        await _context.TeamPlayers.AddAsync(new TeamPlayer { TeamID = secondTeam.TeamID, UserID = users[4].Id, DraftRound = 1 });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act: remove captain #2. RemoveCaptainDto has no order number to send.
        await _service.RemoveCaptain(new RemoveCaptainDto { SeasonID = season.SeasonID, PersonID = users[1].Id });
        _context.ChangeTracker.Clear();

        // Assert
        var teams = await _service.GetTeams(season.SeasonID);
        var captainOrders = await _context.TeamPlayers
            .Where(tp => tp.Team.SeasonID == season.SeasonID && tp.CaptainTeam != null)
            .OrderBy(tp => tp.DraftCaptainOrder)
            .Select(tp => new { tp.UserID, tp.DraftCaptainOrder })
            .ToListAsync();
        var pool = await _service.GetPlayerPool(season.SeasonID);

        Assert.Multiple(() =>
        {
            Assert.That(teams.Select(t => t.Captain.PersonID), Is.EqualTo(new[] { users[0].Id, users[2].Id, users[3].Id }));
            Assert.That(teams.Select(t => t.Captain.Order), Is.EqualTo(new int?[] { 1, 2, 3 }));
            Assert.That(captainOrders.Select(c => c.DraftCaptainOrder), Is.EqualTo(new int?[] { 1, 2, 3 }), "stored DraftCaptainOrder");
            Assert.That(_context.Teams.Any(t => t.TeamID == secondTeam.TeamID), Is.False, "removed captain's team is deleted");
            Assert.That(pool.Select(p => p.PersonID), Is.EquivalentTo(new[] { users[1].Id, users[4].Id }), "captain and their pick are back in the pool");
        });
    }

    [Test]
    public void RemoveCaptainDto_Should_IgnoreUnknownOrderNumber_When_BoundFromWebJson()
    {
        // Older clients posted RemoveCaptain with a CaptainPlacementDto shape that included
        // "orderNumber": 0. ASP.NET Core binds bodies with JsonSerializerDefaults.Web, which
        // skips unknown members, so the field never reaches TeamService.RemoveCaptain.
        var dto = System.Text.Json.JsonSerializer.Deserialize<RemoveCaptainDto>(
            """{"seasonID":7,"personID":201,"orderNumber":0}""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.That(dto, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(dto!.SeasonID, Is.EqualTo(7));
            Assert.That(dto.PersonID, Is.EqualTo(201));
            Assert.That(typeof(RemoveCaptainDto).GetProperty("OrderNumber"), Is.Null);
        });
    }
}
