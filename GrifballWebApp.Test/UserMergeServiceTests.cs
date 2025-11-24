using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Profile;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class UserMergeServiceTests
{
    private GrifballContext _context;
    private UserMergeService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new UserMergeService(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public void Merge_Should_ThrowException_When_MergeToUserDoesNotExist()
    {
        // Arrange
        var mergeFromUser = new User { UserName = "mergefrom", DisplayName = "Merge From" };
        _context.Users.Add(mergeFromUser);
        _context.SaveChanges();

        // Act & Assert
        var ex = Assert.ThrowsAsync<Exception>(() => _service.Merge(999, mergeFromUser.Id, null, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("Did not find user 999"));
    }

    [Test]
    public void Merge_Should_ThrowException_When_MergeFromUserDoesNotExist()
    {
        // Arrange
        var mergeToUser = new User { UserName = "mergeto", DisplayName = "Merge To" };
        _context.Users.Add(mergeToUser);
        _context.SaveChanges();

        // Act & Assert
        var ex = Assert.ThrowsAsync<Exception>(() => _service.Merge(mergeToUser.Id, 999, null, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("Did not find user 999"));
    }

    [Test]
    public async Task Merge_Should_TransferDiscordUserID_When_TransferDiscordIsTrue()
    {
        // Arrange
        var discordUser = new DiscordUser { DiscordUserID = 12345, DiscordUsername = "TestDiscord" };
        await _context.DiscordUsers.AddAsync(discordUser);
        await _context.SaveChangesAsync();

        var mergeToUser = new User { UserName = "mergeto", DisplayName = "Merge To" };
        var mergeFromUser = new User { UserName = "mergefrom", DisplayName = "Merge From", DiscordUserID = discordUser.DiscordUserID };

        await _context.Users.AddRangeAsync(mergeToUser, mergeFromUser);
        await _context.SaveChangesAsync();

        var options = new MergeOptions { TransferDiscord = true };

        // Act
        await _service.Merge(mergeToUser.Id, mergeFromUser.Id, options, CancellationToken.None);

        // Assert
        var updatedMergeTo = await _context.Users.FindAsync(mergeToUser.Id);
        Assert.That(updatedMergeTo!.DiscordUserID, Is.EqualTo(discordUser.DiscordUserID));
    }

    [Test]
    public async Task Merge_Should_TransferXboxID_When_TransferXboxIsTrue()
    {
        // Arrange
        var xboxUser = new XboxUser { Gamertag = "TestGamer", XboxUserID = 12345 };
        await _context.XboxUsers.AddAsync(xboxUser);
        await _context.SaveChangesAsync();

        var mergeToUser = new User { UserName = "mergeto", DisplayName = "Merge To" };
        var mergeFromUser = new User { UserName = "mergefrom", DisplayName = "Merge From", XboxUserID = xboxUser.XboxUserID };

        await _context.Users.AddRangeAsync(mergeToUser, mergeFromUser);
        await _context.SaveChangesAsync();

        var options = new MergeOptions { TransferXbox = true };

        // Act
        await _service.Merge(mergeToUser.Id, mergeFromUser.Id, options, CancellationToken.None);

        // Assert
        var updatedMergeTo = await _context.Users.FindAsync(mergeToUser.Id);
        Assert.That(updatedMergeTo!.XboxUserID, Is.EqualTo(xboxUser.XboxUserID));
    }

    [Test]
    public async Task Merge_Should_TransferTeamPlayers_When_TransferTeamPlayersIsTrue()
    {
        // Arrange
        var mergeToUser = new User { UserName = "mergeto", DisplayName = "Merge To" };
        var mergeFromUser = new User { UserName = "mergefrom", DisplayName = "Merge From" };

        await _context.Users.AddRangeAsync(mergeToUser, mergeFromUser);
        await _context.SaveChangesAsync();

        var season = new Season { SeasonName = "Test Season", SeasonStart = DateTime.UtcNow, SeasonEnd = DateTime.UtcNow.AddDays(30) };
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var team = new Team { SeasonID = season.SeasonID, TeamName = "Test Team" };
        await _context.Teams.AddAsync(team);
        await _context.SaveChangesAsync();

        var teamPlayer = new TeamPlayer { UserID = mergeFromUser.Id, TeamID = team.TeamID };
        await _context.TeamPlayers.AddAsync(teamPlayer);
        await _context.SaveChangesAsync();

        var options = new MergeOptions { TransferTeamPlayers = true };

        // Act
        await _service.Merge(mergeToUser.Id, mergeFromUser.Id, options, CancellationToken.None);

        // Assert
        var updatedTeamPlayer = await _context.TeamPlayers.FirstOrDefaultAsync(tp => tp.TeamPlayerID == teamPlayer.TeamPlayerID);
        Assert.That(updatedTeamPlayer!.UserID, Is.EqualTo(mergeToUser.Id));
    }

    [Test]
    public async Task Merge_Should_TransferSignups_When_TransferSignupsIsTrue()
    {
        // Arrange
        var mergeToUser = new User { UserName = "mergeto", DisplayName = "Merge To" };
        var mergeFromUser = new User { UserName = "mergefrom", DisplayName = "Merge From" };

        await _context.Users.AddRangeAsync(mergeToUser, mergeFromUser);
        await _context.SaveChangesAsync();

        var season = new Season { SeasonName = "Test Season", SeasonStart = DateTime.UtcNow, SeasonEnd = DateTime.UtcNow.AddDays(30) };
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var signup = new SeasonSignup { UserID = mergeFromUser.Id, SeasonID = season.SeasonID, Timestamp = DateTime.UtcNow };
        await _context.SeasonSignups.AddAsync(signup);
        await _context.SaveChangesAsync();

        var options = new MergeOptions { TransferSignups = true };

        // Act
        await _service.Merge(mergeToUser.Id, mergeFromUser.Id, options, CancellationToken.None);

        // Assert
        var updatedSignup = await _context.SeasonSignups.FirstOrDefaultAsync(s => s.SeasonSignupID == signup.SeasonSignupID);
        Assert.That(updatedSignup!.UserID, Is.EqualTo(mergeToUser.Id));
    }

    [Test]
    public async Task Merge_Should_DeleteMergeFromUser_When_DeleteMergeFromIsTrue()
    {
        // Arrange
        var mergeToUser = new User { UserName = "mergeto", DisplayName = "Merge To" };
        var mergeFromUser = new User { UserName = "mergefrom", DisplayName = "Merge From" };

        await _context.Users.AddRangeAsync(mergeToUser, mergeFromUser);
        await _context.SaveChangesAsync();

        var options = new MergeOptions { DeleteMergeFrom = true };

        // Act
        await _service.Merge(mergeToUser.Id, mergeFromUser.Id, options, CancellationToken.None);

        // Assert
        var deletedUser = await _context.Users.FindAsync(mergeFromUser.Id);
        Assert.That(deletedUser, Is.Null);
    }

    [Test]
    public async Task Merge_Should_NotDeleteMergeFromUser_When_DeleteMergeFromIsFalse()
    {
        // Arrange
        var mergeToUser = new User { UserName = "mergeto", DisplayName = "Merge To" };
        var mergeFromUser = new User { UserName = "mergefrom", DisplayName = "Merge From" };

        await _context.Users.AddRangeAsync(mergeToUser, mergeFromUser);
        await _context.SaveChangesAsync();

        var options = new MergeOptions { DeleteMergeFrom = false };

        // Act
        await _service.Merge(mergeToUser.Id, mergeFromUser.Id, options, CancellationToken.None);

        // Assert
        var stillExistsUser = await _context.Users.FindAsync(mergeFromUser.Id);
        Assert.That(stillExistsUser, Is.Not.Null);
    }

    [Test]
    public async Task Merge_Should_UseDefaultOptions_When_OptionsIsNull()
    {
        // Arrange
        var discordUser = new DiscordUser { DiscordUserID = 12345, DiscordUsername = "TestDiscord" };
        await _context.DiscordUsers.AddAsync(discordUser);
        await _context.SaveChangesAsync();

        var mergeToUser = new User { UserName = "mergeto", DisplayName = "Merge To" };
        var mergeFromUser = new User { UserName = "mergefrom", DisplayName = "Merge From", DiscordUserID = discordUser.DiscordUserID };

        await _context.Users.AddRangeAsync(mergeToUser, mergeFromUser);
        await _context.SaveChangesAsync();

        // Act
        await _service.Merge(mergeToUser.Id, mergeFromUser.Id, null, CancellationToken.None);

        // Assert - default options should transfer Discord and delete mergeFrom
        var updatedMergeTo = await _context.Users.FindAsync(mergeToUser.Id);
        var deletedUser = await _context.Users.FindAsync(mergeFromUser.Id);
        
        Assert.Multiple(() =>
        {
            Assert.That(updatedMergeTo!.DiscordUserID, Is.EqualTo(discordUser.DiscordUserID));
            Assert.That(deletedUser, Is.Null);
        });
    }

    [Test]
    public async Task Merge_Should_DeleteUserRoles_When_Merging()
    {
        // Arrange
        var role = new Role { Name = "TestRole" };
        await _context.Roles.AddAsync(role);
        await _context.SaveChangesAsync();

        var mergeToUser = new User { UserName = "mergeto", DisplayName = "Merge To" };
        var mergeFromUser = new User { UserName = "mergefrom", DisplayName = "Merge From" };

        await _context.Users.AddRangeAsync(mergeToUser, mergeFromUser);
        await _context.SaveChangesAsync();

        var userRole = new UserRole { UserId = mergeFromUser.Id, RoleId = role.Id };
        await _context.UserRoles.AddAsync(userRole);
        await _context.SaveChangesAsync();

        // Act
        await _service.Merge(mergeToUser.Id, mergeFromUser.Id, null, CancellationToken.None);

        // Assert
        var deletedRole = await _context.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == mergeFromUser.Id && ur.RoleId == role.Id);
        Assert.That(deletedRole, Is.Null);
    }
}
