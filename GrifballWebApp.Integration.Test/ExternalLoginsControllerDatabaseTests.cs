using System.Security.Claims;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Identity;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using GrifballWebApp.Application.Identity;

namespace GrifballWebApp.Integration.Test;

/// <summary>The link callback against the real service and database.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ExternalLoginsControllerDatabaseTests
{
    private GrifballContext _context;
    private UserManager<User> _userManager;
    private SignInManager<User> _signInManager;
    private ExternalLoginsController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _userManager = IdentityTestHelpers.NewUserManager(_context);
        _signInManager = Substitute.For<SignInManager<User>>(
            _userManager, Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(), null, null, null, null);
        _controller = new ExternalLoginsController(_signInManager, new ExternalLoginService(_userManager, new DiscordAccountLinkService(_context, Substitute.For<ILogger<DiscordAccountLinkService>>())), new EphemeralDataProtectionProvider());
    }

    [TearDown]
    public async Task TearDown()
    {
        _userManager.Dispose();
        await _context.DropDatabaseAndDispose();
    }

    private async Task<User> NewUser(string name)
    {
        var user = User.Seed(userName: name, passwordHash: "hash");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private void SignedInAs(User user)
    {
        _controller.WithUser(user.Id.ToString(), user.UserName);
        _controller.HttpContext.RequestServices = new ServiceCollection()
            .AddSingleton(Substitute.For<IAuthenticationService>()).BuildServiceProvider();
        _signInManager.GetExternalLoginInfoAsync(user.Id.ToString())
            .Returns(new ExternalLoginInfo(new ClaimsPrincipal(new ClaimsIdentity()), "Discord", "111", "Discord"));
    }

    [Test]
    public async Task LinkCallback_AddsLoginToSignedInUser()
    {
        var user = await NewUser("alice");
        SignedInAs(user);

        var result = await _controller.LinkCallback();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.TypeOf<OkObjectResult>());
            Assert.That((await _userManager.FindByLoginAsync("Discord", "111"))?.Id, Is.EqualTo(user.Id));
        }
    }

    [Test]
    public async Task LinkCallback_RefusesLoginOwnedByAnotherUser()
    {
        var owner = await NewUser("owner");
        _context.UserLogins.Add(new UserLogin { UserId = owner.Id, LoginProvider = "Discord", ProviderKey = "111", ProviderDisplayName = "Discord" });
        await _context.SaveChangesAsync();
        var user = await NewUser("alice");
        SignedInAs(user);

        var result = await _controller.LinkCallback();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.TypeOf<ConflictObjectResult>());
            Assert.That(await _context.UserLogins.AsNoTracking().Select(l => l.UserId).SingleAsync(), Is.EqualTo(owner.Id));
        }
    }
}
