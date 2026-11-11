using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.UserManagement;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class PasswordResetTests
{
    private GrifballContext _context;
    private UserManagementService _service;
    private UserManager<User> _userManager;
    private IGetsertXboxUserService _getsertXboxUserService;

    [SetUp]
    public async Task Setup()
    {
        _context = await TestDatabase.NewGrifballContext();
        _userManager = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);
        _getsertXboxUserService = Substitute.For<IGetsertXboxUserService>();
        _service = new UserManagementService(_context, _userManager, _getsertXboxUserService);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task GeneratePasswordResetLink_ShouldSucceed_WhenUserExistsWithPassword()
    {
        // Arrange
        var user = User.Seed(userName: "testuser", passwordHash: "somehashedpassword");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _userManager.FindByNameAsync("testuser").Returns(user);

        // Act
        var (success, message, token, expiresAt) = await _service.GeneratePasswordResetLink("testuser", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(success, Is.True);
            Assert.That(message, Is.EqualTo("Password reset link generated successfully"));
            Assert.That(token, Is.Not.Null.And.Not.Empty);
            Assert.That(expiresAt, Is.Not.Null);
        }
        Assert.That(expiresAt!.Value, Is.GreaterThan(DateTime.UtcNow));

        // Verify reset link was saved in database
        var hashed = PasswordResetLink.HashToken(token ?? throw new AssertionException("No token"));
        var resetLink = await _context.PasswordResetLinks.FirstOrDefaultAsync(x => x.Token == hashed);
        Assert.That(resetLink, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(resetLink.UserId, Is.EqualTo(user.Id));
            Assert.That(resetLink.UsedAt, Is.Null);
            Assert.That(await _context.PasswordResetLinks.AnyAsync(x => x.Token == token), Is.False, "Only the token's hash is kept");
        }
    }

    [Test]
    public async Task GeneratePasswordResetLink_ShouldFail_WhenUserNotFound()
    {
        // Arrange
        _userManager.FindByNameAsync("nonexistentuser").Returns((User?)null);

        // Act
        var (success, message, token, expiresAt) = await _service.GeneratePasswordResetLink("nonexistentuser", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(success, Is.False);
            Assert.That(message, Is.EqualTo("User not found"));
            Assert.That(token, Is.Null);
            Assert.That(expiresAt, Is.Null);
        }
    }

    [Test]
    public async Task GeneratePasswordResetLink_ShouldSucceed_WhenUserHasNoPassword()
    {
        // Arrange
        var user = User.Seed(userName: "oauthuser", passwordHash: null);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _userManager.FindByNameAsync("oauthuser").Returns(user);

        // Act
        var (success, message, token, expiresAt) = await _service.GeneratePasswordResetLink("oauthuser", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(success, Is.True);
            Assert.That(message, Is.EqualTo("Password reset link generated successfully"));
            Assert.That(token, Is.Not.Null.And.Not.Empty);
            Assert.That(expiresAt, Is.Not.Null);
        }
        Assert.That(expiresAt!.Value, Is.GreaterThan(DateTime.UtcNow));

        // Verify reset link was saved in database
        var resetLink = await _context.PasswordResetLinks.FirstOrDefaultAsync(x => x.Token == PasswordResetLink.HashToken(token));
        Assert.That(resetLink, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(resetLink.UserId, Is.EqualTo(user.Id));
            Assert.That(resetLink.UsedAt, Is.Null);
        }
    }

    [Test]
    public async Task GeneratePasswordResetLink_ShouldReplaceExistingUnusedLinks()
    {
        // Arrange
        var user = User.Seed(userName: "testuser", passwordHash: "somehashedpassword");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Add existing reset link
        var existingLink = PasswordResetLink.Seed(user.Id, "oldtoken", DateTime.UtcNow.AddMinutes(5));
        _context.PasswordResetLinks.Add(existingLink);
        await _context.SaveChangesAsync();

        _userManager.FindByNameAsync("testuser").Returns(user);

        // Act
        var (success, message, token, expiresAt) = await _service.GeneratePasswordResetLink("testuser", CancellationToken.None);

        // Assert
        Assert.That(success, Is.True);
        
        // Verify old link expired (unused): the cleanup deletes it
        var oldLink = await _context.PasswordResetLinks.SingleAsync(x => x.Token == PasswordResetLink.HashToken("oldtoken"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(oldLink.IsUsableAt(DateTime.UtcNow), Is.False);
            Assert.That(oldLink.UsedAt, Is.Null);
        }
        var (oldSuccess, oldMessage) = await _service.UsePasswordResetLink("oldtoken", "newpassword", CancellationToken.None);
        Assert.That((oldSuccess, oldMessage), Is.EqualTo((false, "Reset link has expired")));
        
        // Verify new link exists
        var hashed = PasswordResetLink.HashToken(token ?? throw new AssertionException("No token"));
        var newLink = await _context.PasswordResetLinks.FirstOrDefaultAsync(x => x.Token == hashed);
        Assert.That(newLink, Is.Not.Null);
    }

    [Test]
    public async Task UsePasswordResetLink_ShouldSucceed_WhenTokenIsValidAndNotExpired()
    {
        // Arrange
        var user = User.Seed(userName: "testuser", passwordHash: "oldpassword");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var resetLink = PasswordResetLink.Seed(user.Id, "validtoken", DateTime.UtcNow.AddMinutes(5));
        _context.PasswordResetLinks.Add(resetLink);
        await _context.SaveChangesAsync();

        _userManager.GeneratePasswordResetTokenAsync(user).Returns("resettoken");
        _userManager.ResetPasswordAsync(user, "resettoken", "newpassword").Returns(IdentityResult.Success);

        // Act
        var (success, message) = await _service.UsePasswordResetLink("validtoken", "newpassword", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(success, Is.True);
            Assert.That(message, Is.EqualTo("Password set successfully"));
        }

        // Verify reset link is used, and can't be used again
        var linkAfterUse = await _context.PasswordResetLinks.SingleAsync(x => x.Token == PasswordResetLink.HashToken("validtoken"));
        Assert.That(linkAfterUse.UsedAt, Is.Not.Null);
        var (again, againMessage) = await _service.UsePasswordResetLink("validtoken", "newpassword", CancellationToken.None);
        Assert.That((again, againMessage), Is.EqualTo((false, "Invalid or already used reset link")));

        // Verify password reset was called, once
        await _userManager.Received(1).ResetPasswordAsync(user, "resettoken", "newpassword");
    }

    [Test]
    public async Task UsePasswordResetLink_ShouldFail_WhenAnotherRequestUsesTheLinkFirst()
    {
        // Arrange
        var user = User.Seed(userName: "testuser", passwordHash: "oldpassword");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Read (and tracked) here while unused, as a request that read it before the other saved
        var resetLink = PasswordResetLink.Seed(user.Id, "validtoken", DateTime.UtcNow.AddMinutes(5));
        _context.PasswordResetLinks.Add(resetLink);
        await _context.SaveChangesAsync();

        // Another request uses it first
        await using (var other = _context.NewContextLike())
        {
            var link = await other.PasswordResetLinks.SingleAsync(PasswordResetLink.OfToken("validtoken"));
            Assert.That(link.Use(DateTime.UtcNow), Is.EqualTo(PasswordResetLinkUse.Ok));
            await other.SaveChangesAsync();
        }

        _userManager.GeneratePasswordResetTokenAsync(user).Returns("resettoken");
        _userManager.ResetPasswordAsync(user, "resettoken", "newpassword").Returns(IdentityResult.Success);

        // Act
        var (success, message) = await _service.UsePasswordResetLink("validtoken", "newpassword", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(success, Is.False);
            Assert.That(message, Is.EqualTo("Invalid or already used reset link"));
        }
    }

    [Test]
    public async Task UsePasswordResetLink_ShouldFail_WhenTokenIsInvalid()
    {
        // Act
        var (success, message) = await _service.UsePasswordResetLink("invalidtoken", "newpassword", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(success, Is.False);
            Assert.That(message, Is.EqualTo("Invalid or already used reset link"));
        }
    }

    [Test]
    public async Task UsePasswordResetLink_ShouldFail_WhenTokenIsExpired()
    {
        // Arrange
        var user = User.Seed(userName: "testuser", passwordHash: "oldpassword");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var expiredLink = PasswordResetLink.Seed(user.Id, "expiredtoken", DateTime.UtcNow.AddMinutes(-1));
        _context.PasswordResetLinks.Add(expiredLink);
        await _context.SaveChangesAsync();

        // Act
        var (success, message) = await _service.UsePasswordResetLink("expiredtoken", "newpassword", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(success, Is.False);
            Assert.That(message, Is.EqualTo("Reset link has expired"));
        }

        // Verify expired link is cleaned up
        var linkAfterUse = await _context.PasswordResetLinks.FirstOrDefaultAsync(x => x.Token == PasswordResetLink.HashToken("expiredtoken"));
        Assert.That(linkAfterUse, Is.Null);
    }

    [Test]
    public async Task UsePasswordResetLink_ShouldFail_WhenPasswordResetFails()
    {
        // Arrange
        var user = User.Seed(userName: "testuser", passwordHash: "oldpassword");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var resetLink = PasswordResetLink.Seed(user.Id, "validtoken", DateTime.UtcNow.AddMinutes(5));
        _context.PasswordResetLinks.Add(resetLink);
        await _context.SaveChangesAsync();

        _userManager.GeneratePasswordResetTokenAsync(user).Returns("resettoken");
        var failedResult = IdentityResult.Failed(new IdentityError { Description = "Password too weak" });
        _userManager.ResetPasswordAsync(user, "resettoken", "weak").Returns(failedResult);

        // Act
        var (success, message) = await _service.UsePasswordResetLink("validtoken", "weak", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(success, Is.False);
            Assert.That(message, Is.EqualTo("Failed to set password: Password too weak"));
            Assert.That(resetLink.UsedAt, Is.Null, "Still usable: no password was set");
        }
    }

    [Test]
    public async Task UsePasswordResetLink_ShouldAddPassword_WhenUserHasNoPassword()
    {
        // Arrange
        var user = User.Seed(userName: "oauthuser", passwordHash: null);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var resetLink = PasswordResetLink.Seed(user.Id, "validtoken", DateTime.UtcNow.AddMinutes(5));
        _context.PasswordResetLinks.Add(resetLink);
        await _context.SaveChangesAsync();

        _userManager.AddPasswordAsync(user, "newpassword").Returns(IdentityResult.Success);

        // Act
        var (success, message) = await _service.UsePasswordResetLink("validtoken", "newpassword", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(success, Is.True);
            Assert.That(message, Is.EqualTo("Password set successfully"));
        }

        // Verify reset link is used
        var linkAfterUse = await _context.PasswordResetLinks.SingleAsync(x => x.Token == PasswordResetLink.HashToken("validtoken"));
        Assert.That(linkAfterUse.UsedAt, Is.Not.Null);

        // Verify AddPasswordAsync was called
        await _userManager.Received(1).AddPasswordAsync(user, "newpassword");
    }

    [Test]
    public async Task CleanupExpiredPasswordResetLinks_ShouldRemoveExpiredLinks()
    {
        // Arrange
        var user = User.Seed(userName: "testuser");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var expiredLink = PasswordResetLink.Seed(user.Id, "expiredtoken", DateTime.UtcNow.AddMinutes(-1));

        var validLink = PasswordResetLink.Seed(user.Id, "validtoken", DateTime.UtcNow.AddMinutes(5));

        var usedLink = PasswordResetLink.Seed(user.Id, "usedtoken", DateTime.UtcNow.AddMinutes(5), usedAt: DateTime.UtcNow.AddMinutes(-1));

        _context.PasswordResetLinks.AddRange(expiredLink, validLink, usedLink);
        await _context.SaveChangesAsync();

        // Act
        await _service.CleanupExpiredPasswordResetLinks(CancellationToken.None);

        // Assert
        var remainingLinks = await _context.PasswordResetLinks.ToListAsync();
        Assert.That(remainingLinks, Has.Count.EqualTo(1));
        Assert.That(remainingLinks[0].Token, Is.EqualTo(PasswordResetLink.HashToken("validtoken")));
    }
}