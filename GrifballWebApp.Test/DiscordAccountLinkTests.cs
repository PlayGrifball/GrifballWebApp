using System.Security.Claims;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Identity;
using GrifballWebApp.Server.UserManagement;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace GrifballWebApp.Test;

/// <summary>The Discord link service against the test database.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordAccountLinkServiceTests
{
    private GrifballContext _context;
    private DiscordAccountLinkService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new DiscordAccountLinkService(_context, Substitute.For<ILogger<DiscordAccountLinkService>>());
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<User> NewUser(string name, long? discordUserId = null)
    {
        if (discordUserId is long id && !await _context.DiscordUsers.AnyAsync(d => d.DiscordUserID == id))
            _context.DiscordUsers.Add(new DiscordUser { DiscordUserID = id, DiscordUsername = "bot-" + name });
        var user = new User { UserName = name, DiscordUserID = discordUserId };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private Task<DiscordUser?> Row(long id) => _context.DiscordUsers.AsNoTracking().FirstOrDefaultAsync(d => d.DiscordUserID == id);
    private Task<long?> LinkOf(int userId) => _context.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DiscordUserID).SingleAsync();

    [Test]
    public async Task FindBotLinkedUser_FindsTheUserWithThatDiscordId()
    {
        var bob = await NewUser("bob", 111);
        await NewUser("alice");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await _service.FindBotLinkedUserAsync("Discord", "111"))?.Id, Is.EqualTo(bob.Id));
            Assert.That(await _service.FindBotLinkedUserAsync("Discord", "222"), Is.Null);
            Assert.That(await _service.FindBotLinkedUserAsync("Discord", "not-a-number"), Is.Null);
            Assert.That(await _service.FindBotLinkedUserAsync("Google", "111"), Is.Null);
        });
    }

    [Test]
    public async Task Link_CreatesTheDiscordRow_WhenTheBotHasNotSeenTheId()
    {
        var alice = await NewUser("alice");

        var result = await _service.LinkAsync(alice, "Discord", "111", "alice_discord");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result, Is.EqualTo(DiscordLinkResult.Linked));
            Assert.That(await LinkOf(alice.Id), Is.EqualTo(111));
            Assert.That((await Row(111))?.DiscordUsername, Is.EqualTo("alice_discord"));
        });
    }

    [Test]
    public async Task Link_ReusesTheBotsRow_AndRefreshesItsUsername()
    {
        _context.DiscordUsers.Add(new DiscordUser { DiscordUserID = 111, DiscordUsername = "old_name" });
        await _context.SaveChangesAsync();
        var alice = await NewUser("alice");

        var result = await _service.LinkAsync(alice, "Discord", "111", "new_name");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result, Is.EqualTo(DiscordLinkResult.Linked));
            Assert.That(await LinkOf(alice.Id), Is.EqualTo(111));
            Assert.That(await _context.DiscordUsers.CountAsync(), Is.EqualTo(1));
            Assert.That((await Row(111))?.DiscordUsername, Is.EqualTo("new_name"));
        });
    }

    [Test]
    public async Task Link_WithoutAName_KeepsTheRowsNameOrUsesTheId()
    {
        _context.DiscordUsers.Add(new DiscordUser { DiscordUserID = 111, DiscordUsername = "kept" });
        await _context.SaveChangesAsync();
        var alice = await NewUser("alice");
        var bob = await NewUser("bob");

        await _service.LinkAsync(alice, "Discord", "111", null);
        await _service.LinkAsync(bob, "Discord", "222", "");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await Row(111))?.DiscordUsername, Is.EqualTo("kept"));
            Assert.That((await Row(222))?.DiscordUsername, Is.EqualTo("222"));
        });
    }

    [Test]
    public async Task Link_AlreadyLinked_RefreshesTheUsername()
    {
        var bob = await NewUser("bob", 111);

        var result = await _service.LinkAsync(bob, "Discord", "111", "bob_renamed");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result, Is.EqualTo(DiscordLinkResult.AlreadyLinked));
            Assert.That((await Row(111))?.DiscordUsername, Is.EqualTo("bob_renamed"));
        });
    }

    [Test]
    public async Task Link_NeverOverwritesADifferentLink()
    {
        var bob = await NewUser("bob", 111);

        var result = await _service.LinkAsync(bob, "Discord", "222", "someone");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result, Is.EqualTo(DiscordLinkResult.UserLinkedToOtherDiscord));
            Assert.That(await LinkOf(bob.Id), Is.EqualTo(111));
            Assert.That(await Row(222), Is.Null);
        });
    }

    [Test]
    public async Task Link_LeavesAnIdThatAnotherUserHolds()
    {
        await NewUser("bob", 111);
        var alice = await NewUser("alice");

        var result = await _service.LinkAsync(alice, "Discord", "111", "alice_discord");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result, Is.EqualTo(DiscordLinkResult.DiscordLinkedToAnotherUser));
            Assert.That(await LinkOf(alice.Id), Is.Null);
            Assert.That((await Row(111))?.DiscordUsername, Is.EqualTo("bot-bob"));
        });
    }

    [TestCase("Google", "111")]
    [TestCase("Discord", "not-a-number")]
    public async Task Link_IgnoresOtherLogins(string provider, string key)
    {
        var alice = await NewUser("alice");

        Assert.That(await _service.LinkAsync(alice, provider, key, "x"), Is.EqualTo(DiscordLinkResult.NotDiscord));
        Assert.That(await _context.DiscordUsers.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task TryLink_FailedSave_IsUndoneAndReported()
    {
        var alice = await NewUser("alice");
        // Gone from the database behind the context's back: saving her link updates no row
        await _context.Users.Where(u => u.Id == alice.Id).ExecuteDeleteAsync();

        var result = await _service.TryLinkAsync(alice, "Discord", "111", "alice_discord");

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result, Is.EqualTo(DiscordLinkResult.Failed));
            Assert.That(_context.ChangeTracker.Entries().Any(e => e.State is EntityState.Added or EntityState.Modified), Is.False);
            Assert.That(await Row(111), Is.Null);
        });
        // The context still saves afterwards
        _context.DiscordUsers.Add(new DiscordUser { DiscordUserID = 5, DiscordUsername = "five" });
        await Assert.DoesNotThrowAsync(() => _context.SaveChangesAsync());
    }

    [Test]
    public async Task InTransaction_CommitsOnTrue()
    {
        Assert.That(await _service.InTransactionAsync(async () =>
        {
            _context.DiscordUsers.Add(new DiscordUser { DiscordUserID = 1, DiscordUsername = "kept" });
            await _context.SaveChangesAsync();
            return true;
        }), Is.True);

        Assert.That(await _context.DiscordUsers.AsNoTracking().AnyAsync(d => d.DiscordUserID == 1), Is.True);
    }

    [Test]
    public async Task InTransaction_RollsBackOnFalse()
    {
        Assert.That(await _service.InTransactionAsync(async () =>
        {
            _context.DiscordUsers.Add(new DiscordUser { DiscordUserID = 2, DiscordUsername = "dropped" });
            await _context.SaveChangesAsync();
            return false;
        }), Is.False);

        Assert.That(await _context.DiscordUsers.AsNoTracking().AnyAsync(d => d.DiscordUserID == 2), Is.False);
    }

    [Test]
    public async Task InTransaction_RollsBackAndRethrowsOnException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.InTransactionAsync(async () =>
        {
            _context.DiscordUsers.Add(new DiscordUser { DiscordUserID = 3, DiscordUsername = "dropped" });
            await _context.SaveChangesAsync();
            throw new InvalidOperationException("boom");
        }));

        Assert.That(await _context.DiscordUsers.AsNoTracking().AnyAsync(d => d.DiscordUserID == 3), Is.False);
    }
}

