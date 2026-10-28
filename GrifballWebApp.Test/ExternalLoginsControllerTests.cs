using System.Security.Claims;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ExternalLoginsControllerTests
{
    private UserManager<User> _userManager;
    private SignInManager<User> _signInManager;
    private IExternalLoginService _service;
    private IAuthenticationService _authenticationService;
    private IDataProtectionProvider _dataProtection;
    private ExternalLoginsController _controller;

    private static readonly AuthenticationScheme DiscordScheme = new("Discord", "Discord", typeof(IAuthenticationHandler));

    [SetUp]
    public void Setup()
    {
        _userManager = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);
        _signInManager = Substitute.For<SignInManager<User>>(
            _userManager, Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(), null, null, null, null);
        _signInManager.GetExternalAuthenticationSchemesAsync().Returns([DiscordScheme]);
        _service = Substitute.For<IExternalLoginService>();
        _authenticationService = Substitute.For<IAuthenticationService>();
        _dataProtection = new EphemeralDataProtectionProvider();
        _controller = NewController(_service);
    }

    [TearDown]
    public void TearDown() => _userManager.Dispose();

    private ExternalLoginsController NewController(IExternalLoginService service) =>
        new(_signInManager, service, _dataProtection);

    /// <summary>Signed in as the given user, with request services so HttpContext.SignOutAsync works.</summary>
    private ExternalLoginsController AsUser(ExternalLoginsController controller, string? userId, params string[] roles)
    {
        controller.WithUser(userId, "alice", roles);
        controller.HttpContext.RequestServices = new ServiceCollection().AddSingleton(_authenticationService).BuildServiceProvider();
        return controller;
    }

    private string Token(string payload, TimeSpan lifetime) =>
        _dataProtection.CreateProtector(ExternalLoginsController.LinkTokenPurpose).ToTimeLimitedDataProtector().Protect(payload, lifetime);

    private static ExternalLoginInfo LoginInfo(string key = "111") =>
        new(new ClaimsPrincipal(new ClaimsIdentity()), "Discord", key, "Discord");

    // ---------- Authorization ----------

    [Test]
    public void Controller_RequiresSignIn_AndAdminActionsRequireSysadmin()
    {
        var type = typeof(ExternalLoginsController);
        Assert.Multiple(() =>
        {
            Assert.That(ControllerTestHelpers.ClassAuthorize(type), Is.Not.Null);
            Assert.That(ControllerTestHelpers.ActionAllowsAnonymous(type, nameof(ExternalLoginsController.Providers)), Is.True);
            Assert.That(ControllerTestHelpers.ActionAllowsAnonymous(type, nameof(ExternalLoginsController.Link)), Is.True);
            Assert.That(ControllerTestHelpers.ActionAllowsAnonymous(type, nameof(ExternalLoginsController.LinkCallback)), Is.False);
            Assert.That(ControllerTestHelpers.ActionAllowsAnonymous(type, nameof(ExternalLoginsController.Unlink)), Is.False);
            Assert.That(ControllerTestHelpers.ActionAuthorize(type, nameof(ExternalLoginsController.UserLogins))?.Roles, Is.EqualTo("Sysadmin"));
            Assert.That(ControllerTestHelpers.ActionAuthorize(type, nameof(ExternalLoginsController.RemoveUserLogin))?.Roles, Is.EqualTo("Sysadmin"));
        });
    }

    // ---------- Providers / Mine ----------

    [Test]
    public async Task Providers_ListsConfiguredSchemes()
    {
        _signInManager.GetExternalAuthenticationSchemesAsync().Returns([DiscordScheme, new AuthenticationScheme("Other", null, typeof(IAuthenticationHandler))]);

        var providers = await _controller.Providers();

        Assert.That(providers.Select(p => (p.Name, p.DisplayName)), Is.EqualTo(new[] { ("Discord", "Discord"), ("Other", "Other") }));
    }

    [Test]
    public async Task Mine_ReturnsLoginsAndProviders()
    {
        _service.GetLogins(5).Returns(new LinkedLoginsDto { HasPassword = true, Logins = [] });

        var result = await AsUser(_controller, "5").Mine();

        var dto = (LinkedLoginsDto)((OkObjectResult)result).Value!;
        Assert.That(dto.HasPassword, Is.True);
        Assert.That(dto.Providers.Single().Name, Is.EqualTo("Discord"));
    }

    [Test]
    public async Task Mine_MissingUser_IsNotFound()
    {
        Assert.That(await AsUser(_controller, "5").Mine(), Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task Actions_WithoutUserId_AreUnauthorized()
    {
        AsUser(_controller, null);
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await _controller.Mine(), Is.TypeOf<UnauthorizedResult>());
            Assert.That(await _controller.StartLink("Discord"), Is.TypeOf<UnauthorizedResult>());
            Assert.That(await _controller.LinkCallback(), Is.TypeOf<UnauthorizedResult>());
            Assert.That(await _controller.Unlink(new ExternalLoginKeyDto { LoginProvider = "Discord", ProviderKey = "1" }), Is.TypeOf<UnauthorizedResult>());
        });
    }

    // ---------- StartLink / Link ----------

    [Test]
    public async Task StartLink_UnknownProvider_IsBadRequest()
    {
        var result = await AsUser(_controller, "5").StartLink("Nope");

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Unknown login provider 'Nope'"));
    }

    [Test]
    public async Task StartLink_ThenLink_ChallengesProviderForTheSignedInUser()
    {
        var properties = new AuthenticationProperties();
        _signInManager.ConfigureExternalAuthenticationProperties("Discord", "profile/5?linkCallback=true", "5").Returns(properties);

        var start = await AsUser(_controller, "5").StartLink("Discord");
        var token = ((StartLinkResponseDto)((OkObjectResult)start).Value!).Token;

        // The navigation to Link carries no bearer token: the user comes from the protected token alone.
        var link = NewController(_service).WithAnonymousUser();
        var result = await link.Link("Discord", token);

        Assert.That(result, Is.TypeOf<ChallengeResult>());
        var challenge = (ChallengeResult)result;
        Assert.That(challenge.AuthenticationSchemes, Is.EqualTo(new[] { "Discord" }));
        Assert.That(challenge.Properties, Is.SameAs(properties));
        _signInManager.Received(1).ConfigureExternalAuthenticationProperties("Discord", "profile/5?linkCallback=true", "5");
    }

    [Test]
    public async Task Link_TamperedToken_IsBadRequest()
    {
        var result = await _controller.WithAnonymousUser().Link("Discord", "not-a-token");

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        _signInManager.DidNotReceiveWithAnyArgs().ConfigureExternalAuthenticationProperties(default, default, default);
    }

    [Test]
    public async Task Link_ExpiredToken_IsBadRequest()
    {
        var result = await _controller.WithAnonymousUser().Link("Discord", Token("5:Discord", TimeSpan.FromMinutes(-1)));

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
    }

    [TestCase("5:Other", TestName = "Link_TokenForAnotherProvider_IsBadRequest")]
    [TestCase("garbage", TestName = "Link_MalformedPayload_IsBadRequest")]
    [TestCase("x:Discord", TestName = "Link_NonNumericUser_IsBadRequest")]
    public async Task Link_BadPayload_IsBadRequest(string payload)
    {
        var result = await _controller.WithAnonymousUser().Link("Discord", Token(payload, TimeSpan.FromMinutes(5)));

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Link_ProviderNoLongerConfigured_IsBadRequest()
    {
        var token = Token("5:Gone", TimeSpan.FromMinutes(5));

        var result = await _controller.WithAnonymousUser().Link("Gone", token);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Unknown login provider 'Gone'"));
    }

    [Test]
    public async Task LinkCallback_WithoutMatchingExternalLogin_IsBadRequest()
    {
        // StartLink for user 5, then the protected token is fed back as is: user 6's callback
        // asks Identity for the login with its own id, which does not match the stored XSRF key.
        _signInManager.GetExternalLoginInfoAsync("6").Returns((ExternalLoginInfo?)null);

        var result = await AsUser(_controller, "6").LinkCallback();

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("The link request expired or was started by a different account. Try again."));
        await _service.DidNotReceiveWithAnyArgs().Link(default, default!);
    }

    // ---------- LinkCallback ----------

    [TestCase(LinkLoginResult.Linked)]
    [TestCase(LinkLoginResult.AlreadyLinked)]
    public async Task LinkCallback_Linked_ReturnsLoginAndDropsExternalCookie(LinkLoginResult outcome)
    {
        var info = LoginInfo();
        _signInManager.GetExternalLoginInfoAsync("5").Returns(info);
        _service.Link(5, info).Returns(outcome);

        var result = await AsUser(_controller, "5").LinkCallback();

        var dto = (ExternalLoginDto)((OkObjectResult)result).Value!;
        Assert.That((dto.LoginProvider, dto.ProviderKey, dto.ProviderDisplayName), Is.EqualTo(("Discord", "111", "Discord")));
        await _authenticationService.Received(1).SignOutAsync(Arg.Any<HttpContext>(), IdentityConstants.ExternalScheme, Arg.Any<AuthenticationProperties?>());
    }

    [Test]
    public async Task LinkCallback_LoginOwnedByAnotherAccount_IsConflictSuggestingMerge()
    {
        var info = LoginInfo();
        _signInManager.GetExternalLoginInfoAsync("5").Returns(info);
        _service.Link(5, info).Returns(LinkLoginResult.LinkedToAnotherUser);

        var result = await AsUser(_controller, "5").LinkCallback();

        Assert.That(result, Is.TypeOf<ConflictObjectResult>());
        Assert.That((string)((ConflictObjectResult)result).Value!, Does.Contain("already linked to another Grifball account").And.Contain("merge"));
    }

    [TestCase(LinkLoginResult.UserNotFound, typeof(NotFoundObjectResult))]
    [TestCase(LinkLoginResult.Failed, typeof(BadRequestObjectResult))]
    public async Task LinkCallback_OtherOutcomes(LinkLoginResult outcome, Type expected)
    {
        var info = LoginInfo();
        _signInManager.GetExternalLoginInfoAsync("5").Returns(info);
        _service.Link(5, info).Returns(outcome);

        Assert.That(await AsUser(_controller, "5").LinkCallback(), Is.TypeOf(expected));
    }

    // ---------- Unlink / admin ----------

    [TestCase(UnlinkLoginResult.Removed, typeof(OkResult))]
    [TestCase(UnlinkLoginResult.LastSignInMethod, typeof(BadRequestObjectResult))]
    [TestCase(UnlinkLoginResult.LoginNotFound, typeof(NotFoundObjectResult))]
    [TestCase(UnlinkLoginResult.UserNotFound, typeof(NotFoundObjectResult))]
    [TestCase(UnlinkLoginResult.Failed, typeof(BadRequestObjectResult))]
    public async Task Unlink_MapsOutcome(UnlinkLoginResult outcome, Type expected)
    {
        _service.Unlink(5, "Discord", "111").Returns(outcome);

        var result = await AsUser(_controller, "5").Unlink(new ExternalLoginKeyDto { LoginProvider = "Discord", ProviderKey = "111" });

        Assert.That(result, Is.TypeOf(expected));
    }

    [Test]
    public async Task Unlink_LastMethod_ExplainsWhy()
    {
        _service.Unlink(5, "Discord", "111").Returns(UnlinkLoginResult.LastSignInMethod);

        var result = await AsUser(_controller, "5").Unlink(new ExternalLoginKeyDto { LoginProvider = "Discord", ProviderKey = "111" });

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("This is your only way to sign in. Link another provider or set a password first."));
    }

    [Test]
    public async Task UserLogins_ReturnsTheUsersLogins_OrNotFound()
    {
        var dto = new LinkedLoginsDto { HasPassword = false, Logins = [] };
        _service.GetLogins(7).Returns(dto);
        AsUser(_controller, "1", "Sysadmin");

        Assert.That(((OkObjectResult)await _controller.UserLogins(7)).Value, Is.SameAs(dto));
        Assert.That(await _controller.UserLogins(8), Is.TypeOf<NotFoundObjectResult>());
    }

    [Test]
    public async Task RemoveUserLogin_RemovesFromTheGivenUser_AndRefusesTheirLastMethod()
    {
        _service.Unlink(7, "Discord", "111").Returns(UnlinkLoginResult.Removed);
        _service.Unlink(8, "Discord", "222").Returns(UnlinkLoginResult.LastSignInMethod);
        AsUser(_controller, "1", "Sysadmin");

        Assert.That(await _controller.RemoveUserLogin(7, new ExternalLoginKeyDto { LoginProvider = "Discord", ProviderKey = "111" }), Is.TypeOf<OkResult>());
        var refused = await _controller.RemoveUserLogin(8, new ExternalLoginKeyDto { LoginProvider = "Discord", ProviderKey = "222" });
        Assert.That((string)((BadRequestObjectResult)refused).Value!, Does.StartWith("This is the user's only way to sign in."));
    }
}

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
        _controller = new ExternalLoginsController(_signInManager, new ExternalLoginService(_userManager), new EphemeralDataProtectionProvider());
    }

    [TearDown]
    public async Task TearDown()
    {
        _userManager.Dispose();
        await _context.DropDatabaseAndDispose();
    }

    private async Task<User> NewUser(string name)
    {
        var user = new User { UserName = name, PasswordHash = "hash" };
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

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        Assert.That((await _userManager.FindByLoginAsync("Discord", "111"))?.Id, Is.EqualTo(user.Id));
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

        Assert.That(result, Is.TypeOf<ConflictObjectResult>());
        Assert.That(await _context.UserLogins.AsNoTracking().Select(l => l.UserId).SingleAsync(), Is.EqualTo(owner.Id));
    }
}
