using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Queries;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UserMergeServiceTests
{
    private static readonly string[] DiscordThenGoogle = ["Discord", "Google"];
    private static readonly string[] BothKeys = ["111", "222"];

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
    public async Task Merge_Should_ThrowException_When_MergeToUserDoesNotExist()
    {
        // Arrange
        var mergeFromUser = User.Seed(userName: "mergefrom", displayName: "Merge From");
        _context.Users.Add(mergeFromUser);
        _context.SaveChanges();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UserNotFoundException>(() => _service.Merge(999, mergeFromUser.Id, null, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("Did not find user 999"));
    }

    [Test]
    public async Task Merge_Should_ThrowException_When_MergeFromUserDoesNotExist()
    {
        // Arrange
        var mergeToUser = User.Seed(userName: "mergeto", displayName: "Merge To");
        _context.Users.Add(mergeToUser);
        _context.SaveChanges();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UserNotFoundException>(() => _service.Merge(mergeToUser.Id, 999, null, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("Did not find user 999"));
    }

    [Test]
    public async Task Merge_Should_TransferDiscordUserID_When_TransferDiscordIsTrue()
    {
        // Arrange
        var discordUser = DiscordUser.FromDiscord(12345, "TestDiscord");
        _context.DiscordUsers.Add(discordUser);
        await _context.SaveChangesAsync();

        var mergeToUser = User.Seed(userName: "mergeto", displayName: "Merge To");
        var mergeFromUser = User.Seed(userName: "mergefrom", displayName: "Merge From", discordUserID: discordUser.DiscordUserID);

        _context.Users.AddRange(mergeToUser, mergeFromUser);
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
        var xboxUser = XboxUser.FromXbox(12345, "TestGamer");
        _context.XboxUsers.Add(xboxUser);
        await _context.SaveChangesAsync();

        var mergeToUser = User.Seed(userName: "mergeto", displayName: "Merge To");
        var mergeFromUser = User.Seed(userName: "mergefrom", displayName: "Merge From", xboxUserID: xboxUser.XboxUserID);

        _context.Users.AddRange(mergeToUser, mergeFromUser);
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
        var mergeToUser = User.Seed(userName: "mergeto", displayName: "Merge To");
        var mergeFromUser = User.Seed(userName: "mergefrom", displayName: "Merge From");

        _context.Users.AddRange(mergeToUser, mergeFromUser);
        await _context.SaveChangesAsync();

        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var team = Team.Seed(seasonID: season.SeasonID, teamName: "Test Team");
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        await _context.AddPlayers(team, mergeFromUser.Id);

        var options = new MergeOptions { TransferTeamPlayers = true };

        // Act
        await _service.Merge(mergeToUser.Id, mergeFromUser.Id, options, CancellationToken.None);

        // Assert
        var teamPlayerID = team.TeamPlayers.Single().TeamPlayerID;
        _context.ChangeTracker.Clear();
        var updatedTeamPlayer = await _context.Teams.Rosters().FirstOrDefaultAsync(tp => tp.TeamPlayerID == teamPlayerID);
        Assert.That(updatedTeamPlayer!.UserID, Is.EqualTo(mergeToUser.Id));
    }

    [Test]
    public async Task Merge_Should_TransferSignups_When_TransferSignupsIsTrue()
    {
        // Arrange
        var mergeToUser = User.Seed(userName: "mergeto", displayName: "Merge To");
        var mergeFromUser = User.Seed(userName: "mergefrom", displayName: "Merge From");

        _context.Users.AddRange(mergeToUser, mergeFromUser);
        await _context.SaveChangesAsync();

        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var signup = SeasonSignup.Seed(season.SeasonID, mergeFromUser.Id, timestamp: DateTime.UtcNow);
        _context.SeasonSignups.Add(signup);
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
        var mergeToUser = User.Seed(userName: "mergeto", displayName: "Merge To");
        var mergeFromUser = User.Seed(userName: "mergefrom", displayName: "Merge From");

        _context.Users.AddRange(mergeToUser, mergeFromUser);
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
        var mergeToUser = User.Seed(userName: "mergeto", displayName: "Merge To");
        var mergeFromUser = User.Seed(userName: "mergefrom", displayName: "Merge From");

        _context.Users.AddRange(mergeToUser, mergeFromUser);
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
        var discordUser = DiscordUser.FromDiscord(12345, "TestDiscord");
        _context.DiscordUsers.Add(discordUser);
        await _context.SaveChangesAsync();

        var mergeToUser = User.Seed(userName: "mergeto", displayName: "Merge To");
        var mergeFromUser = User.Seed(userName: "mergefrom", displayName: "Merge From", discordUserID: discordUser.DiscordUserID);

        _context.Users.AddRange(mergeToUser, mergeFromUser);
        await _context.SaveChangesAsync();

        // Act
        await _service.Merge(mergeToUser.Id, mergeFromUser.Id, null, CancellationToken.None);

        // Assert - default options should transfer Discord and delete mergeFrom
        var updatedMergeTo = await _context.Users.FindAsync(mergeToUser.Id);
        var deletedUser = await _context.Users.FindAsync(mergeFromUser.Id);
        
        using (Assert.EnterMultipleScope())
        {
            Assert.That(updatedMergeTo!.DiscordUserID, Is.EqualTo(discordUser.DiscordUserID));
            Assert.That(deletedUser, Is.Null);
        }
    }

    [Test]
    public async Task Merge_Should_DeleteUserRoles_When_Merging()
    {
        // Arrange
        var role = new Role { Name = "TestRole" };
        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        var mergeToUser = User.Seed(userName: "mergeto", displayName: "Merge To");
        var mergeFromUser = User.Seed(userName: "mergefrom", displayName: "Merge From");

        _context.Users.AddRange(mergeToUser, mergeFromUser);
        await _context.SaveChangesAsync();

        var userRole = new UserRole { UserId = mergeFromUser.Id, RoleId = role.Id };
        _context.UserRoles.Add(userRole);
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
        var to = User.Seed(userName: "mergeto", displayName: "Merge To", discordUserID: toDiscord);
        var from = User.Seed(userName: "mergefrom", displayName: "Merge From", discordUserID: fromDiscord);
        _context.Users.AddRange(to, from);
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
        _context.DiscordUsers.Add(DiscordUser.FromDiscord(id, "discord" + id));
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(moved.Select(l => l.LoginProvider), Is.EqualTo(DiscordThenGoogle));
            Assert.That(await _context.Users.AsNoTracking().AnyAsync(u => u.Id == from.Id), Is.False);
        }

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
        Assert.That(keys, Is.EqualTo(BothKeys));
    }

    [Test]
    public async Task Merge_TransferLoginsOff_KeepsLoginsOnSource()
    {
        var (to, from) = await TwoUsers();
        await AddLogin(from.Id, "Discord", "111");

        await _service.Merge(to.Id, from.Id, new MergeOptions { TransferLogins = false, DeleteMergeFrom = false });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await LoginsOf(to.Id), Is.Empty);
            Assert.That((await LoginsOf(from.Id)).Single().ProviderKey, Is.EqualTo("111"));
        }
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
        var third = User.Seed(userName: "third", discordUserID: 111);
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(users[to.Id].DiscordUserID, Is.EqualTo(111));
            Assert.That(users[from.Id].DiscordUserID, Is.Null);
        }
    }

    [Test]
    public async Task Merge_IntoItself_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.Merge(5, 5));
        Assert.That(ex!.Message, Does.Contain("itself"));
    }

    [Test]
    public async Task Merge_Twice_SecondFailsWithUserNotFound()
    {
        var (to, from) = await TwoUsers();
        await _service.Merge(to.Id, from.Id);

        var ex = await Assert.ThrowsAsync<UserNotFoundException>(() => _service.Merge(to.Id, from.Id));
        Assert.That(ex!.UserId, Is.EqualTo(from.Id));
    }
}
