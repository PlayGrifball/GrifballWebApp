using System.Security.Claims;
using GrifballWebApp.Application.Tools;
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

    private readonly HubConnections _connections = new();
    private readonly FakeValidator _validator = new();

    /// <summary>The database's answer for every principal: <see cref="Now"/>.</summary>
    private sealed class FakeValidator() : HubSessionValidator(null!)
    {
        public HubUserState? Now { get; set; } = Commissioner;

        public override Task<HubUserState?> CurrentAsync(ClaimsPrincipal? principal) => Task.FromResult(Now);
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
    }

    private IServiceProvider Services() => new ServiceCollection().AddSingleton<HubSessionValidator>(_validator).AddScoped<HubUser>().BuildServiceProvider().CreateScope().ServiceProvider;

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
        // Its groups were the old roles' (TeamsHub's staff group): it connects again as the user is now
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

    [Test]
    public void Allows_TheMethodsAuthorize_WithTheRolesNow()
    {
        var hub = typeof(TestHub);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(HubSessionFilter.Allows(hub.GetMethod(nameof(TestHub.Anyone))!, hub, HubUserState.Anonymous), Is.True);
            Assert.That(HubSessionFilter.Allows(hub.GetMethod(nameof(TestHub.SignedIn))!, hub, HubUserState.Anonymous), Is.False);
            Assert.That(HubSessionFilter.Allows(hub.GetMethod(nameof(TestHub.SignedIn))!, hub, Player), Is.True);
            Assert.That(HubSessionFilter.Allows(hub.GetMethod(nameof(TestHub.Staff))!, hub, Player), Is.False);
            Assert.That(HubSessionFilter.Allows(hub.GetMethod(nameof(TestHub.Staff))!, hub, new HubUserState(7, ["Sysadmin"])), Is.True);
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
        }
    }

    [Test]
    public async Task EndAsync_ClosesTheUsersConnectionsHere_AndNoOneElses()
    {
        var mine = Connection("mine");
        var theirs = Connection("theirs");
        await Connect(mine);
        _validator.Now = new HubUserState(8, []);
        await Connect(theirs);

        await new HubUserSessions(_connections, NullLogger<HubUserSessions>.Instance).EndAsync([7]);

        mine.Received(1).Abort();
        theirs.DidNotReceive().Abort();
    }

    [Test]
    public void FormatAndParse_TheUsersOnTheChannel_IgnoringWhatIsNoID()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(HubUserSessions.Format([7, 12]), Is.EqualTo("7,12"));
            Assert.That(HubUserSessions.Parse("7,12"), Is.EqualTo(new[] { 7, 12 }));
            Assert.That(HubUserSessions.Parse("7,x,"), Is.EqualTo(new[] { 7 }));
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
}
