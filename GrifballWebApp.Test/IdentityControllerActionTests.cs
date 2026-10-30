using System.Security.Claims;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.Identity;
using GrifballWebApp.Server.UserManagement;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;
using MvcSignInResult = Microsoft.AspNetCore.Mvc.SignInResult;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class IdentityControllerActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private UserManager<User> _userManager;
    private SignInManager<User> _signInManager;
    private IOptionsMonitor<BearerTokenOptions> _optionsMonitor;
    private ISecureDataFormat<AuthenticationTicket> _refreshProtector;
    private IUserManagementService _userManagementService;
    private IdentityController _controller;

    [SetUp]
    public void Setup()
    {
        _userManager = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);
        _signInManager = Substitute.For<SignInManager<User>>(
            _userManager, Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(), null, null, null, null);
        _refreshProtector = Substitute.For<ISecureDataFormat<AuthenticationTicket>>();
        _optionsMonitor = Substitute.For<IOptionsMonitor<BearerTokenOptions>>();
        _optionsMonitor.Get(IdentityConstants.BearerScheme).Returns(new BearerTokenOptions { RefreshTokenProtector = _refreshProtector });
        _userManagementService = Substitute.For<IUserManagementService>();

        _controller = new IdentityController(Substitute.For<ILogger<IdentityController>>(), _userManager, _signInManager,
            _optionsMonitor, new FixedTimeProvider(Now), _userManagementService);
    }

    // ---------- Login ----------

    [Test]
    public async Task Login_NullDto_ReturnsUsernameRequired()
    {
        var result = await _controller.Login(null!, CancellationToken.None);

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Username is required"));
    }

    [Test]
    public async Task Login_MissingPassword_ReturnsPasswordRequired()
    {
        var result = await _controller.Login(new LoginDto { Username = "bob", Password = "" }, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Password is required"));
        await _signInManager.DidNotReceiveWithAnyArgs().PasswordSignInAsync(default(string)!, default!, default, default);
    }

    [Test]
    public async Task Login_Success_ReturnsEmptyAndUsesCookieSchemeWithLockout()
    {
        _signInManager.PasswordSignInAsync("bob", "pw", true, true).Returns(IdentitySignInResult.Success);

        var result = await _controller.Login(new LoginDto { Username = "bob", Password = "pw" }, CancellationToken.None);

        Assert.That(result, Is.TypeOf<EmptyResult>());
        Assert.That(_signInManager.AuthenticationScheme, Is.EqualTo(IdentityConstants.ApplicationScheme));
        await _signInManager.Received(1).PasswordSignInAsync("bob", "pw", true, lockoutOnFailure: true);
    }

    [Test]
    public async Task Login_WithoutCookies_UsesBearerScheme()
    {
        _signInManager.PasswordSignInAsync("bob", "pw", true, true).Returns(IdentitySignInResult.Success);

        var result = await _controller.Login(new LoginDto { Username = "bob", Password = "pw" }, CancellationToken.None, useCookies: false);

        Assert.That(result, Is.TypeOf<EmptyResult>());
        Assert.That(_signInManager.AuthenticationScheme, Is.EqualTo(IdentityConstants.BearerScheme));
    }

    [Test]
    public async Task Login_BadCredentials_ReturnsUnauthorized()
    {
        _signInManager.PasswordSignInAsync("bob", "wrong", true, true).Returns(IdentitySignInResult.Failed);

        var result = await _controller.Login(new LoginDto { Username = "bob", Password = "wrong" }, CancellationToken.None);

        Assert.That(result, Is.TypeOf<UnauthorizedResult>());
    }

    [Test]
    public async Task Login_LockedOut_ReturnsUnauthorized()
    {
        _signInManager.PasswordSignInAsync("bob", "pw", true, true).Returns(IdentitySignInResult.LockedOut);

        var result = await _controller.Login(new LoginDto { Username = "bob", Password = "pw" }, CancellationToken.None);

        Assert.That(result, Is.TypeOf<UnauthorizedResult>());
    }

    // ---------- Register ----------

    [Test]
    public async Task Register_MissingUsername_ReturnsBadRequest()
    {
        var result = await _controller.Register(new RegisterDto { Username = "", Password = "pw" }, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Username is required"));
        await _userManager.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!);
    }

    [Test]
    public async Task Register_MissingPassword_ReturnsBadRequest()
    {
        var result = await _controller.Register(new RegisterDto { Username = "bob", Password = null }, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Password is required"));
    }

    [Test]
    public async Task Register_ValidationErrors_ReturnsAllErrorsInBadRequest()
    {
        _userManager.CreateAsync(Arg.Any<User>(), "short").Returns(IdentityResult.Failed(
            new IdentityError { Code = "PasswordTooShort", Description = "Too short" },
            new IdentityError { Code = "DuplicateUserName", Description = "Taken" }));

        var result = await _controller.Register(new RegisterDto { Username = "bob", Password = "short" }, CancellationToken.None);

        var value = (string)((BadRequestObjectResult)result).Value!;
        Assert.That(value, Is.EqualTo($"PasswordTooShort: Too short{Environment.NewLine}DuplicateUserName: Taken{Environment.NewLine}"));
    }

    [Test]
    public async Task Register_Success_CreatesUserWithUsername()
    {
        _userManager.CreateAsync(Arg.Any<User>(), "Secret123!").Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<User>(), "Player").Returns(IdentityResult.Success);

        var result = await _controller.Register(new RegisterDto { Username = "bob", Password = "Secret123!" }, CancellationToken.None);

        Assert.That(result, Is.TypeOf<OkResult>());
        await _userManager.Received(1).CreateAsync(Arg.Is<User>(u => u.UserName == "bob"), "Secret123!");
        await _userManager.Received(1).AddToRoleAsync(Arg.Is<User>(u => u.UserName == "bob"), "Player");
    }

    [Test]
    public async Task Register_AddRoleFails_ReturnsFailedToAddRole()
    {
        _userManager.CreateAsync(Arg.Any<User>(), "Secret123!").Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<User>(), "Player").Returns(IdentityResult.Failed(new IdentityError { Code = "x", Description = "y" }));

        var result = await _controller.Register(new RegisterDto { Username = "bob", Password = "Secret123!" }, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Failed to add role"));
    }

    // ---------- ExternalLogin ----------

    [Test]
    public void ExternalLogin_NoFollowUp_ChallengesDiscordWithCallbackUrl()
    {
        var props = new AuthenticationProperties();
        _signInManager.ConfigureExternalAuthenticationProperties("Discord", "login?callback=true").Returns(props);

        var result = _controller.ExternalLogin(null);

        var challenge = (ChallengeResult)result;
        Assert.That(challenge.AuthenticationSchemes, Is.EqualTo(new[] { "Discord" }));
        Assert.That(challenge.Properties, Is.SameAs(props));
    }

    // The query value arrives decoded; it goes back out escaped, as one query value
    [TestCase("/season/3", "%2Fseason%2F3")]
    [TestCase("/season/3?tab=bracket&x=1", "%2Fseason%2F3%3Ftab%3Dbracket%26x%3D1")]
    [TestCase("/profile/5#linked", "%2Fprofile%2F5%23linked")]
    [TestCase("/a b", "%2Fa%20b")]
    public void ExternalLogin_WithFollowUp_AppendsItEscaped(string followUp, string escaped)
    {
        var props = new AuthenticationProperties();
        _signInManager.ConfigureExternalAuthenticationProperties("Discord", Arg.Any<string>()).Returns(props);

        var result = _controller.ExternalLogin(followUp);

        Assert.That(result, Is.TypeOf<ChallengeResult>());
        _signInManager.Received(1).ConfigureExternalAuthenticationProperties("Discord", "login?callback=true&followUp=" + escaped);
    }

    [TestCase("https://evil.example/")]
    [TestCase("//evil.example")]
    [TestCase("/\\evil.example")]
    [TestCase("javascript:alert(1)")]
    [TestCase("season/3")]
    [TestCase("")]
    [TestCase("/a\nb")]
    public void ExternalLogin_WithAFollowUpOffThisSite_DropsIt(string followUp)
    {
        _signInManager.ConfigureExternalAuthenticationProperties("Discord", Arg.Any<string>()).Returns(new AuthenticationProperties());

        _controller.ExternalLogin(followUp);

        _signInManager.Received(1).ConfigureExternalAuthenticationProperties("Discord", "login?callback=true");
    }

    [TestCase("/", true)]
    [TestCase("/season/3?tab=bracket", true)]
    [TestCase("//evil.example", false)]
    [TestCase("/\\evil.example", false)]
    [TestCase("https://evil.example", false)]
    [TestCase(null, false)]
    public void IsAppRelativePath_Cases(string? path, bool expected)
        => Assert.That(IdentityController.IsAppRelativePath(path), Is.EqualTo(expected));

    // ---------- ExternalLoginCallback ----------

    private static ExternalLoginInfo Info(string? name = "discordName", string? email = "a@b.c")
    {
        var claims = new List<Claim>();
        if (name is not null) claims.Add(new Claim(ClaimTypes.Name, name));
        if (email is not null) claims.Add(new Claim(ClaimTypes.Email, email));
        claims.Add(new Claim("urn:discord:avatar", "abc"));
        return new ExternalLoginInfo(new ClaimsPrincipal(new ClaimsIdentity(claims, "Discord")), "Discord", "key-1", "Discord");
    }

    private void ExternalResult(IdentitySignInResult r) =>
        _signInManager.ExternalLoginSignInAsync("Discord", "key-1", true, true).Returns(r);

    [Test]
    public async Task ExternalLoginCallback_NoInfo_ReturnsMissingInfo()
    {
        _signInManager.GetExternalLoginInfoAsync().Returns((ExternalLoginInfo?)null);

        var result = await _controller.ExternalLoginCallback(false, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Missing info"));
    }

    [TestCase(true, "Identity.Application")]
    [TestCase(false, "Identity.Bearer")]
    public async Task ExternalLoginCallback_ExistingLogin_ReturnsEmptyWithSchemeForUseCookies(bool useCookies, string scheme)
    {
        _signInManager.GetExternalLoginInfoAsync().Returns(Info());
        ExternalResult(IdentitySignInResult.Success);

        var result = await _controller.ExternalLoginCallback(useCookies, CancellationToken.None);

        Assert.That(result, Is.TypeOf<EmptyResult>());
        Assert.That(_signInManager.AuthenticationScheme, Is.EqualTo(scheme));
        await _userManager.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Test]
    public async Task ExternalLoginCallback_LockedOut_ReturnsLocked()
    {
        _signInManager.GetExternalLoginInfoAsync().Returns(Info());
        ExternalResult(IdentitySignInResult.LockedOut);

        var result = await _controller.ExternalLoginCallback(false, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Locked"));
    }

    [Test]
    public async Task ExternalLoginCallback_RequiresTwoFactor_Returns2FA()
    {
        _signInManager.GetExternalLoginInfoAsync().Returns(Info());
        ExternalResult(IdentitySignInResult.TwoFactorRequired);

        var result = await _controller.ExternalLoginCallback(false, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Requires 2FA"));
    }

    [Test]
    public async Task ExternalLoginCallback_NotAllowed_ReturnsNotAllowed()
    {
        _signInManager.GetExternalLoginInfoAsync().Returns(Info());
        ExternalResult(IdentitySignInResult.NotAllowed);

        var result = await _controller.ExternalLoginCallback(false, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Not allowed"));
    }

    [Test]
    public async Task ExternalLoginCallback_EmailAlreadyUsed_ReturnsAlreadyAUser()
    {
        _signInManager.GetExternalLoginInfoAsync().Returns(Info());
        ExternalResult(IdentitySignInResult.Failed);
        _userManager.FindByEmailAsync("a@b.c").Returns(new User { UserName = "other" });

        var result = await _controller.ExternalLoginCallback(false, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Already a user"));
        await _userManager.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Test]
    public async Task ExternalLoginCallback_NameAlreadyUsed_ReturnsAlreadyAUser()
    {
        _signInManager.GetExternalLoginInfoAsync().Returns(Info(email: null));
        ExternalResult(IdentitySignInResult.Failed);
        _userManager.FindByNameAsync("discordName").Returns(new User { UserName = "discordName" });

        var result = await _controller.ExternalLoginCallback(false, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Already a user"));
        await _userManager.DidNotReceiveWithAnyArgs().FindByEmailAsync(default!);
    }

    [Test]
    public async Task ExternalLoginCallback_MissingName_Throws()
    {
        _signInManager.GetExternalLoginInfoAsync().Returns(Info(name: null));
        ExternalResult(IdentitySignInResult.Failed);

        var ex = await Assert.ThrowsAsync<Exception>(() => _controller.ExternalLoginCallback(false, CancellationToken.None));
        Assert.That(ex!.Message, Is.EqualTo("Missing name"));
    }

    [Test]
    public async Task ExternalLoginCallback_CreateFails_ReturnsErrors()
    {
        _signInManager.GetExternalLoginInfoAsync().Returns(Info());
        ExternalResult(IdentitySignInResult.Failed);
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Failed(new IdentityError { Code = "InvalidUserName", Description = "Bad name" }));

        var result = await _controller.ExternalLoginCallback(false, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo($"Errors: InvalidUserName: Bad name{Environment.NewLine}"));
        await _userManager.DidNotReceiveWithAnyArgs().AddLoginAsync(default!, default!);
    }

    [Test]
    public async Task ExternalLoginCallback_AddLoginFails_ReturnsFailedToAddLogin()
    {
        var info = Info();
        _signInManager.GetExternalLoginInfoAsync().Returns(info);
        ExternalResult(IdentitySignInResult.Failed);
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<User>(), "Player").Returns(IdentityResult.Success);
        _userManager.AddLoginAsync(Arg.Any<User>(), info).Returns(IdentityResult.Failed());

        var result = await _controller.ExternalLoginCallback(false, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Failed to add login"));
        await _signInManager.DidNotReceiveWithAnyArgs().SignInAsync(default!, default(bool), default);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ExternalLoginCallback_NewUser_CreatesUserAddsRoleLoginClaimsAndSignsIn(bool useCookies)
    {
        var info = Info();
        _signInManager.GetExternalLoginInfoAsync().Returns(info);
        ExternalResult(IdentitySignInResult.Failed);
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<User>(), "Player").Returns(IdentityResult.Success);
        _userManager.AddLoginAsync(Arg.Any<User>(), info).Returns(IdentityResult.Success);
        var authentication = Substitute.For<IAuthenticationService>();
        _controller.WithAnonymousUser();
        _controller.HttpContext.RequestServices = new ServiceCollection().AddSingleton(authentication).BuildServiceProvider();

        var result = await _controller.ExternalLoginCallback(useCookies, CancellationToken.None);

        // Empty for both: for a bearer token, SignInAsync has already written the response
        Assert.That(result, Is.TypeOf<EmptyResult>());
        // Discord's half-finished sign-in cookie is dropped, before signing in (a bearer sign-in writes the response)
        Received.InOrder(() =>
        {
            authentication.SignOutAsync(Arg.Any<HttpContext>(), IdentityConstants.ExternalScheme, Arg.Any<AuthenticationProperties?>());
            _signInManager.SignInAsync(Arg.Any<User>(), true, null);
        });
        await _userManager.Received(1).CreateAsync(Arg.Is<User>(u => u.UserName == "discordName" && u.Email == "a@b.c"));
        await _userManager.Received(1).AddToRoleAsync(Arg.Is<User>(u => u.UserName == "discordName"), "Player");
        await _userManager.Received(1).AddClaimsAsync(Arg.Any<User>(), Arg.Is<IEnumerable<Claim>>(c => c.Count() == 3));
        await _signInManager.Received(1).SignInAsync(Arg.Is<User>(u => u.UserName == "discordName"), true, null);
    }

    [Test]
    public async Task ExternalLoginCallback_AddRoleFails_ReturnsFailedToAddRole()
    {
        var info = Info();
        _signInManager.GetExternalLoginInfoAsync().Returns(info);
        ExternalResult(IdentitySignInResult.Failed);
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<User>(), "Player").Returns(IdentityResult.Failed(new IdentityError { Code = "x", Description = "y" }));
        _userManager.AddLoginAsync(Arg.Any<User>(), info).Returns(IdentityResult.Success);

        var result = await _controller.ExternalLoginCallback(false, CancellationToken.None);

        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("Failed to add role"));
        await _userManager.DidNotReceiveWithAnyArgs().AddLoginAsync(default!, default!);
        await _signInManager.DidNotReceiveWithAnyArgs().SignInAsync(default!, default(bool), default);
    }

    // ---------- Refresh ----------

    private static AuthenticationTicket Ticket(DateTimeOffset? expires)
    {
        var props = new AuthenticationProperties { ExpiresUtc = expires };
        return new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "5")], "Bearer")), props, IdentityConstants.BearerScheme);
    }

    [Test]
    public async Task Refresh_InvalidToken_Challenges()
    {
        _refreshProtector.Unprotect("bad").Returns((AuthenticationTicket?)null);

        var result = await _controller.Refresh(new RefreshRequest { RefreshToken = "bad" });

        Assert.That(result, Is.TypeOf<ChallengeResult>());
        await _signInManager.DidNotReceiveWithAnyArgs().ValidateSecurityStampAsync(default(ClaimsPrincipal));
    }

    [Test]
    public async Task Refresh_NoExpiry_Challenges()
    {
        _refreshProtector.Unprotect("tok").Returns(Ticket(null));

        var result = await _controller.Refresh(new RefreshRequest { RefreshToken = "tok" });

        Assert.That(result, Is.TypeOf<ChallengeResult>());
    }

    [Test]
    public async Task Refresh_Expired_Challenges()
    {
        _refreshProtector.Unprotect("tok").Returns(Ticket(Now));

        var result = await _controller.Refresh(new RefreshRequest { RefreshToken = "tok" });

        Assert.That(result, Is.TypeOf<ChallengeResult>());
        await _signInManager.DidNotReceiveWithAnyArgs().ValidateSecurityStampAsync(default(ClaimsPrincipal));
    }

    [Test]
    public async Task Refresh_SecurityStampInvalid_Challenges()
    {
        var ticket = Ticket(Now.AddMinutes(1));
        _refreshProtector.Unprotect("tok").Returns(ticket);
        _signInManager.ValidateSecurityStampAsync(ticket.Principal).Returns((User?)null);

        var result = await _controller.Refresh(new RefreshRequest { RefreshToken = "tok" });

        Assert.That(result, Is.TypeOf<ChallengeResult>());
    }

    [Test]
    public async Task Refresh_Valid_SignsInNewPrincipalWithBearerScheme()
    {
        var ticket = Ticket(Now.AddDays(1));
        var user = new User { UserName = "bob" };
        var newPrincipal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "bob")], "Bearer"));
        _refreshProtector.Unprotect("tok").Returns(ticket);
        _signInManager.ValidateSecurityStampAsync(ticket.Principal).Returns(user);
        _signInManager.CreateUserPrincipalAsync(user).Returns(newPrincipal);

        var result = await _controller.Refresh(new RefreshRequest { RefreshToken = "tok" });

        var signIn = (MvcSignInResult)result;
        Assert.That(signIn.Principal, Is.SameAs(newPrincipal));
        Assert.That(signIn.AuthenticationScheme, Is.EqualTo(IdentityConstants.BearerScheme));
    }

    // ---------- MetaInfo ----------

    [Test]
    public void MetaInfo_ReflectsRolesNameAndId()
    {
        _controller.WithUser("17", "Bob", "Sysadmin", "Player");

        var meta = _controller.MetaInfo();

        Assert.That(meta, Is.EqualTo(new MetaInfoResponse
        {
            IsSysAdmin = true,
            IsCommissioner = false,
            IsPlayer = true,
            DisplayName = "Bob",
            UserID = 17,
        }));
    }

    [Test]
    public void MetaInfo_CommissionerOnly()
    {
        _controller.WithUser("3", "Comm", "Commissioner");

        var meta = _controller.MetaInfo();

        Assert.Multiple(() =>
        {
            Assert.That(meta.IsCommissioner, Is.True);
            Assert.That(meta.IsSysAdmin, Is.False);
            Assert.That(meta.IsPlayer, Is.False);
        });
    }

    [Test]
    public void MetaInfo_NoNameNoId_DefaultsToFriendAndZero()
    {
        _controller.WithAnonymousUser();

        var meta = _controller.MetaInfo();

        Assert.That(meta.DisplayName, Is.EqualTo("Friend"));
        Assert.That(meta.UserID, Is.EqualTo(0));
    }

    [Test]
    public void MetaInfo_NonNumericId_ReturnsZero()
    {
        _controller.WithUser("not-a-number", "Bob");

        Assert.That(_controller.MetaInfo().UserID, Is.EqualTo(0));
    }

    [Test]
    public void ExternalLoginCallback_IsPostOnly()
    {
        var attributes = typeof(IdentityController).GetMethod(nameof(IdentityController.ExternalLoginCallback))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute), true)
            .Cast<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>();
        Assert.That(attributes.SelectMany(a => a.HttpMethods), Is.EquivalentTo(new[] { "POST" }));
    }

    [Test]
    public async Task ExternalLoginCallback_UsesCookiesByDefault()
    {
        _signInManager.GetExternalLoginInfoAsync().Returns(Info());
        ExternalResult(IdentitySignInResult.Success);

        await _controller.ExternalLoginCallback();

        Assert.That(_signInManager.AuthenticationScheme, Is.EqualTo(IdentityConstants.ApplicationScheme));
    }

    // ---------- Me ----------

    [Test]
    public void Me_SignedIn_ReturnsMetaInfo()
    {
        _controller.WithUser("17", "Bob", "Commissioner");

        var me = _controller.Me();

        Assert.That(me, Is.EqualTo(new MetaInfoResponse
        {
            IsSysAdmin = false,
            IsCommissioner = true,
            IsPlayer = false,
            DisplayName = "Bob",
            UserID = 17,
        }));
    }

    [Test]
    public void Me_Visitor_ReturnsNull()
    {
        _controller.WithAnonymousUser();

        Assert.That(_controller.Me(), Is.Null);
    }

    [Test]
    public void Me_NoIdentity_ReturnsNull()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() } };

        Assert.That(_controller.Me(), Is.Null);
    }

    // ---------- Antiforgery ----------

    [Test]
    public void Antiforgery_ReturnsTheRequestToken()
    {
        _controller.WithAnonymousUser();
        var antiforgery = Substitute.For<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
        antiforgery.GetAndStoreTokens(_controller.HttpContext)
            .Returns(new Microsoft.AspNetCore.Antiforgery.AntiforgeryTokenSet("request-token", "cookie-token", "form", "X-XSRF-TOKEN"));

        var response = _controller.Antiforgery(antiforgery);

        Assert.That(response.Token, Is.EqualTo("request-token"));
    }

    // ---------- Logout ----------

    [Test]
    public async Task Logout_SignsOutAndReturnsNoContent()
    {
        var result = await _controller.Logout();

        Assert.That(result, Is.TypeOf<NoContentResult>());
        await _signInManager.Received(1).SignOutAsync();
        await _userManager.DidNotReceiveWithAnyArgs().UpdateSecurityStampAsync(default!);
    }

    [Test]
    public async Task Logout_ChangesTheSignedInUsersSecurityStamp()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "5")], "Identity.Application"));
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
        var user = new User { Id = 5, UserName = "grif" };
        _userManager.GetUserAsync(principal).Returns(user);

        var result = await _controller.Logout();

        Assert.That(result, Is.TypeOf<NoContentResult>());
        await _userManager.Received(1).UpdateSecurityStampAsync(user);
        await _signInManager.Received(1).SignOutAsync();
    }

    // ---------- Authorization attributes ----------

    [Test]
    public void Authorization_OnlyMetaInfoRequiresAuthentication()
    {
        var t = typeof(IdentityController);
        Assert.Multiple(() =>
        {
            Assert.That(ControllerTestHelpers.ClassAuthorize(t), Is.Null);
            var meta = ControllerTestHelpers.ActionAuthorize(t, nameof(IdentityController.MetaInfo));
            Assert.That(meta, Is.Not.Null);
            Assert.That(meta!.Roles, Is.Null);
            foreach (var action in new[] { "Login", "Register", "ExternalLogin", "ExternalLoginCallback", "Refresh", "ResetPassword", "Me", "Antiforgery", "Logout" })
                Assert.That(ControllerTestHelpers.ActionAuthorize(t, action), Is.Null, action);
        });
    }
}
