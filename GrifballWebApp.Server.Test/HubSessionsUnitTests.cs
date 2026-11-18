using System.Security.Claims;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Identity;
using GrifballWebApp.Server.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// Every hub's calls checked against the database's user (<see cref="HubSessionFilter"/>), their connections kept and ended
/// (<see cref="HubConnections"/>, <see cref="HubUserSessions"/>) and swept (<see cref="HubSessionSweep"/>), with the database's
/// answer given (<see cref="FakeValidator"/>). Real connections: GrifballWebApp.Integration.Test's HubSessionTests.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class HubSessionsUnitTests
{
    private static readonly HubUserState Commissioner = new(7, ["Player", "Commissioner"]);
    private static readonly HubUserState Player = new(7, ["Player"]);
    private static readonly int[] Seven = [7];
    private static readonly int[] SevenAndTwelve = [7, 12];

    private readonly HubConnections _connections = new();
    private readonly FakeValidator _validator = new();

    /// <summary>The database's answer for every principal: <see cref="Now"/>.</summary>
    private sealed class FakeValidator() : HubSessionValidator(null!, null!, null!, null!)
    {
        public HubUserState? Now { get; set; } = Commissioner;

        public bool Fails { get; set; }

        public override Task<HubUserState?> CurrentAsync(ClaimsPrincipal? principal)
            => Fails ? throw new InvalidOperationException("The database is down") : Task.FromResult(Now);

        public override Task<IReadOnlyList<HubUserState?>> CurrentAllAsync(IReadOnlyList<ClaimsPrincipal> principals)
            => Fails ? throw new InvalidOperationException("The database is down") : Task.FromResult<IReadOnlyList<HubUserState?>>([.. principals.Select(_ => Now)]);
    }

    private sealed class TestHub : Microsoft.AspNetCore.SignalR.Hub
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "A hub method is an instance method")]
        public void Anyone() { }

        [Authorize(Roles = "Commissioner, Sysadmin")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "A hub method is an instance method")]
        public void Staff() { }

        [Authorize]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "A hub method is an instance method")]
        public void SignedIn() { }

        [Authorize(Policy = StaffPolicy)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "A hub method is an instance method")]
        public void ByPolicy() { }
    }

    /// <summary>A hub only signed-in users may use, but for one method anyone may.</summary>
    [Authorize]
    private sealed class SignedInHub : Microsoft.AspNetCore.SignalR.Hub
    {
        [AllowAnonymous]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "A hub method is an instance method")]
        public void Open() { }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "A hub method is an instance method")]
        public void Closed() { }
    }

    private const string StaffPolicy = "Staff";

    /// <summary>A call's services: the validator, the call's user, and authorization as the site has it (a policy of staff's too).</summary>
    private IServiceProvider Services() => new ServiceCollection()
        .AddSingleton<HubSessionValidator>(_validator)
        .AddScoped<HubUser>()
        .AddLogging()
        .AddAuthorization(options => options.AddPolicy(StaffPolicy, policy => policy.RequireRole("Commissioner", "Sysadmin")))
        .BuildServiceProvider().CreateScope().ServiceProvider;

    private static HubCallerContext Connection(string id = "connection-1")
    {
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns(id);
        context.User.Returns(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "7")], "Cookies")));
        return context;
    }

    private static HubInvocationContext Call(HubCallerContext connection, IServiceProvider services, string method)
        => new(connection, services, new TestHub(), typeof(TestHub).GetMethod(method)!, []);

    private async Task<(bool Ran, string? Refusal)> Invoke(HubCallerContext connection, string method, IServiceProvider? services = null)
    {
        var ran = false;
        try
        {
            await new HubSessionFilter(_connections).InvokeMethodAsync(Call(connection, services ?? Services(), method), _ => { ran = true; return ValueTask.FromResult<object?>(null); });
            return (ran, null);
        }
        catch (HubException e)
        {
            return (ran, e.Message);
        }
    }

    private async Task Connect(HubCallerContext connection, IServiceProvider? services = null)
        => await new HubSessionFilter(_connections).OnConnectedAsync(new HubLifetimeContext(connection, services ?? Services(), new TestHub()), _ => Task.CompletedTask);

    [Test]
    public async Task OnConnected_KeepsTheConnection_AsTheDatabasesUser_ForTheHub()
    {
        var connection = Connection();
        var services = Services();

        await Connect(connection, services);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_connections.Find("connection-1")?.User, Is.EqualTo(Commissioner));
            Assert.That(services.GetRequiredService<HubUser>().State, Is.EqualTo(Commissioner));
        }
    }

    [Test]
    public void OnConnected_ASessionThatHasEnded_IsRefused()
    {
        _validator.Now = null;

        Assert.That(() => Connect(Connection()), Throws.TypeOf<HubException>().With.Message.EqualTo(HubSessionFilter.SessionEnded));
        Assert.That(_connections.SignedIn(), Is.Empty);
    }

    [Test]
    public async Task OnDisconnected_ForgetsTheConnection()
    {
        var connection = Connection();
        await Connect(connection);

        await new HubSessionFilter(_connections).OnDisconnectedAsync(new HubLifetimeContext(connection, Services(), new TestHub()), null, (_, _) => Task.CompletedTask);

        Assert.That(_connections.Find("connection-1"), Is.Null);
    }

    [Test]
    public async Task Invoke_AsTheUserStill_RunsTheMethod_AsTheDatabasesUser()
    {
        var connection = Connection();
        await Connect(connection);
        var services = Services();

        var (ran, refusal) = await Invoke(connection, nameof(TestHub.Staff), services);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((ran, refusal), Is.EqualTo((true, (string?)null)));
            Assert.That(services.GetRequiredService<HubUser>().AsToolCaller().Roles, Is.EquivalentTo(Commissioner.Roles));
        }
        connection.DidNotReceive().Abort();
    }

    [Test]
    public async Task Invoke_SignedOutSinceItConnected_IsRefused_AndTheConnectionClosed()
    {
        var connection = Connection();
        await Connect(connection);
        _validator.Now = null;

        var (ran, refusal) = await Invoke(connection, nameof(TestHub.Anyone));

        Assert.That((ran, refusal), Is.EqualTo((false, HubSessionFilter.SessionEnded)));
        connection.Received(1).Abort();
    }

    [Test]
    public async Task Invoke_RolesChangedSinceItConnected_IsRefused_AndTheConnectionClosed_EvenForAMethodAnyoneMayCall()
    {
        // Its groups were the old roles' (LiveHub's watched topics): it connects again as the user is now
        var connection = Connection();
        await Connect(connection);
        _validator.Now = Player;

        var (ran, refusal) = await Invoke(connection, nameof(TestHub.Anyone));

        Assert.That((ran, refusal), Is.EqualTo((false, HubSessionFilter.RolesChanged)));
        connection.Received(1).Abort();
    }

    [Test]
    public async Task Invoke_AMethodTheUsersRolesDontAllowNow_IsRefused_WhateverTheClaims()
    {
        _validator.Now = Player;
        var connection = Connection();
        await Connect(connection);

        var (ran, refusal) = await Invoke(connection, nameof(TestHub.Staff));

        Assert.That((ran, refusal), Is.EqualTo((false, HubSessionFilter.NotAllowed)));
        connection.Received(1).Abort();
    }

    /// <summary>Whether <paramref name="user"/> may call <paramref name="hub"/>'s <paramref name="method"/> as the site authorizes it.</summary>
    private Task<bool> Allows(Type hub, string method, HubUserState user)
        => HubSessionFilter.AllowsAsync(Services(), hub.GetMethod(method)!, hub, Connection().User, user);

    [Test]
    public async Task AllowsAsync_TheMethodsAuthorize_WithTheRolesNow_ThroughTheSitesAuthorization()
    {
        var hub = typeof(TestHub);
        var sysadmin = new HubUserState(7, ["Sysadmin"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Allows(hub, nameof(TestHub.Anyone), HubUserState.Anonymous), Is.True);
            Assert.That(await Allows(hub, nameof(TestHub.SignedIn), HubUserState.Anonymous), Is.False);
            Assert.That(await Allows(hub, nameof(TestHub.SignedIn), Player), Is.True);
            Assert.That(await Allows(hub, nameof(TestHub.Staff), Player), Is.False);
            Assert.That(await Allows(hub, nameof(TestHub.Staff), sysadmin), Is.True);
            Assert.That(await Allows(hub, nameof(TestHub.ByPolicy), Player), Is.False, "a policy, with the roles now");
            Assert.That(await Allows(hub, nameof(TestHub.ByPolicy), sysadmin), Is.True);
        }
    }

    [Test]
    public async Task AllowsAsync_AllowAnonymous_UnderAHubsAuthorize_LetsAnyoneCallIt_AndOnlyIt()
    {
        var hub = typeof(SignedInHub);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Allows(hub, nameof(SignedInHub.Open), HubUserState.Anonymous), Is.True);
            Assert.That(await Allows(hub, nameof(SignedInHub.Closed), HubUserState.Anonymous), Is.False);
            Assert.That(await Allows(hub, nameof(SignedInHub.Closed), Player), Is.True);
        }
    }

    [Test]
    public void AsNow_KeepsTheClaims_ButTheRolesAreTheDatabases()
    {
        var claims = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "7"), new Claim(ClaimTypes.Role, "Sysadmin")], "Cookies"));

        var now = HubSessionFilter.AsNow(claims, Player);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(now.FindFirstValue(ClaimTypes.NameIdentifier), Is.EqualTo("7"));
            Assert.That(now.IsInRole("Sysadmin"), Is.False);
            Assert.That(now.IsInRole("Player"), Is.True);
            Assert.That(now.Identity?.IsAuthenticated, Is.True);
            Assert.That(HubSessionFilter.AsNow(claims, HubUserState.Anonymous).Identity?.IsAuthenticated, Is.False);
            Assert.That(HubSessionFilter.AsNow(null, Player).Identity?.IsAuthenticated, Is.False);
        }
    }

    [Test]
    public void OnConnected_TheHubRefusing_LeavesNoConnectionKept()
    {
        var connection = Connection();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => new HubSessionFilter(_connections).OnConnectedAsync(new HubLifetimeContext(connection, Services(), new TestHub()),
                _ => throw new HubException("No")), Throws.TypeOf<HubException>());
            Assert.That(_connections.Find("connection-1"), Is.Null);
        }
    }

    [Test]
    public void HubUser_IsTheCallersToolCaller_SignedOutNone()
    {
        var user = new HubUser();
        var signedOut = user.AsToolCaller();
        user.Set(Commissioner);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(signedOut, Is.SameAs(ToolCaller.Anonymous));
            Assert.That(user.AsToolCaller().UserID, Is.EqualTo(7));
            Assert.That(user.Roles, Is.EquivalentTo(Commissioner.Roles));
            Assert.That(new HubUserState(7, ["Commissioner", "Player"]).SameAs(Commissioner), Is.True, "in any order");
            Assert.That(Player.SameAs(Commissioner), Is.False);
            Assert.That(new HubUserState(8, Player.Roles).SameAs(Player), Is.False, "another user");
        }
    }

    [Test]
    public async Task End_ClosesTheUsersConnectionsHere_AndNoOneElses()
    {
        var mine = Connection("mine");
        var theirs = Connection("theirs");
        var signedOut = Connection("signed-out");
        await Connect(mine);
        _validator.Now = new HubUserState(8, []);
        await Connect(theirs);
        _validator.Now = HubUserState.Anonymous;
        await Connect(signedOut);

        new HubUserSessions(_connections, NullLogger<HubUserSessions>.Instance).End([7]);

        mine.Received(1).Abort();
        theirs.DidNotReceive().Abort();
        signedOut.DidNotReceive().Abort();
    }

    [Test]
    public async Task Invoke_AConnectionThisProcessDoesntKnow_IsCheckedAgainstTheDatabaseAlone()
    {
        var (ran, refusal) = await Invoke(Connection("unknown"), nameof(TestHub.Staff));

        Assert.That((ran, refusal), Is.EqualTo((true, (string?)null)));
    }

    [Test]
    public async Task Sweep_RunsEveryInterval_AndLogsAFailedCheck_WithoutStopping()
    {
        var connection = Connection();
        await Connect(connection);
        var aborted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.When(c => c.Abort()).Do(_ => aborted.TrySetResult());
        var scopes = new ServiceCollection().AddSingleton<HubSessionValidator>(_validator).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var time = new FakeTimeProvider();
        var logger = new RecordingLogger<HubSessionSweep>();
        using var sweep = new HubSessionSweep(_connections, scopes, time, logger);
        await sweep.StartAsync(CancellationToken.None);

        // The service starts its timer on its own thread: tick until it has run, whenever that is
        async Task<bool> TickUntil(Func<Task<bool>> done)
        {
            for (var i = 0; i < 100; i++)
            {
                time.Advance(HubSessionSweep.Interval);
                if (await done())
                    return true;
            }
            return false;
        }

        _validator.Fails = true;
        var logged = await TickUntil(() => logger.WaitForEntries(1, TimeSpan.FromMilliseconds(100)));
        _validator.Fails = false;
        _validator.Now = null;
        var closed = await TickUntil(async () => await Task.WhenAny(aborted.Task, Task.Delay(100)) == aborted.Task);
        await sweep.StopAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(logged, Is.True);
            Assert.That(logger.Entries[0].Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(closed, Is.True, "still sweeping after the failure");
        }
    }

    [Test]
    public async Task Validator_AsksIdentity_TheStampTheLockoutAndTheRolesNow()
    {
        var users = Substitute.For<UserManager<User>>(Substitute.For<IUserStore<User>>(), null!, null!, null!, null!, null!, new IdentityErrorDescriber(), null!, null!);
        var signIn = Substitute.For<SignInManager<User>>(users, Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), Substitute.For<IUserClaimsPrincipalFactory<User>>(),
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), NullLogger<SignInManager<User>>.Instance,
            Substitute.For<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>(), Substitute.For<IUserConfirmation<User>>());
        var validator = new HubSessionValidator(signIn, null!, Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), TimeProvider.System);
        var principal = Connection().User;
        var user = User.Register("staffer");
        user.Id = 7;

        signIn.ValidateSecurityStampAsync(principal).Returns(user);
        users.GetRolesAsync(user).Returns(new List<string> { "Commissioner" });
        var current = await validator.CurrentAsync(principal);
        users.IsLockedOutAsync(user).Returns(true);
        var locked = await validator.CurrentAsync(principal);
        signIn.ValidateSecurityStampAsync(principal).Returns((User?)null);
        var signedOut = await validator.CurrentAsync(principal);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await validator.CurrentAsync(new ClaimsPrincipal(new ClaimsIdentity())), Is.SameAs(HubUserState.Anonymous));
            Assert.That(await validator.CurrentAsync(null), Is.SameAs(HubUserState.Anonymous));
            Assert.That(current?.SameAs(new HubUserState(7, ["Commissioner"])), Is.True);
            Assert.That(locked, Is.Null, "locked out");
            Assert.That(signedOut, Is.Null, "a stamp that is no longer theirs");
        }
    }

    [Test]
    public void FormatAndParse_TheUsersOnTheChannel_IgnoringWhatIsNoID()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(HubUserSessions.Format([7, 12]), Is.EqualTo("7,12"));
            Assert.That(HubUserSessions.Parse("7,12"), Is.EqualTo(SevenAndTwelve));
            Assert.That(HubUserSessions.Parse("7,x,"), Is.EqualTo(Seven));
            Assert.That(HubUserSessions.Parse(null), Is.Empty);
        }
    }

    [Test]
    public async Task Sweep_ClosesTheConnectionsWhoseUserChangedWithoutAWord_AndKeepsTheRest()
    {
        var changed = Connection("changed");
        var unchanged = Connection("unchanged");
        await Connect(changed);
        _validator.Now = Player;
        await Connect(unchanged);
        var scopes = new ServiceCollection().AddSingleton<HubSessionValidator>(_validator).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var ended = await new HubSessionSweep(_connections, scopes, new FakeTimeProvider(), NullLogger<HubSessionSweep>.Instance).SweepAsync();

        Assert.That(ended, Is.EqualTo(1));
        changed.Received(1).Abort();
        unchanged.DidNotReceive().Abort();
    }

    [Test]
    public async Task Sweep_ASessionThatEnded_IsClosed()
    {
        var connection = Connection();
        await Connect(connection);
        _validator.Now = null;
        var scopes = new ServiceCollection().AddSingleton<HubSessionValidator>(_validator).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        await new HubSessionSweep(_connections, scopes, new FakeTimeProvider(), NullLogger<HubSessionSweep>.Instance).SweepAsync();

        connection.Received(1).Abort();
    }

    [Test]
    public async Task Sweep_NoConnections_ChecksNothing()
    {
        _validator.Fails = true;
        var scopes = new ServiceCollection().AddSingleton<HubSessionValidator>(_validator).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var ended = await new HubSessionSweep(_connections, scopes, new FakeTimeProvider(), NullLogger<HubSessionSweep>.Instance).SweepAsync();

        Assert.That(ended, Is.Zero);
    }

    [Test]
    public async Task End_NoUsers_PublishesNothing()
    {
        // Never connected: Redis is only reached by a publication
        await using var redis = new GrifballWebApp.Infrastructure.Redis.AppRedis("127.0.0.1:9", "grif-test");
        var sessions = new HubUserSessions(_connections, NullLogger<HubUserSessions>.Instance, redis);

        sessions.End([]);

        Assert.That(sessions.Published, Is.SameAs(Task.CompletedTask));
    }
}
