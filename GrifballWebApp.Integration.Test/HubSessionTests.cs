using System.Net;
using System.Net.Http.Json;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Preview;
using GrifballWebApp.Server.Identity;
using GrifballWebApp.Server.Preview;
using GrifballWebApp.Server.Live;
using GrifballWebApp.Server.SignalR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServerProgram = GrifballWebApp.Server.Program;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// Hub connections stay only as long as their user may have them: real SignalR connections (WebSockets, signed in by the browser's
/// cookie) to the real <see cref="ServerProgram"/> in preview mode, each test with users of its own. A sign-out, a role removed, an
/// expired cookie: the connection is closed, its next call refused. The other pods'
/// connections (through Redis): SignalRBackplaneTests. Non-parallelizable: Program.Main swaps the static Serilog logger.
/// </summary>
[TestFixture]
[NonParallelizable]
public class HubSessionTests
{
    private const string Password = "Hub-Session-Password-1";
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);
    private static readonly string[] CommissionerRole = ["Commissioner"];

    private string _connectionString = null!;
    private WebApplicationFactory<ServerProgram> _factory = null!;
    private readonly List<HubConnection> _connections = [];

    /// <summary>The site in preview mode on its own database; <paramref name="cookieLifetime"/> set, sessions that short.</summary>
    private sealed class PreviewFactory(Dictionary<string, string?> settings, TimeSpan? cookieLifetime = null) : WebApplicationFactory<ServerProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);
            if (cookieLifetime is { } lifetime)
                builder.ConfigureTestServices(services => services.ConfigureApplicationCookie(options =>
                {
                    options.ExpireTimeSpan = lifetime;
                    options.SlidingExpiration = false;
                }));
        }
    }

    private static PreviewFactory Site(string connectionString, TimeSpan? cookieLifetime = null) => new(new()
    {
        ["Database:Provider"] = TestDatabase.Provider.ToString(),
        ["ConnectionStrings:GrifballWebApp"] = connectionString,
        ["ApplyMigrations"] = "true",
        ["CreateDatabase"] = "true",
        [PreviewMode.ConfigurationKey] = "true",
        [PreviewSmokeUsers.PasswordKey] = Password,
    }, cookieLifetime);

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _connectionString = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, $"HubSessionDb_{Guid.NewGuid():N}");
        _factory = Site(_connectionString);
    }

    [TearDown]
    public async Task TearDown()
    {
        foreach (var connection in _connections)
            await connection.DisposeAsync();
        _connections.Clear();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _factory.DisposeAsync();
        await TestDatabase.DropDatabase(_connectionString, ifExists: true);
    }

    private HttpClient Browser(WebApplicationFactory<ServerProgram>? site = null) => (site ?? _factory).CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false,
        AllowAutoRedirect = false,
    });

    /// <summary>A new user with <paramref name="roles"/>, signed in: their ID and their session cookie.</summary>
    private async Task<(int Id, string Cookie)> SignedIn(string[] roles, WebApplicationFactory<ServerProgram>? site = null)
    {
        site ??= _factory;
        var name = $"hub{Guid.NewGuid():N}"[..20];
        int id;
        using (var scope = site.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = User.Register(name);
            Assert.That((await users.CreateAsync(user, Password)).Succeeded, Is.True);
            if (roles.Length > 0)
                Assert.That((await users.AddToRolesAsync(user, roles)).Succeeded, Is.True);
            id = user.Id;
        }
        using var browser = Browser(site);
        using var login = await browser.PostAsJsonAsync("/identity/login", new { Username = name, Password });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var cookie = login.Headers.GetValues("Set-Cookie").First(v => v.StartsWith(BrowserAuth.CookieName + "=", StringComparison.Ordinal)).Split(';')[0];
        return (id, cookie);
    }

    /// <summary>A connection to <paramref name="hub"/> over WebSockets with <paramref name="cookie"/>, as the site's pages make one; started.</summary>
    private async Task<(HubConnection Connection, Task<Exception?> Closed)> Connect(string hub, string cookie, WebApplicationFactory<ServerProgram>? site = null)
    {
        site ??= _factory;
        var connection = new HubConnectionBuilder()
            .WithUrl($"https://localhost/{hub}", options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = async (context, ct) =>
                {
                    var socket = site.Server.CreateWebSocketClient();
                    socket.ConfigureRequest = request =>
                    {
                        request.Headers.Cookie = cookie;
                        request.Headers.Origin = "https://localhost";
                    };
                    return await socket.ConnectAsync(context.Uri, ct);
                };
            })
            .Build();
        _connections.Add(connection);
        var closed = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += e =>
        {
            closed.TrySetResult(e);
            return Task.CompletedTask;
        };
        await connection.StartAsync();
        return (connection, closed.Task);
    }

    /// <summary>
    /// Once the hub has finished connecting the connection: StartAsync returns at the handshake, before
    /// OnConnectedAsync has run, and the hub takes a call only after it.
    /// </summary>
    private static async Task Joined(HubConnection connection)
    {
        await connection.InvokeAsync<List<string>>(nameof(ILiveHubServer.Watch), new List<string>());
    }

    /// <summary>Whether <paramref name="closed"/> happens within <see cref="Wait"/>.</summary>
    private static async Task<bool> ClosesSoon(Task closed) => await Task.WhenAny(closed, Task.Delay(Wait)) == closed;

    private async Task RemoveRoleBehindTheAppsBack(int userId, string role)
    {
        // Straight in the database, as SQL would: no save announces it, so only each call's own check sees it
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GrifballContext>();
        var roleId = await db.Roles.Where(r => r.Name == role).Select(r => r.Id).SingleAsync();
        Assert.That(await db.UserRoles.Where(r => r.UserId == userId && r.RoleId == roleId).ExecuteDeleteAsync(), Is.EqualTo(1), "the role was the user's, and is gone");
    }

    [Test]
    public async Task SigningOut_ClosesTheUsersConnection()
    {
        var (_, cookie) = await SignedIn([]);
        var (_, closed) = await Connect(LiveHub.Path, cookie);

        using var browser = Browser();
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/identity/logout");
        logout.Headers.Add("Cookie", cookie);
        logout.Headers.Add("Sec-Fetch-Site", "same-origin");
        using var response = await browser.SendAsync(logout);

        var closes = await ClosesSoon(closed);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(closes, Is.True, "the logout's new security stamp ends the session");
        }
    }

    [Test]
    public async Task ARoleRemovedBehindTheAppsBack_FailsTheNextCall_AndClosesTheConnection()
    {
        var (id, cookie) = await SignedIn(["Commissioner"]);
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            Assert.That(await users.GetRolesAsync((await users.FindByIdAsync(id.ToString(System.Globalization.CultureInfo.InvariantCulture)))!), Is.EqualTo(CommissionerRole), "a commissioner before connecting");
        }
        var (connection, closed) = await Connect(LiveHub.Path, cookie);
        Assert.That(connection.State, Is.EqualTo(HubConnectionState.Connected));
        // The server has the connection, with the user as they are: a call answered (a start returns before it does)
        await Joined(connection);

        await RemoveRoleBehindTheAppsBack(id, "Commissioner");

        // What the call's check compares: the roles the connection was made with, and the user's now
        var connected = _factory.Services.GetRequiredService<HubConnections>().SignedIn().Single(c => c.User.UserId == id).User;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var now = await users.GetRolesAsync((await users.FindByIdAsync(id.ToString(System.Globalization.CultureInfo.InvariantCulture)))!);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(connected.Roles, Is.EqualTo(CommissionerRole), "connected as a commissioner");
                Assert.That(now, Is.Empty, "the role is gone");
            }
        }
        Assert.That(async () => await connection.InvokeAsync<List<string>>(nameof(ILiveHubServer.Watch), new List<string>()), Throws.Exception);
        Assert.That(await ClosesSoon(closed), Is.True);
    }

    [Test]
    public async Task ARemovedStaffMember_TheirConnectionIsClosed()
    {
        var (id, cookie) = await SignedIn(["Commissioner"]);
        var (staff, closed) = await Connect(LiveHub.Path, cookie);
        await Joined(staff);

        // Through Identity, as the user management page does: the save ends their connections at once
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            Assert.That((await users.RemoveFromRoleAsync((await users.FindByIdAsync(id.ToString(System.Globalization.CultureInfo.InvariantCulture)))!, "Commissioner")).Succeeded, Is.True);
        }
        Assert.That(await ClosesSoon(closed), Is.True, "a connection that only listens is closed too");
    }

    [TestCase("LiveHub")]
    [TestCase("ChatHub")]
    public async Task AnExpiredCookie_ClosesTheConnection_OnEveryHub(string hub)
    {
        var connectionString = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, $"HubExpiryDb_{Guid.NewGuid():N}");
        var shortSessions = Site(connectionString, cookieLifetime: TimeSpan.FromSeconds(5));
        try
        {
            var (_, cookie) = await SignedIn([], shortSessions);
            var (_, closed) = await Connect(hub, cookie, shortSessions);

            Assert.That(await ClosesSoon(closed), Is.True, "CloseOnAuthenticationExpiration");
        }
        finally
        {
            foreach (var connection in _connections)
                await connection.DisposeAsync();
            _connections.Clear();
            await shortSessions.DisposeAsync();
            await TestDatabase.DropDatabase(connectionString, ifExists: true);
        }
    }
}
