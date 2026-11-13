using GrifballWebApp.Domain.Players;
using GrifballWebApp.Server.Preview;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>PreviewSmokeUsers's unit tests: no database, host or network (#402). UserManager is NSubstitute's, as the
/// Identity controller tests already substitute it.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class PreviewSmokeUsersUnitTests
{
    private static IConfiguration Config(bool enabled = true, string? password = "correct-password")
    {
        var values = new Dictionary<string, string?> { ["Preview:Enabled"] = enabled.ToString() };
        if (password is not null)
            values[PreviewSmokeUsers.PasswordKey] = password;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static UserManager<User> Users() => Substitute.For<UserManager<User>>(
        Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);

    [Test]
    public async Task EnsureAsync_DoesNothing_WhenPreviewModeIsOff()
    {
        var result = await PreviewSmokeUsers.EnsureAsync(Config(enabled: false), Users(), new RecordingLogger<object>());

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task EnsureAsync_DoesNothing_WhenNoPasswordIsConfigured()
    {
        var result = await PreviewSmokeUsers.EnsureAsync(Config(password: null), Users(), new RecordingLogger<object>());

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task EnsureAsync_CreatesEveryUser_WhenNoneExist()
    {
        var users = Users();
        users.CreateAsync(Arg.Any<User>(), "correct-password").Returns(IdentityResult.Success);
        users.AddToRolesAsync(Arg.Any<User>(), Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Success);

        var result = await PreviewSmokeUsers.EnsureAsync(Config(), users, new RecordingLogger<object>());

        Assert.That(result, Is.EqualTo(PreviewSmokeUsers.UserNames.Count));
    }

    [Test]
    public async Task EnsureAsync_SkipsAUser_WhoseCreationFails_AndStillTriesTheOthers()
    {
        var users = Users();
        users.CreateAsync(Arg.Any<User>(), "correct-password").Returns(IdentityResult.Failed(new IdentityError { Description = "Password too short" }));
        var logger = new RecordingLogger<object>();

        var result = await PreviewSmokeUsers.EnsureAsync(Config(), users, logger);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Zero);
            // An error logged for each user that couldn't be created, plus the "ready" summary EnsureAsync always logs
            Assert.That(logger.Entries.Where(e => e.Level == LogLevel.Error).Select(e => e.Message),
                Is.EquivalentTo(PreviewSmokeUsers.UserNames.Select(name => $"Preview smoke-test user {name} is not ready")));
            Assert.That(logger.Entries.Single(e => e.Level == LogLevel.Information).Message,
                Is.EqualTo("Preview smoke-test users ready (0 created or updated)"));
        }
    }

    [Test]
    public async Task EnsureAsync_SetsThePassword_WhenAnExistingUsersDoesNotMatch()
    {
        var users = Users();
        var existing = User.Register(PreviewSmokeUsers.UserNames[0]);
        users.FindByNameAsync(PreviewSmokeUsers.UserNames[0]).Returns(existing);
        users.FindByNameAsync(PreviewSmokeUsers.UserNames[1]).Returns((User?)null);
        users.CheckPasswordAsync(existing, "correct-password").Returns(false);
        users.HasPasswordAsync(existing).Returns(false);
        users.AddPasswordAsync(existing, "correct-password").Returns(IdentityResult.Success);
        users.CreateAsync(Arg.Any<User>(), "correct-password").Returns(IdentityResult.Success);
        users.AddToRolesAsync(Arg.Any<User>(), Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Success);

        var result = await PreviewSmokeUsers.EnsureAsync(Config(), users, new RecordingLogger<object>());

        Assert.That(result, Is.EqualTo(PreviewSmokeUsers.UserNames.Count));
        await users.Received(1).AddPasswordAsync(existing, "correct-password");
        await users.DidNotReceive().RemovePasswordAsync(Arg.Any<User>());
    }
}
