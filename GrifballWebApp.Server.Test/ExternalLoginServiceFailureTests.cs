using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NSubstitute;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Server.Identity;

namespace GrifballWebApp.Server.Test;

/// <summary>The paths where Identity itself reports a failure, which a real store does not hit for valid input.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ExternalLoginServiceFailureTests
{
    private UserManager<User> _userManager;
    private ExternalLoginService _service;
    private readonly User _user = User.Seed(id: 5, userName: "alice");

    [SetUp]
    public void Setup()
    {
        _userManager = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);
        _userManager.FindByIdAsync("5").Returns(_user);
        _service = new ExternalLoginService(_userManager, Substitute.For<IDiscordAccountLinkService>());
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
