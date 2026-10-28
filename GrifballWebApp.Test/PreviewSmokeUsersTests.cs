using GrifballWebApp.Database;
using GrifballWebApp.Server.Preview;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Role = GrifballWebApp.Database.Models.Role;
using User = GrifballWebApp.Database.Models.User;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class PreviewSmokeUsersTests
{
    private const string Password = "Smoke-Test-Password-1";

    private GrifballContext _context;
    private ServiceProvider _provider;
    private IServiceScope _scope;
    private UserManager<User> _userManager;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => _context);
        services.AddIdentity<User, Role>()
            .AddDefaultTokenProviders()
            .AddEntityFrameworkStores<GrifballContext>();
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _userManager = _scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
        _scope.Dispose();
        await _provider.DisposeAsync();
    }

    private static IConfiguration Configuration(string? preview, string? password)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [PreviewMode.ConfigurationKey] = preview,
            [PreviewSmokeUsers.PasswordKey] = password,
        }).Build();

    private Task<int?> Ensure(string? preview = "true", string? password = Password)
        => PreviewSmokeUsers.EnsureAsync(Configuration(preview, password), _userManager, NullLogger.Instance);

    private Task<bool> AnySmokeUser()
        => _context.Users.AnyAsync(u => PreviewSmokeUsers.UserNames.Contains(u.UserName!));

    [Test]
    public async Task Should_CreateBothUsers_WithPasswordAndRoles_InPreviewMode()
    {
        var changed = await Ensure();

        Assert.That(changed, Is.EqualTo(2));
        foreach (var userName in PreviewSmokeUsers.UserNames)
        {
            var user = await _userManager.FindByNameAsync(userName);
            Assert.That(user, Is.Not.Null, userName);
            var passwordOk = await _userManager.CheckPasswordAsync(user!, Password);
            var roles = await _userManager.GetRolesAsync(user!);
            Assert.Multiple(() =>
            {
                Assert.That(passwordOk, Is.True, userName);
                Assert.That(roles, Is.EquivalentTo(new[] { "Sysadmin", "Commissioner" }), userName);
                Assert.That(user!.DisplayName, Is.EqualTo(userName));
                Assert.That(user.LockoutEnd, Is.Null);
            });
        }
    }

    [TestCase(null)]
    [TestCase("false")]
    public async Task Should_DoNothing_WhenPreviewModeIsOff(string? preview)
    {
        Assert.That(await Ensure(preview: preview), Is.Null);
        Assert.That(await AnySmokeUser(), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    public async Task Should_DoNothing_WhenThePasswordIsNotSet(string? password)
    {
        Assert.That(await Ensure(password: password), Is.Null);
        Assert.That(await AnySmokeUser(), Is.False);
    }

    [Test]
    public async Task Should_BeIdempotent()
    {
        await Ensure();
        var ids = await _context.Users.Where(u => PreviewSmokeUsers.UserNames.Contains(u.UserName!)).Select(u => u.Id).ToListAsync();

        Assert.That(await Ensure(), Is.EqualTo(0));

        var idsAfter = await _context.Users.Where(u => PreviewSmokeUsers.UserNames.Contains(u.UserName!)).Select(u => u.Id).ToListAsync();
        Assert.That(idsAfter, Is.EquivalentTo(ids));
        var user = await _userManager.FindByNameAsync(PreviewSmokeUsers.UserNames[0]);
        Assert.That(await _userManager.GetRolesAsync(user!), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Should_RepairADriftedUser_PasswordRolesAndLockout()
    {
        await Ensure();
        var user = (await _userManager.FindByNameAsync(PreviewSmokeUsers.UserNames[0]))!;
        await _userManager.RemoveFromRoleAsync(user, "Commissioner");
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await _userManager.AccessFailedAsync(user);

        // A rotated secret.
        const string rotated = "Rotated-Smoke-Password-2";
        Assert.That(await Ensure(password: rotated), Is.EqualTo(2), "both users get the new password; the first is also repaired");

        user = (await _userManager.FindByNameAsync(PreviewSmokeUsers.UserNames[0]))!;
        var newOk = await _userManager.CheckPasswordAsync(user, rotated);
        var oldOk = await _userManager.CheckPasswordAsync(user, Password);
        var roles = await _userManager.GetRolesAsync(user);
        Assert.Multiple(() =>
        {
            Assert.That(newOk, Is.True);
            Assert.That(oldOk, Is.False);
            Assert.That(roles, Is.EquivalentTo(new[] { "Sysadmin", "Commissioner" }));
            Assert.That(user.LockoutEnd, Is.Null);
            Assert.That(user.AccessFailedCount, Is.Zero);
        });
    }

    [Test]
    public async Task Should_GiveAPasswordToAnExistingUserWithoutOne()
    {
        // Same name already taken by an account that only signs in externally.
        await _userManager.CreateAsync(new User { UserName = PreviewSmokeUsers.UserNames[1] });

        Assert.That(await Ensure(), Is.EqualTo(2));

        var user = (await _userManager.FindByNameAsync(PreviewSmokeUsers.UserNames[1]))!;
        Assert.That(await _userManager.CheckPasswordAsync(user, Password), Is.True);
    }

    [Test]
    public async Task Should_SkipAndNotThrow_WhenThePasswordIsRefused()
    {
        // Too short and too simple for the identity password rules.
        Assert.That(await Ensure(password: "short"), Is.EqualTo(0));
        Assert.That(await AnySmokeUser(), Is.False);
    }
}
