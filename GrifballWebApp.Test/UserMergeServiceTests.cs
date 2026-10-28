using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Profile;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
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
        var ex = Assert.ThrowsAsync<UserNotFoundException>(() => _service.Merge(999, mergeFromUser.Id, null, CancellationToken.None));
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
        var ex = Assert.ThrowsAsync<UserNotFoundException>(() => _service.Merge(mergeToUser.Id, 999, null, CancellationToken.None));
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

    // ---------- External logins ----------

    private async Task<(User To, User From)> TwoUsers(long? fromDiscord = null, long? toDiscord = null)
    {
        var to = new User { UserName = "mergeto", DisplayName = "Merge To", DiscordUserID = toDiscord };
        var from = new User { UserName = "mergefrom", DisplayName = "Merge From", DiscordUserID = fromDiscord };
        await _context.Users.AddRangeAsync(to, from);
        await _context.SaveChangesAsync();
        return (to, from);
    }

    private async Task AddLogin(int userId, string provider, string key)
    {
        _context.UserLogins.Add(new UserLogin { UserId = userId, LoginProvider = provider, ProviderKey = key, ProviderDisplayName = provider });
        await _context.SaveChangesAsync();
    }

    private async Task AddDiscordUser(long id)
    {
        _context.DiscordUsers.Add(new DiscordUser { DiscordUserID = id, DiscordUsername = "discord" + id });
        await _context.SaveChangesAsync();
    }

    private Task<List<UserLogin>> LoginsOf(int userId) =>
        _context.UserLogins.AsNoTracking().Where(l => l.UserId == userId).OrderBy(l => l.LoginProvider).ToListAsync();

    [Test]
    public async Task Merge_MovesAllExternalLogins_DeletesSource_AndTargetSignsInWithThem()
    {
        // The incident: a login-only account (no DiscordUserID) merged with TransferDiscord + DeleteMergeFrom.
        var (to, from) = await TwoUsers();
        await AddLogin(from.Id, "Discord", "111");
        await AddLogin(from.Id, "Google", "g-1");

        await _service.Merge(to.Id, from.Id, new MergeOptions { TransferDiscord = true, DeleteMergeFrom = true });

        var moved = await LoginsOf(to.Id);
        Assert.That(moved.Select(l => l.LoginProvider), Is.EqualTo(new[] { "Discord", "Google" }));
        Assert.That(await _context.Users.AsNoTracking().AnyAsync(u => u.Id == from.Id), Is.False);

        // The next "Sign in with Discord" finds the target instead of creating a new account.
        var userManager = IdentityTestHelpers.NewUserManager(_context);
        var signsInAs = await userManager.FindByLoginAsync("Discord", "111");
        Assert.That(signsInAs?.Id, Is.EqualTo(to.Id));
    }

    [Test]
    public async Task Merge_KeepsTargetsOwnLogins_AndAllowsSeveralForOneProvider()
    {
        var (to, from) = await TwoUsers();
        await AddLogin(to.Id, "Discord", "222");
        await AddLogin(from.Id, "Discord", "111");

        await _service.Merge(to.Id, from.Id);

        var keys = (await LoginsOf(to.Id)).Select(l => l.ProviderKey).Order();
        Assert.That(keys, Is.EqualTo(new[] { "111", "222" }));
    }

    [Test]
    public async Task Merge_TransferLoginsOff_KeepsLoginsOnSource()
    {
        var (to, from) = await TwoUsers();
        await AddLogin(from.Id, "Discord", "111");

        await _service.Merge(to.Id, from.Id, new MergeOptions { TransferLogins = false, DeleteMergeFrom = false });

        Assert.That(await LoginsOf(to.Id), Is.Empty);
        Assert.That((await LoginsOf(from.Id)).Single().ProviderKey, Is.EqualTo("111"));
    }

    [Test]
    public async Task Merge_TransferLoginsOff_WithDelete_RemovesLoginsWithSource()
    {
        var (to, from) = await TwoUsers();
        await AddLogin(from.Id, "Discord", "111");

        await _service.Merge(to.Id, from.Id, new MergeOptions { TransferLogins = false, DeleteMergeFrom = true });

        Assert.That(await _context.UserLogins.AsNoTracking().AnyAsync(), Is.False);
    }

    [Test]
    public async Task Merge_LinksDiscordFromLogin_WhenNeitherUserHasDiscordUserID()
    {
        await AddDiscordUser(111);
        var (to, from) = await TwoUsers();
        await AddLogin(from.Id, "Discord", "111");

        await _service.Merge(to.Id, from.Id);

        var target = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == to.Id);
        Assert.That(target.DiscordUserID, Is.EqualTo(111));
    }

    [Test]
    public async Task Merge_KeepsTargetsDiscordUserID_OverTheSourcesDiscordLogin()
    {
        await AddDiscordUser(111);
        await AddDiscordUser(222);
        var (to, from) = await TwoUsers(toDiscord: 222);
        await AddLogin(from.Id, "Discord", "111");

        await _service.Merge(to.Id, from.Id);

        var target = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == to.Id);
        Assert.That(target.DiscordUserID, Is.EqualTo(222));
    }

    [TestCase("111", false, true, TestName = "Merge_DoesNotLinkDiscord_WhenDiscordUserUnknown")]
    [TestCase("not-a-number", true, true, TestName = "Merge_DoesNotLinkDiscord_WhenLoginKeyIsNotAnId")]
    [TestCase("111", true, false, TestName = "Merge_DoesNotLinkDiscord_WhenTransferDiscordOff")]
    public async Task Merge_DoesNotLinkDiscordFromLogin(string key, bool knownDiscordUser, bool transferDiscord)
    {
        if (knownDiscordUser)
            await AddDiscordUser(111);
        var (to, from) = await TwoUsers();
        await AddLogin(from.Id, "Discord", key);

        await _service.Merge(to.Id, from.Id, new MergeOptions { TransferDiscord = transferDiscord });

        var target = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == to.Id);
        Assert.That(target.DiscordUserID, Is.Null);
    }

    [Test]
    public async Task Merge_DoesNotLinkDiscord_WhenAThirdUserHasIt()
    {
        await AddDiscordUser(111);
        var (to, from) = await TwoUsers();
        var third = new User { UserName = "third", DiscordUserID = 111 };
        _context.Users.Add(third);
        await _context.SaveChangesAsync();
        await AddLogin(from.Id, "Discord", "111");

        await _service.Merge(to.Id, from.Id);

        var target = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == to.Id);
        Assert.That(target.DiscordUserID, Is.Null);
    }

    [Test]
    public async Task Merge_TransferDiscord_WithoutDelete_ClearsSourceLink()
    {
        // Users.DiscordUserID is unique, so the source must let go of it when the target takes it.
        await AddDiscordUser(111);
        var (to, from) = await TwoUsers(fromDiscord: 111);

        await _service.Merge(to.Id, from.Id, new MergeOptions { DeleteMergeFrom = false });

        var users = await _context.Users.AsNoTracking().Where(u => u.Id == to.Id || u.Id == from.Id).ToDictionaryAsync(u => u.Id);
        Assert.Multiple(() =>
        {
            Assert.That(users[to.Id].DiscordUserID, Is.EqualTo(111));
            Assert.That(users[from.Id].DiscordUserID, Is.Null);
        });
    }

    [Test]
    public void Merge_IntoItself_Throws()
    {
        var ex = Assert.ThrowsAsync<ArgumentException>(() => _service.Merge(5, 5));
        Assert.That(ex!.Message, Does.Contain("itself"));
    }

    [Test]
    public async Task Merge_Twice_SecondFailsWithUserNotFound()
    {
        var (to, from) = await TwoUsers();
        await _service.Merge(to.Id, from.Id);

        var ex = Assert.ThrowsAsync<UserNotFoundException>(() => _service.Merge(to.Id, from.Id));
        Assert.That(ex!.UserId, Is.EqualTo(from.Id));
    }
}
