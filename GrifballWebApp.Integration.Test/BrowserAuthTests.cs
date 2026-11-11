using GrifballWebApp.Server.Identity;
using GrifballWebApp.Server.Preview;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ServerProgram = GrifballWebApp.Server.Program;
using GrifballWebApp.Infrastructure.Preview;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// Signing in, as a browser (the HttpOnly session cookie, with antiforgery tokens for changes) and as a script
/// (a bearer token), against the real <see cref="ServerProgram"/> in preview mode, whose smoke-test users
/// (<see cref="PreviewSmokeUsers"/>, Sysadmin and Commissioner) sign in with a known password.
/// Non-parallelizable because Program.Main swaps the static Serilog logger.
/// </summary>
[TestFixture]
[NonParallelizable]
public class BrowserAuthTests
{
    private const string Password = "Smoke-Test-Password-1";
    // A Sysadmin-only POST that changes nothing worth undoing
    private const string AdminPost = "/password-reset-links/cleanup";

    private string _connectionString = null!;
    private WebApplicationFactory<ServerProgram> _factory = null!;

    private static string UserName => PreviewSmokeUsers.UserNames[0];

    private sealed class PreviewFactory(Dictionary<string, string?> settings) : WebApplicationFactory<ServerProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);
        }
    }

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _connectionString = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, $"BrowserAuthDb_{Guid.NewGuid():N}");
        _factory = new PreviewFactory(new()
        {
            ["Database:Provider"] = TestDatabase.Provider.ToString(),
            ["ConnectionStrings:GrifballWebApp"] = _connectionString,
            ["ApplyMigrations"] = "true",
            ["CreateDatabase"] = "true",
            [PreviewMode.ConfigurationKey] = "true",
            [PreviewSmokeUsers.PasswordKey] = Password,
        });
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _factory.DisposeAsync();
        await TestDatabase.DropDatabase(_connectionString, ifExists: true);
    }

    /// <summary>A browser: keeps cookies, over HTTPS (the cookies are Secure), and follows no redirects.</summary>
    private HttpClient Browser() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true,
        AllowAutoRedirect = false,
    });

    /// <summary>A script: no cookies.</summary>
    private HttpClient Script() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false,
        AllowAutoRedirect = false,
    });

    private static async Task<HttpResponseMessage> Login(HttpClient client, string query = "")
        => await client.PostAsJsonAsync("/identity/login" + query, new { Username = UserName, Password });

    private static string? SetCookie(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(name + "=", StringComparison.Ordinal))
            : null;

    private static async Task<string> AntiforgeryToken(HttpClient client)
    {
        using var response = await client.GetAsync("/identity/antiforgery-token");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Headers.CacheControl?.NoStore, Is.True, "a token is per user: never cached");
        }
        return (await response.Content.ReadFromJsonAsync<AntiforgeryTokenResponse>())!.Token;
    }

    private static HttpRequestMessage Post(string path, string? antiforgeryToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (antiforgeryToken is not null)
            request.Headers.Add(BrowserAuth.AntiforgeryHeader, antiforgeryToken);
        return request;
    }

    [Test]
    public async Task Login_SetsTheSessionCookie_HttpOnlySecureLaxHostOnly()
    {
        using var client = Browser();
        using var response = await Login(client);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var cookie = SetCookie(response, BrowserAuth.CookieName);
        Assert.That(cookie, Is.Not.Null, "the session cookie");
        var attributes = cookie!.Split(';').Skip(1).Select(a => a.Trim().ToLowerInvariant()).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(attributes, Does.Contain("httponly"));
            Assert.That(attributes, Does.Contain("secure"));
            Assert.That(attributes, Does.Contain("samesite=lax"));
            Assert.That(attributes, Does.Contain("path=/"));
            // "__Host-" cookies may not name a domain
            Assert.That(attributes, Has.None.StartsWith("domain="));
            // Persistent: it outlives the browser session
            Assert.That(attributes, Has.Some.StartsWith("expires="));
        }
        // No token in the body for scripts on the page to read
        Assert.That(await response.Content.ReadAsStringAsync(), Is.Empty);
    }

    [Test]
    public async Task Cookie_AuthenticatesTheBrowser()
    {
        using var client = Browser();
        using (var login = await Login(client))
            Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var me = await client.GetAsync("/identity/me");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(me.Headers.CacheControl?.NoStore, Is.True, "who is signed in is never cached");
        }
        var info = await me.Content.ReadFromJsonAsync<MetaInfoResponse>();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(info!.DisplayName, Is.EqualTo(UserName));
            Assert.That(info.IsSysAdmin && info.IsCommissioner, Is.True);
        }

        using var metaInfo = await client.GetAsync("/identity/meta-info");
        Assert.That(metaInfo.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Visitor_GetsNoContentFromMe_And401NotARedirect()
    {
        using var client = Browser();

        using var me = await client.GetAsync("/identity/me");
        Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        using var metaInfo = await client.GetAsync("/identity/meta-info");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(metaInfo.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(metaInfo.Headers.Location, Is.Null);
        }

        using var admin = await client.SendAsync(Post(AdminPost));
        Assert.That(admin.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task SignedIn_WithoutTheRole_Is403NotARedirect()
    {
        using var client = Browser();
        var userName = "player-" + Guid.NewGuid().ToString("N")[..8];
        using (var register = await client.PostAsJsonAsync("/identity/register", new { Username = userName, Password }))
            Assert.That(register.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        using (var login = await client.PostAsJsonAsync("/identity/login", new { Username = userName, Password }))
            Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var response = await client.SendAsync(Post(AdminPost, await AntiforgeryToken(client)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(response.Headers.Location, Is.Null);
        }
    }

    [Test]
    public async Task CookiePost_WithoutAntiforgeryToken_IsRefused()
    {
        using var client = Browser();
        using (await Login(client)) { }

        using var response = await client.SendAsync(Post(AdminPost));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(response.Headers.TryGetValues(BrowserAuth.AntiforgeryRejectedHeader, out _), Is.True);
        }
    }

    [Test]
    public async Task CookiePost_WithAWrongAntiforgeryToken_IsRefused()
    {
        using var client = Browser();
        using (await Login(client)) { }
        await AntiforgeryToken(client);

        using var response = await client.SendAsync(Post(AdminPost, "not-the-token"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CookiePost_WithAntiforgeryToken_IsAccepted()
    {
        using var client = Browser();
        using (await Login(client)) { }
        var token = await AntiforgeryToken(client);

        using var response = await client.SendAsync(Post(AdminPost, token));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task AntiforgeryToken_FromBeforeSigningIn_IsRefusedAfterwards()
    {
        // The token belongs to the visitor it was made for; the client asks again after signing in.
        using var client = Browser();
        var visitorToken = await AntiforgeryToken(client);
        using (await Login(client)) { }

        using var response = await client.SendAsync(Post(AdminPost, visitorToken));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(response.Headers.TryGetValues(BrowserAuth.AntiforgeryRejectedHeader, out _), Is.True);
        }
    }

    [Test]
    public async Task Bearer_LoginWithoutCookies_GivesATokenThatWorksWithoutAntiforgery()
    {
        using var client = Script();
        using var login = await Login(client, "?useCookies=false");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(SetCookie(login, BrowserAuth.CookieName), Is.Null);
        }
        var token = (await login.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Authentication.BearerToken.AccessTokenResponse>())!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(token.AccessToken, Is.Not.Empty);
            Assert.That(token.RefreshToken, Is.Not.Empty);
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var metaInfo = await client.GetAsync("/identity/meta-info");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(metaInfo.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await metaInfo.Content.ReadFromJsonAsync<MetaInfoResponse>())!.DisplayName, Is.EqualTo(UserName));
        }

        // A browser never sends this header by itself, so no antiforgery token is needed
        using var post = await client.SendAsync(Post(AdminPost));
        Assert.That(post.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        using var refresh = await client.PostAsJsonAsync("/identity/refresh", new { token.RefreshToken });
        Assert.That(refresh.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Bearer_ABadToken_Is401_EvenWithASessionCookie()
    {
        using var client = Browser();
        using (await Login(client)) { }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/identity/meta-info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");
        using var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Logout_ClearsTheSessionCookie()
    {
        using var client = Browser();
        using (await Login(client)) { }

        using var logout = await client.SendAsync(Post("/identity/logout"));

        Assert.That(logout.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var cookie = SetCookie(logout, BrowserAuth.CookieName);
        Assert.That(cookie, Is.Not.Null.And.Contains("expires=Thu, 01 Jan 1970"));
        using var me = await client.GetAsync("/identity/me");
        Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public void SecurityStamp_IsCheckedEveryFewMinutes()
    {
        var options = _factory.Services.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value;
        Assert.That(options.ValidationInterval, Is.EqualTo(BrowserAuth.SecurityStampValidationInterval));
        Assert.That(options.ValidationInterval, Is.LessThanOrEqualTo(TimeSpan.FromMinutes(5)));
    }

    [Test]
    public async Task Logout_EndsCopiesOfTheSessionCookie()
    {
        // Checked on every request here, rather than after BrowserAuth.SecurityStampValidationInterval
        await using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero)));
        var options = new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false, AllowAutoRedirect = false };
        using var client = factory.CreateClient(options);
        using var login = await Login(client);
        var cookie = SetCookie(login, BrowserAuth.CookieName)!.Split(';')[0];

        HttpRequestMessage WithCookie(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Add("Cookie", cookie);
            return request;
        }

        using (var before = await client.SendAsync(WithCookie(HttpMethod.Get, "/identity/meta-info")))
            Assert.That(before.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var logout = await client.SendAsync(WithCookie(HttpMethod.Post, "/identity/logout")))
            Assert.That(logout.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // A copy of the cookie kept from before the logout no longer signs anyone in
        using var after = await client.SendAsync(WithCookie(HttpMethod.Get, "/identity/meta-info"));
        Assert.That(after.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    // ---------- SignalR hub: cookie sessions only from this site's own pages ----------

    private const string Negotiate = "/TeamsHub/negotiate?negotiateVersion=1";

    private static HttpRequestMessage NegotiateRequest(string? origin = null, string? site = null, string? bearer = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Negotiate);
        if (origin is not null)
            request.Headers.Add("Origin", origin);
        if (site is not null)
            request.Headers.Add("Sec-Fetch-Site", site);
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return request;
    }

    [TestCase("https://grif-preview1.grifball.xyz", null)]
    [TestCase("https://evil.example", null)]
    [TestCase("null", null)]
    [TestCase("https://localhost:8443", null)]
    [TestCase(null, "same-site")]
    [TestCase(null, "cross-site")]
    [TestCase(null, "none")]
    [TestCase("https://localhost", "same-site")]
    public async Task Hub_CookieSession_FromAnotherOrigin_IsRefused(string? origin, string? site)
    {
        using var client = Browser();
        using (await Login(client)) { }

        using var response = await client.SendAsync(NegotiateRequest(origin, site));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [TestCase("https://localhost", "same-origin")]
    [TestCase("https://LOCALHOST", null)]
    [TestCase("http://localhost", null)]
    [TestCase(null, null)]
    public async Task Hub_CookieSession_FromThisSite_IsAccepted(string? origin, string? site)
    {
        using var client = Browser();
        using (await Login(client)) { }

        using var response = await client.SendAsync(NegotiateRequest(origin, site));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Hub_BearerOrVisitor_IsNotCheckedForOrigin()
    {
        using var script = Script();
        using var login = await Login(script, "?useCookies=false");
        var token = (await login.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Authentication.BearerToken.AccessTokenResponse>())!.AccessToken;

        using var bearer = await script.SendAsync(NegotiateRequest("https://evil.example", "cross-site", token));
        Assert.That(bearer.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var visitorClient = Browser();
        using var visitor = await visitorClient.SendAsync(NegotiateRequest("https://evil.example", "cross-site"));
        Assert.That(visitor.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Hub_WebSocket_WithTheCookie_FromAnotherOrigin_IsRefused_AndFromThisOneAccepted()
    {
        using var client = Browser();
        using var login = await Login(client);
        var cookie = SetCookie(login, BrowserAuth.CookieName)!.Split(';')[0];

        WebSocketClient Socket(string origin)
        {
            var socket = _factory.Server.CreateWebSocketClient();
            socket.ConfigureRequest = request =>
            {
                request.Headers.Cookie = cookie;
                request.Headers.Origin = origin;
            };
            return socket;
        }

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Socket("https://grif-preview1.grifball.xyz").ConnectAsync(new Uri("wss://localhost/TeamsHub"), CancellationToken.None));
        Assert.That(refused!.Message, Does.Contain("403"));

        using var accepted = await Socket("https://localhost").ConnectAsync(new Uri("wss://localhost/TeamsHub"), CancellationToken.None);
        Assert.That(accepted.State, Is.EqualTo(System.Net.WebSockets.WebSocketState.Open));
        await accepted.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
    }

    [Test]
    public async Task CookiePost_WithoutFetchMetadata_FromAnotherOrigin_IsRefused_EvenWhereNoTokenIsNeeded()
    {
        using var client = Browser();
        using (await Login(client)) { }

        using var foreign = Post("/identity/logout");
        foreign.Headers.Add("Origin", "https://grif-preview1.grifball.xyz");
        using var refused = await client.SendAsync(foreign);
        Assert.That(refused.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        using var own = Post("/identity/logout");
        own.Headers.Add("Origin", "https://localhost");
        using var accepted = await client.SendAsync(own);
        Assert.That(accepted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [TestCase("/identity/login", "Sec-Fetch-Site", "cross-site")]
    [TestCase("/identity/login", "Sec-Fetch-Site", "same-site")]
    [TestCase("/identity/login", "Origin", "https://grif-preview1.grifball.xyz")]
    [TestCase("/identity/register", "Sec-Fetch-Site", "same-site")]
    [TestCase("/identity/register", "Origin", "https://evil.example")]
    public async Task VisitorPost_FromAnotherSite_IsRefused(string path, string header, string value)
    {
        using var client = Script();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { Username = UserName, Password }),
        };
        request.Headers.Add(header, value);

        using var response = await client.SendAsync(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(SetCookie(response, BrowserAuth.CookieName), Is.Null, "not signed in");
        }
    }

    [TestCase(null, null)]
    [TestCase("Sec-Fetch-Site", "same-origin")]
    [TestCase("Origin", "https://localhost")]
    public async Task VisitorLogin_FromThisSiteOrAScript_IsAccepted(string? header, string? value)
    {
        using var client = Script();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/identity/login")
        {
            Content = JsonContent.Create(new { Username = UserName, Password }),
        };
        if (header is not null)
            request.Headers.Add(header, value);

        using var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task CookieGet_ToAnActionThatChangesSomething_IsNotRouted()
    {
        using var client = Browser();
        using (await Login(client)) { }

        using var response = await client.GetAsync("/halo/tokens/delete");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
    }

    [Test]
    public async Task CookieGet_FromAnotherSite_IsRefused()
    {
        using var client = Browser();
        using (await Login(client)) { }

        foreach (var site in new[] { "cross-site", "same-site" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/identity/meta-info");
            request.Headers.Add("Sec-Fetch-Site", site);
            using var response = await client.SendAsync(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), site);
        }
        foreach (var site in new[] { "same-origin", "none" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/identity/meta-info");
            request.Headers.Add("Sec-Fetch-Site", site);
            using var response = await client.SendAsync(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), site);
        }
    }

    [Test]
    public async Task Visitor_FromAnotherSite_IsServedAsUsual()
    {
        using var client = Browser();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/identity/me");
        request.Headers.Add("Sec-Fetch-Site", "cross-site");

        using var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }
}
