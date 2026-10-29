using GrifballWebApp.Server.Identity;
using GrifballWebApp.Server.Preview;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using ServerProgram = GrifballWebApp.Server.Program;

namespace GrifballWebApp.Test;

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
    private const string AdminPost = "/Admin/CleanupExpiredPasswordResetLinks";

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
        => await client.PostAsJsonAsync("/Identity/Login" + query, new { Username = UserName, Password });

    private static string? SetCookie(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(name + "=", StringComparison.Ordinal))
            : null;

    private static async Task<string> AntiforgeryToken(HttpClient client)
    {
        using var response = await client.GetAsync("/Identity/Antiforgery");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True, "a token is per user: never cached");
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
        Assert.Multiple(() =>
        {
            Assert.That(attributes, Does.Contain("httponly"));
            Assert.That(attributes, Does.Contain("secure"));
            Assert.That(attributes, Does.Contain("samesite=lax"));
            Assert.That(attributes, Does.Contain("path=/"));
            // "__Host-" cookies may not name a domain
            Assert.That(attributes, Has.None.StartsWith("domain="));
            // Persistent: it outlives the browser session
            Assert.That(attributes, Has.Some.StartsWith("expires="));
        });
        // No token in the body for scripts on the page to read
        Assert.That(await response.Content.ReadAsStringAsync(), Is.Empty);
    }

    [Test]
    public async Task Cookie_AuthenticatesTheBrowser()
    {
        using var client = Browser();
        using (var login = await Login(client))
            Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var me = await client.GetAsync("/Identity/Me");
        Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(me.Headers.CacheControl?.NoStore, Is.True, "who is signed in is never cached");
        var info = await me.Content.ReadFromJsonAsync<MetaInfoResponse>();
        Assert.That(info!.DisplayName, Is.EqualTo(UserName));
        Assert.That(info.IsSysAdmin && info.IsCommissioner, Is.True);

        using var metaInfo = await client.GetAsync("/Identity/MetaInfo");
        Assert.That(metaInfo.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Visitor_GetsNoContentFromMe_And401NotARedirect()
    {
        using var client = Browser();

        using var me = await client.GetAsync("/Identity/Me");
        Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        using var metaInfo = await client.GetAsync("/Identity/MetaInfo");
        Assert.That(metaInfo.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(metaInfo.Headers.Location, Is.Null);

        using var admin = await client.SendAsync(Post(AdminPost));
        Assert.That(admin.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task SignedIn_WithoutTheRole_Is403NotARedirect()
    {
        using var client = Browser();
        var userName = "player-" + Guid.NewGuid().ToString("N")[..8];
        using (var register = await client.PostAsJsonAsync("/Identity/Register", new { Username = userName, Password }))
            Assert.That(register.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var login = await client.PostAsJsonAsync("/Identity/Login", new { Username = userName, Password }))
            Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var response = await client.SendAsync(Post(AdminPost, await AntiforgeryToken(client)));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(response.Headers.Location, Is.Null);
    }

    [Test]
    public async Task CookiePost_WithoutAntiforgeryToken_IsRefused()
    {
        using var client = Browser();
        using (await Login(client)) { }

        using var response = await client.SendAsync(Post(AdminPost));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Headers.TryGetValues(BrowserAuth.AntiforgeryRejectedHeader, out _), Is.True);
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

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task AntiforgeryToken_FromBeforeSigningIn_IsRefusedAfterwards()
    {
        // The token belongs to the visitor it was made for; the client asks again after signing in.
        using var client = Browser();
        var visitorToken = await AntiforgeryToken(client);
        using (await Login(client)) { }

        using var response = await client.SendAsync(Post(AdminPost, visitorToken));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Headers.TryGetValues(BrowserAuth.AntiforgeryRejectedHeader, out _), Is.True);
    }

    [Test]
    public async Task Bearer_LoginWithoutCookies_GivesATokenThatWorksWithoutAntiforgery()
    {
        using var client = Script();
        using var login = await Login(client, "?useCookies=false");

        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(SetCookie(login, BrowserAuth.CookieName), Is.Null);
        var token = (await login.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Authentication.BearerToken.AccessTokenResponse>())!;
        Assert.That(token.AccessToken, Is.Not.Empty);
        Assert.That(token.RefreshToken, Is.Not.Empty);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var metaInfo = await client.GetAsync("/Identity/MetaInfo");
        Assert.That(metaInfo.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await metaInfo.Content.ReadFromJsonAsync<MetaInfoResponse>())!.DisplayName, Is.EqualTo(UserName));

        // A browser never sends this header by itself, so no antiforgery token is needed
        using var post = await client.SendAsync(Post(AdminPost));
        Assert.That(post.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var refresh = await client.PostAsJsonAsync("/Identity/Refresh", new { token.RefreshToken });
        Assert.That(refresh.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Bearer_ABadToken_Is401_EvenWithASessionCookie()
    {
        using var client = Browser();
        using (await Login(client)) { }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/Identity/MetaInfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");
        using var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Logout_ClearsTheSessionCookie()
    {
        using var client = Browser();
        using (await Login(client)) { }

        using var logout = await client.SendAsync(Post("/Identity/Logout"));

        Assert.That(logout.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var cookie = SetCookie(logout, BrowserAuth.CookieName);
        Assert.That(cookie, Is.Not.Null.And.Contains("expires=Thu, 01 Jan 1970"));
        using var me = await client.GetAsync("/Identity/Me");
        Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    // ---------- Which requests need a token (CookieAntiforgeryFilter.RequiresToken) ----------

    private static HttpContext Context(string method, bool signedIn = true, string? authorization = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        if (authorization is not null)
            context.Request.Headers.Authorization = authorization;
        context.User = new ClaimsPrincipal(signedIn
            ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "Identity.Application")
            : new ClaimsIdentity());
        return context;
    }

    [TestCase("POST", true)]
    [TestCase("PUT", true)]
    [TestCase("PATCH", true)]
    [TestCase("DELETE", true)]
    [TestCase("GET", false)]
    [TestCase("HEAD", false)]
    [TestCase("OPTIONS", false)]
    [TestCase("TRACE", false)]
    public void RequiresToken_ForChangesFromACookieSession(string method, bool expected)
    {
        Assert.That(CookieAntiforgeryFilter.RequiresToken(Context(method), []), Is.EqualTo(expected));
    }

    [Test]
    public void RequiresToken_NotForBearerTokensVisitorsOrIgnoredActions()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CookieAntiforgeryFilter.RequiresToken(Context("POST", authorization: "Bearer abc"), []), Is.False);
            Assert.That(CookieAntiforgeryFilter.RequiresToken(Context("POST", authorization: "bearer abc"), []), Is.False);
            Assert.That(CookieAntiforgeryFilter.RequiresToken(Context("POST", authorization: "Basic abc"), []), Is.True);
            Assert.That(CookieAntiforgeryFilter.RequiresToken(Context("POST", signedIn: false), []), Is.False);
            var noIdentity = Context("POST");
            noIdentity.User = new ClaimsPrincipal();
            Assert.That(CookieAntiforgeryFilter.RequiresToken(noIdentity, []), Is.False);
            Assert.That(CookieAntiforgeryFilter.RequiresToken(Context("POST"), [new IgnoreAntiforgeryTokenAttribute()]), Is.False);
            Assert.That(CookieAntiforgeryFilter.RequiresToken(Context("POST"), [new HttpPostAttribute()]), Is.True);
        });
    }

    [Test]
    public async Task CookieGet_FromAnotherSite_IsRefused()
    {
        using var client = Browser();
        using (await Login(client)) { }

        foreach (var site in new[] { "cross-site", "same-site" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/Identity/MetaInfo");
            request.Headers.Add("Sec-Fetch-Site", site);
            using var response = await client.SendAsync(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), site);
        }
        foreach (var site in new[] { "same-origin", "none" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/Identity/MetaInfo");
            request.Headers.Add("Sec-Fetch-Site", site);
            using var response = await client.SendAsync(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), site);
        }
    }

    [Test]
    public async Task Visitor_FromAnotherSite_IsServedAsUsual()
    {
        using var client = Browser();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Identity/Me");
        request.Headers.Add("Sec-Fetch-Site", "cross-site");

        using var response = await client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public void IsFromAnotherSite_ByFetchMetadata()
    {
        static HttpRequest With(string? site)
        {
            var context = new DefaultHttpContext();
            if (site is not null)
                context.Request.Headers["Sec-Fetch-Site"] = site;
            return context.Request;
        }

        Assert.Multiple(() =>
        {
            Assert.That(CookieAntiforgeryFilter.IsFromAnotherSite(With("cross-site")), Is.True);
            Assert.That(CookieAntiforgeryFilter.IsFromAnotherSite(With("same-site")), Is.True);
            Assert.That(CookieAntiforgeryFilter.IsFromAnotherSite(With("same-origin")), Is.False);
            Assert.That(CookieAntiforgeryFilter.IsFromAnotherSite(With("none")), Is.False);
            Assert.That(CookieAntiforgeryFilter.IsFromAnotherSite(With(null)), Is.False);
        });
    }

    [Test]
    public void SelectScheme_BearerWithTheHeader_CookieOtherwise()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BrowserAuth.SelectScheme(Context("GET", authorization: "Bearer abc")), Is.EqualTo("Identity.Bearer"));
            Assert.That(BrowserAuth.SelectScheme(Context("GET")), Is.EqualTo("Identity.Application"));
        });
    }
}
