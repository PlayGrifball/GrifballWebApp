using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ExternalLoginServiceTests
{
    private GrifballContext _context;
    private UserManager<User> _userManager;
    private ExternalLoginService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _userManager = IdentityTestHelpers.NewUserManager(_context);
        _service = new ExternalLoginService(_userManager);
    }

    [TearDown]
    public async Task TearDown()
    {
        _userManager.Dispose();
        await _context.DropDatabaseAndDispose();
    }

    private async Task<User> NewUser(string name, bool withPassword = false)
    {
        var user = new User { UserName = name, PasswordHash = withPassword ? "hash" : null };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task AddLogin(User user, string provider, string key)
    {
        _context.UserLogins.Add(new UserLogin { UserId = user.Id, LoginProvider = provider, ProviderKey = key, ProviderDisplayName = provider + " display" });
        await _context.SaveChangesAsync();
    }

    private static UserLoginInfo Discord(string key) => new("Discord", key, "Discord");

    [Test]
    public async Task GetLogins_ReturnsNull_ForMissingUser()
    {
        Assert.That(await _service.GetLogins(12345), Is.Null);
    }

    [Test]
    public async Task GetLogins_ListsLoginsAndPassword()
    {
        var user = await NewUser("alice", withPassword: true);
        await AddLogin(user, "Discord", "1");
        await AddLogin(user, "Google", "g");

        var result = await _service.GetLogins(user.Id);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.HasPassword, Is.True);
            Assert.That(result.Logins.Select(l => l.LoginProvider), Is.EqualTo(new[] { "Discord", "Google" }));
            Assert.That(result.Logins[0].ProviderKey, Is.EqualTo("1"));
            Assert.That(result.Logins[0].ProviderDisplayName, Is.EqualTo("Discord display"));
        });
    }

    [Test]
    public async Task Link_AddsLoginToTheUser_WhoThenSignsInWithIt()
    {
        var user = await NewUser("alice", withPassword: true);

        var result = await _service.Link(user.Id, Discord("111"));

        Assert.That(result, Is.EqualTo(LinkLoginResult.Linked));
        Assert.That((await _userManager.FindByLoginAsync("Discord", "111"))?.Id, Is.EqualTo(user.Id));
    }

    [Test]
    public async Task Link_SameLoginAgain_IsAlreadyLinked()
    {
        var user = await NewUser("alice");
        await AddLogin(user, "Discord", "111");

        Assert.That(await _service.Link(user.Id, Discord("111")), Is.EqualTo(LinkLoginResult.AlreadyLinked));
    }

    [Test]
    public async Task Link_RefusesLoginOwnedByAnotherUser()
    {
        var owner = await NewUser("owner");
        await AddLogin(owner, "Discord", "111");
        var user = await NewUser("alice", withPassword: true);

        var result = await _service.Link(user.Id, Discord("111"));

        Assert.That(result, Is.EqualTo(LinkLoginResult.LinkedToAnotherUser));
        Assert.That((await _userManager.FindByLoginAsync("Discord", "111"))?.Id, Is.EqualTo(owner.Id));
    }

    [Test]
    public async Task Link_MissingUser_IsUserNotFound()
    {
        Assert.That(await _service.Link(12345, Discord("111")), Is.EqualTo(LinkLoginResult.UserNotFound));
    }

    [Test]
    public async Task Unlink_RemovesLogin_WhenUserHasPassword()
    {
        var user = await NewUser("alice", withPassword: true);
        await AddLogin(user, "Discord", "111");

        Assert.That(await _service.Unlink(user.Id, "Discord", "111"), Is.EqualTo(UnlinkLoginResult.Removed));
        Assert.That(await _context.UserLogins.AsNoTracking().AnyAsync(), Is.False);
    }

    [Test]
    public async Task Unlink_RemovesLogin_WhenAnotherLoginRemains()
    {
        var user = await NewUser("alice");
        await AddLogin(user, "Discord", "111");
        await AddLogin(user, "Discord", "222");

        Assert.That(await _service.Unlink(user.Id, "Discord", "111"), Is.EqualTo(UnlinkLoginResult.Removed));
        Assert.That((await _context.UserLogins.AsNoTracking().SingleAsync()).ProviderKey, Is.EqualTo("222"));
    }

    [Test]
    public async Task Unlink_RefusesLastSignInMethod()
    {
        var user = await NewUser("alice");
        await AddLogin(user, "Discord", "111");

        Assert.That(await _service.Unlink(user.Id, "Discord", "111"), Is.EqualTo(UnlinkLoginResult.LastSignInMethod));
        Assert.That(await _context.UserLogins.AsNoTracking().CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Unlink_SomeoneElsesLogin_IsLoginNotFound()
    {
        var owner = await NewUser("owner");
        await AddLogin(owner, "Discord", "111");
        var user = await NewUser("alice", withPassword: true);

        Assert.That(await _service.Unlink(user.Id, "Discord", "111"), Is.EqualTo(UnlinkLoginResult.LoginNotFound));
        Assert.That(await _context.UserLogins.AsNoTracking().CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Unlink_MissingUser_IsUserNotFound()
    {
        Assert.That(await _service.Unlink(12345, "Discord", "111"), Is.EqualTo(UnlinkLoginResult.UserNotFound));
    }
}

/// <summary>The paths where Identity itself reports a failure, which a real store does not hit for valid input.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ExternalLoginServiceFailureTests
{
    private UserManager<User> _userManager;
    private ExternalLoginService _service;
    private readonly User _user = new() { Id = 5, UserName = "alice" };

    [SetUp]
    public void Setup()
    {
        _userManager = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);
        _userManager.FindByIdAsync("5").Returns(_user);
        _service = new ExternalLoginService(_userManager);
    }

    [TearDown]
    public void TearDown() => _userManager.Dispose();

    [Test]
    public async Task Link_IdentityFailure_IsFailed()
    {
        var info = new UserLoginInfo("Discord", "111", "Discord");
        _userManager.AddLoginAsync(_user, info).Returns(IdentityResult.Failed());

        Assert.That(await _service.Link(5, info), Is.EqualTo(LinkLoginResult.Failed));
    }

    [Test]
    public async Task Unlink_IdentityFailure_IsFailed()
    {
        _userManager.GetLoginsAsync(_user).Returns([new UserLoginInfo("Discord", "111", null)]);
        _userManager.HasPasswordAsync(_user).Returns(true);
        _userManager.RemoveLoginAsync(_user, "Discord", "111").Returns(IdentityResult.Failed());

        Assert.That(await _service.Unlink(5, "Discord", "111"), Is.EqualTo(UnlinkLoginResult.Failed));
    }

    [Test]
    public async Task GetLogins_FallsBackToProviderName_WithoutDisplayName()
    {
        _userManager.GetLoginsAsync(_user).Returns([new UserLoginInfo("Discord", "111", null)]);

        var result = await _service.GetLogins(5);

        Assert.That(result!.Logins.Single().ProviderDisplayName, Is.EqualTo("Discord"));
    }
}
