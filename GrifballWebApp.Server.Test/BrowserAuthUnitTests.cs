using Microsoft.AspNetCore.SignalR;
using GrifballWebApp.Server.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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

    /// <summary>A request to a hub (negotiate or the connection), with its query.</summary>
    private static DefaultHttpContext HubRequest(string query, bool hub = true)
    {
        var context = Context("GET");
        context.Request.QueryString = new QueryString(query);
        var metadata = hub ? new EndpointMetadataCollection(new HubMetadata(typeof(GrifballWebApp.Server.Teams.TeamsHub))) : EndpointMetadataCollection.Empty;
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, hub ? "hub" : "api"));
        return context;
    }

    [Test]
    public void SelectScheme_BearerForAHubsAccessTokenInItsQuery()
    {
        // SignalR's browser client sends the token in the query on the WebSocket, which can't carry headers
        var hub = HubRequest("?id=1&access_token=abc");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BrowserAuth.HubAccessToken(hub), Is.EqualTo("abc"));
            Assert.That(BrowserAuth.SelectScheme(hub), Is.EqualTo("Identity.Bearer"));
            Assert.That(BrowserAuth.IsForeignHubRequest(hub), Is.False, "A token is not a cookie session");
        }
    }

    [Test]
    public void HubAccessToken_OnlyAHubsAndOnlyWithAValue()
    {
        using (Assert.EnterMultipleScope())
        {
            // An API call's query token is not read: it would end up in logs and history
            Assert.That(BrowserAuth.HubAccessToken(HubRequest("?access_token=abc", hub: false)), Is.Null);
            Assert.That(BrowserAuth.SelectScheme(HubRequest("?access_token=abc", hub: false)), Is.EqualTo("Identity.Application"));
            Assert.That(BrowserAuth.HubAccessToken(HubRequest("?access_token=")), Is.Null);
            Assert.That(BrowserAuth.SelectScheme(HubRequest("?id=1")), Is.EqualTo("Identity.Application"));
            // No endpoint at all (a request routing found nothing for)
            var unrouted = Context("GET");
            unrouted.Request.QueryString = new QueryString("?access_token=abc");
            Assert.That(BrowserAuth.HubAccessToken(unrouted), Is.Null);
        }
    }

    [Test]
    public void IsCookieSession_SignedInWithoutABearerToken()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(CookieAntiforgeryFilter.IsCookieSession(Context("GET")), Is.True);
            Assert.That(CookieAntiforgeryFilter.IsCookieSession(Context("GET", authorization: "Bearer abc")), Is.False);
            Assert.That(CookieAntiforgeryFilter.IsCookieSession(Context("GET", signedIn: false)), Is.False);
        }
    }

    [Test]
    public void ConfigureCookie_SetsTheHostOnlySessionCookie()
    {
        var options = new CookieAuthenticationOptions();

        BrowserAuth.ConfigureCookie(options);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.Cookie.Name, Is.EqualTo(BrowserAuth.CookieName));
            Assert.That(options.Cookie.HttpOnly, Is.True);
            Assert.That(options.Cookie.SecurePolicy, Is.EqualTo(CookieSecurePolicy.Always));
            Assert.That(options.Cookie.SameSite, Is.EqualTo(SameSiteMode.Lax));
            Assert.That(options.ExpireTimeSpan, Is.EqualTo(BrowserAuth.CookieLifetime));
            Assert.That(options.SlidingExpiration, Is.True);
        }
    }

    [Test]
    public void ConfigureAntiforgery_SetsTheHostOnlyStrictCookie()
    {
        var options = new AntiforgeryOptions();

        BrowserAuth.ConfigureAntiforgery(options);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.HeaderName, Is.EqualTo(BrowserAuth.AntiforgeryHeader));
            Assert.That(options.Cookie.Name, Is.EqualTo(BrowserAuth.AntiforgeryCookieName));
            Assert.That(options.Cookie.HttpOnly, Is.True);
            Assert.That(options.Cookie.SecurePolicy, Is.EqualTo(CookieSecurePolicy.Always));
            Assert.That(options.Cookie.SameSite, Is.EqualTo(SameSiteMode.Strict));
        }
    }

    // ---------- CookieAntiforgeryFilter.OnAuthorizationAsync ----------

    private static AuthorizationFilterContext FilterContext(HttpContext httpContext, List<object>? endpointMetadata = null)
    {
        var actionDescriptor = new ActionDescriptor
        {
            EndpointMetadata = endpointMetadata ?? [],
        };
        var actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
        return new AuthorizationFilterContext(actionContext, []);
    }

    [Test]
    public async Task OnAuthorizationAsync_Refuses403_FromAnotherSite()
    {
        var context = Context("POST");
        context.Request.Headers["Sec-Fetch-Site"] = "cross-site";
        var filterContext = FilterContext(context);
        var filter = new CookieAntiforgeryFilter(Substitute.For<IAntiforgery>(), new RecordingLogger<CookieAntiforgeryFilter>());

        await filter.OnAuthorizationAsync(filterContext);

        Assert.That(((ObjectResult)filterContext.Result!).StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task OnAuthorizationAsync_Refuses403_FromAnotherOriginWithoutFetchMetadata()
    {
        var context = Context("POST", signedIn: false);
        context.Request.Host = new HostString("grifball.xyz");
        context.Request.Headers.Origin = "https://evil.example";
        var filterContext = FilterContext(context);
        var filter = new CookieAntiforgeryFilter(Substitute.For<IAntiforgery>(), new RecordingLogger<CookieAntiforgeryFilter>());

        await filter.OnAuthorizationAsync(filterContext);

        Assert.That(((ObjectResult)filterContext.Result!).StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task OnAuthorizationAsync_DoesNothing_ForASafeMethod()
    {
        var context = Context("GET");
        var filterContext = FilterContext(context);
        var filter = new CookieAntiforgeryFilter(Substitute.For<IAntiforgery>(), new RecordingLogger<CookieAntiforgeryFilter>());

        await filter.OnAuthorizationAsync(filterContext);

        Assert.That(filterContext.Result, Is.Null);
    }

    [Test]
    public async Task OnAuthorizationAsync_DoesNothing_WhenTheTokenValidates()
    {
        var context = Context("POST");
        var filterContext = FilterContext(context);
        var antiforgery = Substitute.For<IAntiforgery>();
        var filter = new CookieAntiforgeryFilter(antiforgery, new RecordingLogger<CookieAntiforgeryFilter>());

        await filter.OnAuthorizationAsync(filterContext);

        // Not just that nothing refused the request: that the token was actually checked (a wrongly false
        // RequiresToken would also leave Result null, without ever calling ValidateRequestAsync).
        Assert.That(filterContext.Result, Is.Null);
        await antiforgery.Received(1).ValidateRequestAsync(context);
    }

    [Test]
    public async Task OnAuthorizationAsync_Refuses400_WhenTheTokenDoesNotValidate()
    {
        var context = Context("POST");
        var filterContext = FilterContext(context);
        var antiforgery = Substitute.For<IAntiforgery>();
        antiforgery.ValidateRequestAsync(context).ThrowsAsync(new AntiforgeryValidationException("bad token"));
        var filter = new CookieAntiforgeryFilter(antiforgery, new RecordingLogger<CookieAntiforgeryFilter>());

        await filter.OnAuthorizationAsync(filterContext);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(((ObjectResult)filterContext.Result!).StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(context.Response.Headers[BrowserAuth.AntiforgeryRejectedHeader].ToString(), Is.EqualTo("true"));
        }
    }

    [Test]
    public async Task OnAuthorizationAsync_SkipsTheToken_ForAnIgnoredAction()
    {
        var context = Context("POST");
        var filterContext = FilterContext(context, [new IgnoreAntiforgeryTokenAttribute()]);
        var antiforgery = Substitute.For<IAntiforgery>();
        var filter = new CookieAntiforgeryFilter(antiforgery, new RecordingLogger<CookieAntiforgeryFilter>());

        await filter.OnAuthorizationAsync(filterContext);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(filterContext.Result, Is.Null);
            Assert.That(antiforgery.ReceivedCalls(), Is.Empty);
        }
    }

    /// <summary>A request on <paramref name="context"/> to a SignalR hub endpoint, its services logging to <paramref name="logger"/>.</summary>
    private static DefaultHttpContext ToAHub(DefaultHttpContext context, Microsoft.Extensions.Logging.ILogger logger)
    {
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new Microsoft.AspNetCore.SignalR.HubMetadata(typeof(GrifballWebApp.Server.Teams.TeamsHub))), "teams hub"));
        context.Request.Path = "/hubs/teams/negotiate";
        var loggers = Substitute.For<Microsoft.Extensions.Logging.ILoggerFactory>();
        loggers.CreateLogger(typeof(BrowserAuth).FullName!).Returns(logger);
        context.RequestServices = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton(loggers)
            .BuildServiceProvider();
        return context;
    }

    [Test]
    public async Task HubGuard_RefusesACookieSessionsHubRequestFromAnotherSite_AndLogsIt()
    {
        var logger = new RecordingLogger<BrowserAuthUnitTests>();
        var context = ToAHub(Context("GET"), logger);
        context.Request.Headers["Sec-Fetch-Site"] = "same-site";
        var called = false;

        await BrowserAuth.RefuseForeignHubRequests(context, _ => { called = true; return Task.CompletedTask; });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(called, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            Assert.That(logger.Entries.Single().Message, Is.EqualTo("Refused hub request /hubs/teams/negotiate from another origin"));
        }
    }

    [TestCase(true, "same-origin", null)]
    [TestCase(true, "", "https://grifball.xyz")]
    [TestCase(false, "cross-site", "https://evil.example")]
    public async Task HubGuard_PassesAHubRequestFromThisSite_OrWithoutACookieSession(bool signedIn, string fetchSite, string? origin)
    {
        var logger = new RecordingLogger<BrowserAuthUnitTests>();
        var context = ToAHub(Context("GET", signedIn), logger);
        context.Request.Host = new HostString("grifball.xyz");
        if (fetchSite.Length > 0)
            context.Request.Headers["Sec-Fetch-Site"] = fetchSite;
        if (origin is not null)
            context.Request.Headers.Origin = origin;
        var called = false;

        await BrowserAuth.RefuseForeignHubRequests(context, _ => { called = true; return Task.CompletedTask; });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(called, Is.True);
            Assert.That(logger.Entries, Is.Empty);
        }
    }

    [Test]
    public async Task ConfigureCookie_AnswersAnApiCallWithoutASession401_AndOneNotAllowed403_NotARedirect()
    {
        var options = new CookieAuthenticationOptions();
        BrowserAuth.ConfigureCookie(options);
        var scheme = new Microsoft.AspNetCore.Authentication.AuthenticationScheme(BrowserAuth.CookieName, null, typeof(CookieAuthenticationHandler));
        var signIn = new DefaultHttpContext();
        var denied = new DefaultHttpContext();

        await options.Events.RedirectToLogin(new Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions>(signIn, scheme, options, new(), "/login"));
        await options.Events.RedirectToAccessDenied(new Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions>(denied, scheme, options, new(), "/denied"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(signIn.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
            Assert.That(denied.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            Assert.That(signIn.Response.Headers.Location.ToString(), Is.Empty);
        }
    }
}
