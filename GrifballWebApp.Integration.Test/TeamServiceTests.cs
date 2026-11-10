using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Infrastructure.Teams;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
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
        _service = new TeamService(_context, _publisher, new OutboxDraftChannel(_context, new ContextOutbox(_context)), new ContextDatabaseLocks(_context));
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
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));

        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var user1 = User.Seed(userName: "user1", displayName: "User 1");
        var user2 = User.Seed(userName: "user2", displayName: "User 2");
        var xboxUser1 = XboxUser.FromXbox(111, "Gamer1");
        var xboxUser2 = XboxUser.FromXbox(222, "Gamer2");

        _context.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        user1.TieToXbox(xboxUser1.XboxUserID);
        user2.TieToXbox(xboxUser2.XboxUserID);
        _context.XboxUsers.AddRange(xboxUser1, xboxUser2);
        await _context.SaveChangesAsync();

        var signup1 = SeasonSignup.Seed(season.SeasonID, user1.Id, timestamp: DateTime.UtcNow);
        var signup2 = SeasonSignup.Seed(season.SeasonID, user2.Id, timestamp: DateTime.UtcNow);

        _context.SeasonSignups.AddRange(signup1, signup2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPlayerPool(season.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Any(p => p.Name == "Gamer1"), Is.True);
            Assert.That(result.Any(p => p.Name == "Gamer2"), Is.True);
        }
    }

    [Test]
    public async Task GetPlayerPool_Should_ExcludePlayersOnTeams_When_PlayersAreAlreadyOnTeams()
    {
        // Arrange
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));

        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var user1 = User.Seed(userName: "user1", displayName: "User 1");
        var user2 = User.Seed(userName: "user2", displayName: "User 2");
        var xboxUser1 = XboxUser.FromXbox(111, "Gamer1");
        var xboxUser2 = XboxUser.FromXbox(222, "Gamer2");

        _context.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        user1.TieToXbox(xboxUser1.XboxUserID);
        user2.TieToXbox(xboxUser2.XboxUserID);
        _context.XboxUsers.AddRange(xboxUser1, xboxUser2);
        await _context.SaveChangesAsync();

        var signup1 = SeasonSignup.Seed(season.SeasonID, user1.Id, timestamp: DateTime.UtcNow);
        var signup2 = SeasonSignup.Seed(season.SeasonID, user2.Id, timestamp: DateTime.UtcNow);

        _context.SeasonSignups.AddRange(signup1, signup2);
        await _context.SaveChangesAsync();

        // Add user1 to a team
        var team = Team.Seed(seasonID: season.SeasonID, teamName: "Test Team");
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        await _context.AddPlayers(team, user1.Id);

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
        var season1 = Season.Seed(seasonName: "Season 1").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));

        var season2 = Season.Seed(seasonName: "Season 2").WithDates(seasonStart: DateTime.UtcNow.AddDays(40), seasonEnd: DateTime.UtcNow.AddDays(70));

        _context.Seasons.AddRange(season1, season2);
        await _context.SaveChangesAsync();

        var user1 = User.Seed(userName: "user1", displayName: "User 1");
        var user2 = User.Seed(userName: "user2", displayName: "User 2");
        var xboxUser1 = XboxUser.FromXbox(111, "Gamer1");
        var xboxUser2 = XboxUser.FromXbox(222, "Gamer2");

        _context.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        user1.TieToXbox(xboxUser1.XboxUserID);
        user2.TieToXbox(xboxUser2.XboxUserID);
        _context.XboxUsers.AddRange(xboxUser1, xboxUser2);
        await _context.SaveChangesAsync();

        var signup1 = SeasonSignup.Seed(season1.SeasonID, user1.Id, timestamp: DateTime.UtcNow);
        var signup2 = SeasonSignup.Seed(season2.SeasonID, user2.Id, timestamp: DateTime.UtcNow);

        _context.SeasonSignups.AddRange(signup1, signup2);
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
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));

        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var user = User.Seed(userName: "testuser", displayName: "Display Name");

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var signup = SeasonSignup.Seed(season.SeasonID, user.Id, timestamp: DateTime.UtcNow);

        _context.SeasonSignups.Add(signup);
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
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));

        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var user = User.Seed(userName: "testuser", displayName: "Display Name");
        var discordUser = DiscordUser.FromDiscord(12345, "DiscordUser");

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        user.LinkDiscord(discordUser.DiscordUserID);
        _context.DiscordUsers.Add(discordUser);
        await _context.SaveChangesAsync();

        var signup = SeasonSignup.Seed(season.SeasonID, user.Id, timestamp: DateTime.UtcNow);

        _context.SeasonSignups.Add(signup);
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
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var users = Enumerable.Range(1, 5).Select(i => User.Seed(userName: $"user{i}", displayName: $"User {i}")).ToList();
        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();
        _context.SeasonSignups.AddRange(users.Select(u => SeasonSignup.Seed(season.SeasonID, u.Id, teamName: $"Team {u.UserName}")));
        await _context.SaveChangesAsync();

        for (var i = 0; i < 4; i++)
            await _service.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = users[i].Id, OrderNumber = i + 1 }, resortOnly: false);

        var secondTeam = _context.Teams.Single(t => t.TeamName == "Team user2");
        _context.Set<TeamPlayer>().Add(TeamPlayer.Seed(teamID: secondTeam.TeamID, userID: users[4].Id, draftRound: 1));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act: remove captain #2. RemoveCaptainDto has no order number to send.
        await _service.RemoveCaptain(new RemoveCaptainDto { SeasonID = season.SeasonID, PersonID = users[1].Id });
        _context.ChangeTracker.Clear();

        // Assert
        var teams = await _service.GetTeams(season.SeasonID);
        var captainOrders = await _context.Set<TeamPlayer>()
            .Where(tp => tp.Team.SeasonID == season.SeasonID && tp.CaptainTeam != null)
            .OrderBy(tp => tp.DraftCaptainOrder)
            .Select(tp => new { tp.UserID, tp.DraftCaptainOrder })
            .ToListAsync();
        var pool = await _service.GetPlayerPool(season.SeasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams.Select(t => t.Captain.PersonID), Is.EqualTo(new[] { users[0].Id, users[2].Id, users[3].Id }));
            Assert.That(teams.Select(t => t.Captain.Order), Is.EqualTo(new int?[] { 1, 2, 3 }));
            Assert.That(captainOrders.Select(c => c.DraftCaptainOrder), Is.EqualTo(new int?[] { 1, 2, 3 }), "stored DraftCaptainOrder");
            Assert.That(_context.Teams.Any(t => t.TeamID == secondTeam.TeamID), Is.False, "removed captain's team is deleted");
            Assert.That(pool.Select(p => p.PersonID), Is.EquivalentTo(new[] { users[1].Id, users[4].Id }), "captain and their pick are back in the pool");
        }
    }
}
