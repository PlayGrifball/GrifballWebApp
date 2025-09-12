using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.UserManagement;
using GrifballWebApp.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Surprenant.Grunt.Core;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class PasswordResetTests
{
    private GrifballContext _context;
    private UserManagementService _service;
    private UserManager<User> _userManager;
    private IGetsertXboxUserService _getsertXboxUserService;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
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
        var user = new User { UserName = "testuser", PasswordHash = "somehashedpassword" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _userManager.FindByNameAsync("testuser").Returns(user);

        // Act
        var (success, message, token, expiresAt) = await _service.GeneratePasswordResetLink("testuser", 1, CancellationToken.None);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(message, Is.EqualTo("Password reset link generated successfully"));
        Assert.That(token, Is.Not.Null.And.Not.Empty);
        Assert.That(expiresAt, Is.Not.Null);
        Assert.That(expiresAt!.Value, Is.GreaterThan(DateTime.UtcNow));

        // Verify reset link was saved in database
        var resetLink = await _context.PasswordResetLinks.FirstOrDefaultAsync(x => x.Token == token);
        Assert.That(resetLink, Is.Not.Null);
        Assert.That(resetLink.UserId, Is.EqualTo(user.Id));
        Assert.That(resetLink.IsUsed, Is.False);
    }

    [Test]
    public async Task GeneratePasswordResetLink_ShouldFail_WhenUserNotFound()
    {
        // Arrange
        _userManager.FindByNameAsync("nonexistentuser").Returns((User?)null);

        // Act
        var (success, message, token, expiresAt) = await _service.GeneratePasswordResetLink("nonexistentuser", 1, CancellationToken.None);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(message, Is.EqualTo("User not found"));
        Assert.That(token, Is.Null);
        Assert.That(expiresAt, Is.Null);
    }

    [Test]
    public async Task GeneratePasswordResetLink_ShouldFail_WhenUserHasNoPassword()
    {
        // Arrange
        var user = new User { UserName = "oauthuser", PasswordHash = null };
        _userManager.FindByNameAsync("oauthuser").Returns(user);

        // Act
        var (success, message, token, expiresAt) = await _service.GeneratePasswordResetLink("oauthuser", 1, CancellationToken.None);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(message, Is.EqualTo("User does not have a password set (likely uses external login only)"));
        Assert.That(token, Is.Null);
        Assert.That(expiresAt, Is.Null);
    }

    [Test]
    public async Task GeneratePasswordResetLink_ShouldReplaceExistingUnusedLinks()
    {
        // Arrange
        var user = new User { UserName = "testuser", PasswordHash = "somehashedpassword" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Add existing reset link
        var existingLink = new PasswordResetLink
        {
            UserId = user.Id,
            Token = "oldtoken",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            CreatedByID = 1,
            ModifiedByID = 1,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        _context.PasswordResetLinks.Add(existingLink);
        await _context.SaveChangesAsync();

        _userManager.FindByNameAsync("testuser").Returns(user);

        // Act
        var (success, message, token, expiresAt) = await _service.GeneratePasswordResetLink("testuser", 1, CancellationToken.None);

        // Assert
        Assert.That(success, Is.True);
        
        // Verify old link is removed
        var oldLink = await _context.PasswordResetLinks.FirstOrDefaultAsync(x => x.Token == "oldtoken");
        Assert.That(oldLink, Is.Null);
        
        // Verify new link exists
        var newLink = await _context.PasswordResetLinks.FirstOrDefaultAsync(x => x.Token == token);
        Assert.That(newLink, Is.Not.Null);
    }

    [Test]
    public async Task UsePasswordResetLink_ShouldSucceed_WhenTokenIsValidAndNotExpired()
    {
        // Arrange
        var user = new User { UserName = "testuser", PasswordHash = "oldpassword" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var resetLink = new PasswordResetLink
        {
            UserId = user.Id,
            Token = "validtoken",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            CreatedByID = 1,
            ModifiedByID = 1,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        _context.PasswordResetLinks.Add(resetLink);
        await _context.SaveChangesAsync();

        _userManager.GeneratePasswordResetTokenAsync(user).Returns("resettoken");
        _userManager.ResetPasswordAsync(user, "resettoken", "newpassword").Returns(IdentityResult.Success);

        // Act
        var (success, message) = await _service.UsePasswordResetLink("validtoken", "newpassword", CancellationToken.None);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(message, Is.EqualTo("Password reset successfully"));

        // Verify reset link is removed
        var linkAfterUse = await _context.PasswordResetLinks.FirstOrDefaultAsync(x => x.Token == "validtoken");
        Assert.That(linkAfterUse, Is.Null);

        // Verify password reset was called
        await _userManager.Received(1).ResetPasswordAsync(user, "resettoken", "newpassword");
    }

    [Test]
    public async Task UsePasswordResetLink_ShouldFail_WhenTokenIsInvalid()
    {
        // Act
        var (success, message) = await _service.UsePasswordResetLink("invalidtoken", "newpassword", CancellationToken.None);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(message, Is.EqualTo("Invalid or already used reset link"));
    }

    [Test]
    public async Task UsePasswordResetLink_ShouldFail_WhenTokenIsExpired()
    {
        // Arrange
        var user = new User { UserName = "testuser", PasswordHash = "oldpassword" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var expiredLink = new PasswordResetLink
        {
            UserId = user.Id,
            Token = "expiredtoken",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1), // Expired 1 minute ago
            CreatedByID = 1,
            ModifiedByID = 1,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        _context.PasswordResetLinks.Add(expiredLink);
        await _context.SaveChangesAsync();

        // Act
        var (success, message) = await _service.UsePasswordResetLink("expiredtoken", "newpassword", CancellationToken.None);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(message, Is.EqualTo("Reset link has expired"));

        // Verify expired link is cleaned up
        var linkAfterUse = await _context.PasswordResetLinks.FirstOrDefaultAsync(x => x.Token == "expiredtoken");
        Assert.That(linkAfterUse, Is.Null);
    }

    [Test]
    public async Task UsePasswordResetLink_ShouldFail_WhenPasswordResetFails()
    {
        // Arrange
        var user = new User { UserName = "testuser", PasswordHash = "oldpassword" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var resetLink = new PasswordResetLink
        {
            UserId = user.Id,
            Token = "validtoken",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            CreatedByID = 1,
            ModifiedByID = 1,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
        _context.PasswordResetLinks.Add(resetLink);
        await _context.SaveChangesAsync();

        _userManager.GeneratePasswordResetTokenAsync(user).Returns("resettoken");
        var failedResult = IdentityResult.Failed(new IdentityError { Description = "Password too weak" });
        _userManager.ResetPasswordAsync(user, "resettoken", "weak").Returns(failedResult);

        // Act
        var (success, message) = await _service.UsePasswordResetLink("validtoken", "weak", CancellationToken.None);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(message, Is.EqualTo("Failed to reset password: Password too weak"));
    }

    [Test]
    public async Task CleanupExpiredPasswordResetLinks_ShouldRemoveExpiredLinks()
    {
        // Arrange
        var user = new User { UserName = "testuser" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var expiredLink = new PasswordResetLink
        {
            UserId = user.Id,
            Token = "expiredtoken",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedByID = 1,
            ModifiedByID = 1,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        var validLink = new PasswordResetLink
        {
            UserId = user.Id,
            Token = "validtoken",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            CreatedByID = 1,
            ModifiedByID = 1,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        var usedLink = new PasswordResetLink
        {
            UserId = user.Id,
            Token = "usedtoken",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IsUsed = true,
            CreatedByID = 1,
            ModifiedByID = 1,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        _context.PasswordResetLinks.AddRange(expiredLink, validLink, usedLink);
        await _context.SaveChangesAsync();

        // Act
        await _service.CleanupExpiredPasswordResetLinks(CancellationToken.None);

        // Assert
        var remainingLinks = await _context.PasswordResetLinks.ToListAsync();
        Assert.That(remainingLinks.Count, Is.EqualTo(1));
        Assert.That(remainingLinks[0].Token, Is.EqualTo("validtoken"));
    }
}