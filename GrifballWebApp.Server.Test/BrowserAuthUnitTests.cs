using GrifballWebApp.Server.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Security.Claims;

namespace GrifballWebApp.Server.Test;

/// <summary>BrowserAuthTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[NonParallelizable]
public class BrowserAuthUnitTests
{
    private static readonly string[] ExpectedAttributes = ["POST"];

    [Test]
    public void IsFromAnotherOrigin_ComparesHostAndPort()
    {
        static HttpRequest With(string host, string? origin)
        {
            var context = new DefaultHttpContext();
            context.Request.Host = new HostString(host);
            if (origin is not null)
                context.Request.Headers.Origin = origin;
            return context.Request;
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BrowserAuth.IsFromAnotherOrigin(With("grifball.xyz", null)), Is.False);
            Assert.That(BrowserAuth.IsFromAnotherOrigin(With("grifball.xyz", "https://grifball.xyz")), Is.False);
            Assert.That(BrowserAuth.IsFromAnotherOrigin(With("grifball.xyz:443", "https://grifball.xyz")), Is.False);
            Assert.That(BrowserAuth.IsFromAnotherOrigin(With("localhost:4200", "http://localhost:4200")), Is.False);
            Assert.That(BrowserAuth.IsFromAnotherOrigin(With("grifball.xyz", "https://grif-preview2.grifball.xyz")), Is.True);
            Assert.That(BrowserAuth.IsFromAnotherOrigin(With("localhost:4200", "http://localhost:5295")), Is.True);
            Assert.That(BrowserAuth.IsFromAnotherOrigin(With("grifball.xyz", "https://grifball.xyz:8443")), Is.True);
            Assert.That(BrowserAuth.IsFromAnotherOrigin(With("grifball.xyz", "null")), Is.True);
            Assert.That(BrowserAuth.IsFromAnotherOrigin(With("", "https://grifball.xyz")), Is.True);
        }
    }

    [Test]
    public async Task HubGuard_PassesNonHubEndpoints()
    {
        var context = Context("GET");
        context.Request.Headers.Origin = "https://evil.example";
        var called = false;

        await BrowserAuth.RefuseForeignHubRequests(context, _ => { called = true; return Task.CompletedTask; });

        Assert.That(called, Is.True);
    }

    [Test]
    public void IsUnsafeFromAnotherOriginWithoutFetchMetadata_Cases()
    {
        static HttpContext With(string method, string? origin, string? site = null, string? authorization = null)
        {
            var context = Context(method, authorization: authorization);
            context.Request.Host = new HostString("grifball.xyz");
            if (origin is not null)
                context.Request.Headers.Origin = origin;
            if (site is not null)
                context.Request.Headers["Sec-Fetch-Site"] = site;
            return context;
        }

        const string Foreign = "https://evil.grifball.xyz";
        using (Assert.EnterMultipleScope())
        {
            Assert.That(CookieAntiforgeryFilter.IsUnsafeFromAnotherOriginWithoutFetchMetadata(With("POST", Foreign)), Is.True);
            Assert.That(CookieAntiforgeryFilter.IsUnsafeFromAnotherOriginWithoutFetchMetadata(With("GET", Foreign)), Is.False);
            Assert.That(CookieAntiforgeryFilter.IsUnsafeFromAnotherOriginWithoutFetchMetadata(With("POST", "https://grifball.xyz")), Is.False);
            Assert.That(CookieAntiforgeryFilter.IsUnsafeFromAnotherOriginWithoutFetchMetadata(With("POST", null)), Is.False);
            Assert.That(CookieAntiforgeryFilter.IsUnsafeFromAnotherOriginWithoutFetchMetadata(With("POST", Foreign, site: "same-origin")), Is.False);
            // Signed in or not: a visitor's Login is a change too (login CSRF)
            Assert.That(CookieAntiforgeryFilter.IsUnsafeFromAnotherOriginWithoutFetchMetadata(With("POST", Foreign, authorization: "Bearer x")), Is.True);
            var visitor = With("POST", Foreign);
            visitor.User = new ClaimsPrincipal(new ClaimsIdentity());
            Assert.That(CookieAntiforgeryFilter.IsUnsafeFromAnotherOriginWithoutFetchMetadata(visitor), Is.True);
        }
    }

    // ---------- Which requests need a token (CookieAntiforgeryFilter.RequiresToken) ----------

    private static DefaultHttpContext Context(string method, bool signedIn = true, string? authorization = null)
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

    // Actions that change something: POSTs, so they need the antiforgery token and a link cannot call them.
    [TestCase(typeof(GrifballWebApp.Server.Controllers.AdminController), "SetCode")]
    [TestCase(typeof(GrifballWebApp.Server.Controllers.AdminController), "DeleteTokens")]
    [TestCase(typeof(GrifballWebApp.Server.Controllers.AdminController), "PullMatch")]
    [TestCase(typeof(GrifballWebApp.Server.Controllers.SeedController), "Medals")]
    [TestCase(typeof(GrifballWebApp.Server.Teams.TeamsController), "LockCaptains")]
    [TestCase(typeof(GrifballWebApp.Server.Teams.TeamsController), "UnlockCaptains")]
    [TestCase(typeof(GrifballWebApp.Server.Brackets.BracketsController), "CreateBracket")]
    [TestCase(typeof(GrifballWebApp.Server.Brackets.BracketsController), "SetSeeds")]
    [TestCase(typeof(GrifballWebApp.Server.MatchPlanner.MatchPlannerController), "CreateSeasonMatches")]
    [TestCase(typeof(GrifballWebApp.Server.SeasonMatchPage.SeasonMatchController), "ReportMatch")]
    [TestCase(typeof(GrifballWebApp.Server.Profile.ProfileController), "SetGamertag")]
    public void StateChangingAction_IsPostOnly(Type controller, string action)
    {
        var attributes = controller.GetMethod(action)!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute), true)
            .Cast<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().ToList();
        Assert.That(attributes.SelectMany(a => a.HttpMethods), Is.EquivalentTo(ExpectedAttributes));
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
        using (Assert.EnterMultipleScope())
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
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(CookieAntiforgeryFilter.IsFromAnotherSite(With("cross-site")), Is.True);
            Assert.That(CookieAntiforgeryFilter.IsFromAnotherSite(With("same-site")), Is.True);
            Assert.That(CookieAntiforgeryFilter.IsFromAnotherSite(With("same-origin")), Is.False);
            Assert.That(CookieAntiforgeryFilter.IsFromAnotherSite(With("none")), Is.False);
            Assert.That(CookieAntiforgeryFilter.IsFromAnotherSite(With(null)), Is.False);
        }
    }

    [Test]
    public void SelectScheme_BearerWithTheHeader_CookieOtherwise()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(BrowserAuth.SelectScheme(Context("GET", authorization: "Bearer abc")), Is.EqualTo("Identity.Bearer"));
            Assert.That(BrowserAuth.SelectScheme(Context("GET")), Is.EqualTo("Identity.Application"));
        }
    }
}