/// <summary>Discord sign-in (ExternalLoginCallback) against the real stores and database, with the sign-in itself faked.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class IdentityControllerDiscordLinkTests
{
    private GrifballContext _context;
    private UserManager<User> _userManager;
    private SignInManager<User> _signInManager;
    private IdentityController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _userManager = IdentityTestHelpers.NewUserManager(_context);
        _signInManager = Substitute.For<SignInManager<User>>(
            _userManager, Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(), null, null, null, null);
        // Like the real one: succeeds when a user has this login
        _signInManager.ExternalLoginSignInAsync(Arg.Any<string>(), Arg.Any<string>(), true, true)
            .Returns(async ci => await _userManager.FindByLoginAsync(ci.ArgAt<string>(0), ci.ArgAt<string>(1)) is null
                ? IdentitySignInResult.Failed : IdentitySignInResult.Success);
        _controller = new IdentityController(Substitute.For<ILogger<IdentityController>>(), _userManager, _signInManager,
            Substitute.For<IOptionsMonitor<BearerTokenOptions>>(), TimeProvider.System, Substitute.For<IUserManagementService>(),
            new DiscordAccountLinkService(_context, Substitute.For<ILogger<DiscordAccountLinkService>>()));
        _controller.WithAnonymousUser();
        _controller.HttpContext.RequestServices = new ServiceCollection().AddSingleton(Substitute.For<IAuthenticationService>()).BuildServiceProvider();
    }

    [TearDown]
    public async Task TearDown()
    {
        _userManager.Dispose();
        await _context.DropDatabaseAndDispose();
    }

    private void SignInFromDiscord(string id, string name)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Name, name)], "Discord"));
        _signInManager.GetExternalLoginInfoAsync().Returns(new ExternalLoginInfo(principal, "Discord", id, "Discord"));
    }

    private async Task<User> NewUser(string name, long? discordUserId = null, string? loginKey = null)
    {
        if (discordUserId is long id)
            _context.DiscordUsers.Add(new DiscordUser { DiscordUserID = id, DiscordUsername = "bot-" + name });
        var user = new User { UserName = name, DiscordUserID = discordUserId };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        if (loginKey is not null)
            await _userManager.AddLoginAsync(user, new UserLoginInfo("Discord", loginKey, "Discord"));
        return user;
    }

    private Task<User> Reload(int id) => _context.Users.AsNoTracking().SingleAsync(u => u.Id == id);

    [TestCase(true)]
    [TestCase(false)]
    public async Task FirstSignIn_OfABotLinkedPlayer_JoinsTheirAccount(bool useCookies)
    {
        var bob = await NewUser("bob", 111);
        SignInFromDiscord("111", "bob_discord");

        var result = await _controller.ExternalLoginCallback(useCookies, CancellationToken.None);

        Assert.That(result, Is.TypeOf<EmptyResult>());
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await _context.Users.CountAsync(), Is.EqualTo(1));
            Assert.That((await _userManager.FindByLoginAsync("Discord", "111"))?.Id, Is.EqualTo(bob.Id));
            Assert.That(await _context.UserRoles.CountAsync(r => r.UserId == bob.Id), Is.Zero, "roles unchanged");
            Assert.That((await _context.DiscordUsers.AsNoTracking().SingleAsync()).DiscordUsername, Is.EqualTo("bob_discord"));
            Assert.That(_signInManager.AuthenticationScheme, Is.EqualTo(useCookies ? IdentityConstants.ApplicationScheme : IdentityConstants.BearerScheme));
        });
        await _signInManager.Received(1).ExternalLoginSignInAsync("Discord", "111", true, true);
        await _signInManager.DidNotReceiveWithAnyArgs().SignInAsync(default!, default(bool), default);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task NewAccount_GetsTheLink_AndANewDiscordRow(bool useCookies)
    {
        SignInFromDiscord("222", "carol");

        var result = await _controller.ExternalLoginCallback(useCookies, CancellationToken.None);

        Assert.That(result, Is.TypeOf<EmptyResult>());
        var carol = await _context.Users.AsNoTracking().SingleAsync();
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(carol.UserName, Is.EqualTo("carol"));
            Assert.That(carol.DiscordUserID, Is.EqualTo(222));
            Assert.That((await _context.DiscordUsers.AsNoTracking().SingleAsync()).DiscordUsername, Is.EqualTo("carol"));
            Assert.That((await _userManager.FindByLoginAsync("Discord", "222"))?.Id, Is.EqualTo(carol.Id));
        });
        await _signInManager.Received(1).SignInAsync(Arg.Is<User>(u => u.Id == carol.Id), true, null);
    }

    [Test]
    public async Task NewAccount_ReusesTheRowTheBotMade()
    {
        // The bot saw a DM from this id but never made an account for it
        _context.DiscordUsers.Add(new DiscordUser { DiscordUserID = 333, DiscordUsername = "dave_old" });
        await _context.SaveChangesAsync();
        SignInFromDiscord("333", "dave");

        await _controller.ExternalLoginCallback(true, CancellationToken.None);

        var dave = await _context.Users.AsNoTracking().SingleAsync();
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(dave.DiscordUserID, Is.EqualTo(333));
            Assert.That((await _context.DiscordUsers.AsNoTracking().SingleAsync()).DiscordUsername, Is.EqualTo("dave"));
        });
    }

    [Test]
    public async Task NewAccount_FailedStep_LeavesNoAccount()
    {
        // The Player role is gone, so adding it throws after the user was created
        await _context.Roles.Where(r => r.Name == "Player").ExecuteDeleteAsync();
        SignInFromDiscord("444", "erin");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.ExternalLoginCallback(true, CancellationToken.None));
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await _context.Users.AsNoTracking().CountAsync(), Is.Zero);
            Assert.That(await _context.DiscordUsers.AsNoTracking().CountAsync(), Is.Zero);
        });
    }

    [Test]
    public async Task LaterSignIn_BackfillsTheLink()
    {
        var frank = await NewUser("frank", loginKey: "555");
        SignInFromDiscord("555", "frank_discord");

        await _controller.ExternalLoginCallback(true, CancellationToken.None);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await Reload(frank.Id)).DiscordUserID, Is.EqualTo(555));
            Assert.That((await _context.DiscordUsers.AsNoTracking().SingleAsync()).DiscordUsername, Is.EqualTo("frank_discord"));
            Assert.That(await _context.Users.CountAsync(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task LaterSignIn_KeepsADifferentExistingLink()
    {
        var gina = await NewUser("gina", discordUserId: 777, loginKey: "666");
        SignInFromDiscord("666", "gina_discord");

        await _controller.ExternalLoginCallback(true, CancellationToken.None);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await Reload(gina.Id)).DiscordUserID, Is.EqualTo(777));
            Assert.That(await _context.DiscordUsers.AnyAsync(d => d.DiscordUserID == 666), Is.False);
        });
    }

    [Test]
    public async Task LaterSignIn_DoesNotTakeAnIdAnotherUserHolds()
    {
        var holder = await NewUser("holder", discordUserId: 888);
        var hank = await NewUser("hank", loginKey: "888");
        SignInFromDiscord("888", "hank_discord");

        await _controller.ExternalLoginCallback(true, CancellationToken.None);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await Reload(hank.Id)).DiscordUserID, Is.Null);
            Assert.That((await Reload(holder.Id)).DiscordUserID, Is.EqualTo(888));
        });
        await _signInManager.Received(1).ExternalLoginSignInAsync("Discord", "888", true, true);
    }
}
