using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Application.UserManagement;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

[TestFixture]
internal sealed class UserManagementServiceUnitTests : ApplicationDbContextTests
{
    private IGetsertXboxUserService _xboxService = null!;
    private UserManager<User> _userManager = null!;
    private UserManagementService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _xboxService = Substitute.For<IGetsertXboxUserService>();
        _userManager = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(), null!, null!, null!, null!, null!, new IdentityErrorDescriber(), null!, null!);
        _service = new UserManagementService(Context, _userManager, _xboxService);
    }

    // RegionConfiguration seeds these through HasData; EnsureCreated() applies them, so a test reuses the existing
    // row rather than inserting its own (which would violate the unique key).
    private const int WestNorthAmericaRegionID = 1;

    // GetUsers and GetUser aren't covered here. Both project Roles as a collection correlated to the outer user
    // through an unrelated DbSet (_context.Roles, not a navigation): on SQL Server and Postgres this is a plain
    // correlated subquery, but SQLite has no APPLY/LATERAL, so EF Core needs a service only Infrastructure's
    // DbContextOptionsBuilder wires up (DatabaseProviderExtensions.UseGrifballDatabase replaces
    // IQueryTranslationPostprocessorFactory with SqliteQueryTranslationPostprocessorFactory for exactly this).
    // FakeApplicationDbContext, built on plain UseSqlite() so this layer's tests stay out of Infrastructure, doesn't
    // have it, so both methods throw "Translating this query requires the SQL APPLY operation, which is not
    // supported on SQLite" here. GrifballWebApp.Integration.Test's UserManagementServiceTests and
    // UserManagementServiceBranchTests already cover both against the real providers, SQLite included.

    [Test]
    public async Task CreateUser_UserNameTaken_ReturnsMessage()
    {
        Context.Users.Add(User.Seed(userName: "alice", id: 1));
        await Context.SaveChangesAsync();
        var request = new CreateUserRequestDto { DisplayName = "Alice", Gamertag = "GamerOne", UserName = "alice" };

        var result = await _service.CreateUser(request, CancellationToken.None);

        Assert.That(result, Is.EqualTo("User name is taken"));
    }

    [Test]
    public async Task CreateUser_XboxLookupFails_ReturnsMessage()
    {
        _xboxService.GetsertXboxUserByGamertag("GamerOne", Arg.Any<CancellationToken>())
            .Returns(((XboxUser?)null, "no such gamertag"));
        var request = new CreateUserRequestDto { DisplayName = "Alice", Gamertag = "GamerOne", UserName = "alice" };

        var result = await _service.CreateUser(request, CancellationToken.None);

        Assert.That(result, Is.EqualTo("no such gamertag"));
    }

    [Test]
    public async Task CreateUser_CreateFails_ReturnsErrors()
    {
        var xbox = XboxUser.FromXbox(111, "GamerOne");
        _xboxService.GetsertXboxUserByGamertag("GamerOne", Arg.Any<CancellationToken>()).Returns(((XboxUser?)xbox, (string?)null));
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Failed(new IdentityError { Description = "Bad user" }));
        var request = new CreateUserRequestDto { DisplayName = "Alice", Gamertag = "GamerOne", UserName = "alice" };

        var result = await _service.CreateUser(request, CancellationToken.None);

        Assert.That(result, Does.Contain("Bad user"));
    }

    [Test]
    public async Task CreateUser_AddRoleFails_ReturnsMessage()
    {
        var xbox = XboxUser.FromXbox(111, "GamerOne");
        _xboxService.GetsertXboxUserByGamertag("GamerOne", Arg.Any<CancellationToken>()).Returns(((XboxUser?)xbox, (string?)null));
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<User>(), "Player").Returns(IdentityResult.Failed());
        var request = new CreateUserRequestDto { DisplayName = "Alice", Gamertag = "GamerOne", UserName = "alice" };

        var result = await _service.CreateUser(request, CancellationToken.None);

        Assert.That(result, Is.EqualTo("Failed to add the Player role"));
    }

    [Test]
    public async Task CreateUser_Success_ReturnsNull()
    {
        var xbox = XboxUser.FromXbox(111, "GamerOne");
        _xboxService.GetsertXboxUserByGamertag("GamerOne", Arg.Any<CancellationToken>()).Returns(((XboxUser?)xbox, (string?)null));
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<User>(), "Player").Returns(IdentityResult.Success);
        var request = new CreateUserRequestDto { DisplayName = "Alice", Gamertag = "GamerOne", UserName = "alice" };

        var result = await _service.CreateUser(request, CancellationToken.None);

        Assert.That(result, Is.Null);
        await _userManager.Received().AddToRoleAsync(Arg.Any<User>(), "Player");
    }

    [Test]
    public async Task EditUser_NotFound_Throws()
    {
        var edit = new UserResponseDto
        {
            UserID = 999, Region = null, DisplayName = "x", Gamertag = null, Discord = null,
            ExternalAuthCount = 0, HasPassword = false, Roles = [],
        };

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.EditUser(edit, CancellationToken.None));
        Assert.That(ex!.Message, Is.EqualTo("User does not exist"));
    }

    [Test]
    public async Task EditUser_InvalidRegion_ReturnsMessage()
    {
        var user = User.Seed(userName: "alice", id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        var edit = new UserResponseDto
        {
            UserID = 1, Region = null, RegionID = 999, DisplayName = "Alice", Gamertag = null, Discord = null,
            ExternalAuthCount = 0, HasPassword = false, Roles = [],
        };

        var result = await _service.EditUser(edit, CancellationToken.None);

        Assert.That(result, Is.EqualTo("There is no such region"));
    }

    [Test]
    public async Task EditUser_Success_UpdatesProfileAndRoles()
    {
        var user = User.Seed(userName: "alice", displayName: "Alice", id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        _userManager.SetUserNameAsync(Arg.Any<User>(), "alice2").Returns(IdentityResult.Success);
        _userManager.GetRolesAsync(Arg.Any<User>()).Returns(new List<string>());
        _userManager.AddToRolesAsync(Arg.Any<User>(), Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Success);

        var edit = new UserResponseDto
        {
            UserID = 1, Region = null, RegionID = WestNorthAmericaRegionID, DisplayName = "Alice Renamed", Gamertag = null, Discord = null,
            ExternalAuthCount = 0, HasPassword = false, UserName = "alice2", IsDummyUser = true,
            Roles = [new RoleDto { RoleName = "Player", HasRole = true }],
        };

        var result = await _service.EditUser(edit, CancellationToken.None);

        Assert.That(result, Is.Null);
        Context.ChangeTracker.Clear();
        var reread = await Context.Users.FindAsync(1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reread!.DisplayName, Is.EqualTo("Alice Renamed"));
            Assert.That(reread.IsDummyUser, Is.True);
            Assert.That(reread.RegionID, Is.EqualTo(WestNorthAmericaRegionID));
        }
        await _userManager.Received().AddToRolesAsync(Arg.Any<User>(), Arg.Is<IEnumerable<string>>(r => r.Contains("Player")));
    }

    [Test]
    public async Task GeneratePasswordResetLink_UserNotFound_ReturnsFailure()
    {
        _userManager.FindByNameAsync("nobody").Returns((User?)null);

        var (success, message, token, expiresAt) = await _service.GeneratePasswordResetLink("nobody", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(message, Is.EqualTo("User not found"));
            Assert.That(token, Is.Null);
            Assert.That(expiresAt, Is.Null);
        }
    }

    [Test]
    public async Task GeneratePasswordResetLink_ExpiresOldLinksAndIssuesNew()
    {
        var user = User.Seed(userName: "alice", id: 1);
        Context.Users.Add(user);
        var now = DateTime.UtcNow;
        var (oldLink, oldToken) = PasswordResetLink.Issue(1, now.AddMinutes(-1));
        Context.PasswordResetLinks.Add(oldLink);
        await Context.SaveChangesAsync();
        _userManager.FindByNameAsync("alice").Returns(user);

        var (success, message, token, expiresAt) = await _service.GeneratePasswordResetLink("alice", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.True);
            Assert.That(token, Is.Not.Null.And.Not.EqualTo(oldToken));
            Assert.That(expiresAt, Is.Not.Null);
        }
        Context.ChangeTracker.Clear();
        var links = await Context.PasswordResetLinks.ToListAsync();
        Assert.That(links, Has.Count.EqualTo(2));
        Assert.That(links.Single(l => l.Token == PasswordResetLink.HashToken(oldToken)).CanUse(DateTime.UtcNow), Is.EqualTo(PasswordResetLinkUse.Expired));
    }

    [Test]
    public async Task UsePasswordResetLink_InvalidToken_ReturnsFailure()
    {
        var (success, message) = await _service.UsePasswordResetLink("doesnotexist", "NewPassword1!", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(message, Is.EqualTo("Invalid or already used reset link"));
        }
    }

    [Test]
    public async Task UsePasswordResetLink_Expired_DeletesLinkAndReturnsFailure()
    {
        var user = User.Seed(userName: "alice", id: 1);
        Context.Users.Add(user);
        var (link, token) = PasswordResetLink.Issue(1, DateTime.UtcNow.AddHours(-1));
        Context.PasswordResetLinks.Add(link);
        await Context.SaveChangesAsync();

        var (success, message) = await _service.UsePasswordResetLink(token, "NewPassword1!", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(message, Is.EqualTo("Reset link has expired"));
        }
        Assert.That(await Context.PasswordResetLinks.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task UsePasswordResetLink_NoExistingPassword_AddsPassword()
    {
        var user = User.Seed(userName: "alice", id: 1);
        Context.Users.Add(user);
        var (link, token) = PasswordResetLink.Issue(1, DateTime.UtcNow);
        Context.PasswordResetLinks.Add(link);
        await Context.SaveChangesAsync();
        _userManager.AddPasswordAsync(Arg.Any<User>(), "NewPassword1!").Returns(IdentityResult.Success);

        var (success, message) = await _service.UsePasswordResetLink(token, "NewPassword1!", CancellationToken.None);

        Assert.That(success, Is.True);
        await _userManager.Received().AddPasswordAsync(Arg.Any<User>(), "NewPassword1!");
        Context.ChangeTracker.Clear();
        var reread = await Context.PasswordResetLinks.FirstAsync();
        Assert.That(reread.UsedAt, Is.Not.Null);
    }

    /// <summary>
    /// Another request uses the link between this one's read and its save: UsedAt is the link's concurrency token, so this
    /// one's save matches no row, it is refused, and its transaction rolls back with the password change in it. On one
    /// SQLite in-memory connection the other request can't commit on its own while this one's transaction is open (a
    /// connection has one transaction), so its context joins that transaction, and its UsedAt is rolled back too here; in
    /// production it was committed and stays. What this checks is this request's side: refused, its password change undone.
    /// </summary>
    [Test]
    public async Task UsePasswordResetLink_UsedByAnotherRequestMeanwhile_IsRefused_AndItsPasswordChangeRollsBack()
    {
        var user = User.Seed(userName: "alice", id: 1);
        Context.Users.Add(user);
        var (link, token) = PasswordResetLink.Issue(1, DateTime.UtcNow);
        Context.PasswordResetLinks.Add(link);
        await Context.SaveChangesAsync();
        var linkId = link.Id;
        var otherRequestRan = false;
        _userManager.AddPasswordAsync(Arg.Any<User>(), "NewPassword1!").Returns(async _ =>
        {
            // The password change, written in this request's transaction as Identity's store (the same context) writes it
            await Context.Users.Where(u => u.Id == 1).ExecuteUpdateAsync(u => u.SetProperty(x => x.PasswordHash, "new-hash"));
            // The other request's use of the link, from a context of its own
            await using var other = Context.NewContextLike();
            await other.Database.UseTransactionAsync(Context.Database.CurrentTransaction!.GetDbTransaction());
            await other.PasswordResetLinks.Where(l => l.Id == linkId).ExecuteUpdateAsync(l => l.SetProperty(x => x.UsedAt, DateTime.UtcNow));
            otherRequestRan = true;
            return IdentityResult.Success;
        });

        var (success, message) = await _service.UsePasswordResetLink(token, "NewPassword1!", CancellationToken.None);

        Context.ChangeTracker.Clear();
        var passwordHash = await Context.Users.Where(u => u.Id == 1).Select(u => u.PasswordHash).SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(otherRequestRan, Is.True);
            Assert.That(success, Is.False);
            Assert.That(message, Is.EqualTo("Invalid or already used reset link"));
            Assert.That(passwordHash, Is.Null, "the password change was rolled back with the transaction");
        }
    }

    [Test]
    public async Task UsePasswordResetLink_ExistingPassword_ResetsPassword()
    {
        var user = User.Seed(userName: "alice", id: 1, passwordHash: "existing-hash");
        Context.Users.Add(user);
        var (link, token) = PasswordResetLink.Issue(1, DateTime.UtcNow);
        Context.PasswordResetLinks.Add(link);
        await Context.SaveChangesAsync();
        _userManager.GeneratePasswordResetTokenAsync(Arg.Any<User>()).Returns("reset-token");
        _userManager.ResetPasswordAsync(Arg.Any<User>(), "reset-token", "NewPassword1!").Returns(IdentityResult.Success);

        var (success, _) = await _service.UsePasswordResetLink(token, "NewPassword1!", CancellationToken.None);

        Assert.That(success, Is.True);
        await _userManager.Received().ResetPasswordAsync(Arg.Any<User>(), "reset-token", "NewPassword1!");
    }

    [Test]
    public async Task UsePasswordResetLink_ResetFails_ReturnsErrors()
    {
        var user = User.Seed(userName: "alice", id: 1);
        Context.Users.Add(user);
        var (link, token) = PasswordResetLink.Issue(1, DateTime.UtcNow);
        Context.PasswordResetLinks.Add(link);
        await Context.SaveChangesAsync();
        _userManager.AddPasswordAsync(Arg.Any<User>(), "bad").Returns(IdentityResult.Failed(new IdentityError { Description = "Too short" }));

        var (success, message) = await _service.UsePasswordResetLink(token, "bad", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(success, Is.False);
            Assert.That(message, Does.Contain("Too short"));
        }
    }

    [Test]
    public async Task CleanupExpiredPasswordResetLinks_RemovesSpentLinks()
    {
        var user = User.Seed(userName: "alice", id: 1);
        Context.Users.Add(user);
        var (expired, _) = PasswordResetLink.Issue(1, DateTime.UtcNow.AddHours(-1));
        var (fresh, _) = PasswordResetLink.Issue(1, DateTime.UtcNow);
        Context.PasswordResetLinks.AddRange(expired, fresh);
        await Context.SaveChangesAsync();

        await _service.CleanupExpiredPasswordResetLinks(CancellationToken.None);

        Context.ChangeTracker.Clear();
        var remaining = await Context.PasswordResetLinks.ToListAsync();
        Assert.That(remaining, Has.Count.EqualTo(1));
        Assert.That(remaining[0].Token, Is.EqualTo(fresh.Token));
    }

    [Test]
    public async Task CleanupExpiredPasswordResetLinks_NoneExpired_DoesNothing()
    {
        var user = User.Seed(userName: "alice", id: 1);
        Context.Users.Add(user);
        var (fresh, _) = PasswordResetLink.Issue(1, DateTime.UtcNow);
        Context.PasswordResetLinks.Add(fresh);
        await Context.SaveChangesAsync();

        await _service.CleanupExpiredPasswordResetLinks(CancellationToken.None);

        Assert.That(await Context.PasswordResetLinks.CountAsync(), Is.EqualTo(1));
    }
}
