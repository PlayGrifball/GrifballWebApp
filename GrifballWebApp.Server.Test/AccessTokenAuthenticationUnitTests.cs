using System.Reflection;
using System.Security.Claims;
using GrifballWebApp.Server.Mcp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The static rules <see cref="AccessTokenAuthentication"/> and its filter/middleware friends enforce: whether a request
/// was signed in by an access token, whether it's read-only, which requests go to MCP, and the global guards that use those
/// facts. No database, host or network: <see cref="GrifMcp"/>'s and <see cref="AccessTokenAuthentication"/>'s own sign-in
/// flow is covered by the integration tests (AccessTokenScopeTests, McpServerTests).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class AccessTokenAuthenticationUnitTests
{
    private static ClaimsPrincipal User(string? tokenId = null, string? tokenName = null, bool? readOnly = null)
    {
        var claims = new List<Claim>();
        if (tokenId is not null)
            claims.Add(new Claim(AccessTokenAuthentication.TokenIdClaimType, tokenId));
        if (tokenName is not null)
            claims.Add(new Claim(AccessTokenAuthentication.TokenNameClaimType, tokenName));
        if (readOnly is not null)
            claims.Add(new Claim(AccessTokenAuthentication.ReadOnlyClaimType, readOnly.Value ? "true" : "false"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    [Test]
    public void IsAccessToken_OnlyWithTheTokenIdClaim()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(AccessTokenAuthentication.IsAccessToken(User(tokenId: "abc")), Is.True);
            Assert.That(AccessTokenAuthentication.IsAccessToken(User()), Is.False);
            Assert.That(AccessTokenAuthentication.IsAccessToken(new ClaimsPrincipal()), Is.False, "no identity at all");
        }
    }

    [Test]
    public void IsReadOnly_OnlyWhenTheClaimSaysTrue()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(AccessTokenAuthentication.IsReadOnly(User(tokenId: "abc", readOnly: true)), Is.True);
            Assert.That(AccessTokenAuthentication.IsReadOnly(User(tokenId: "abc", readOnly: false)), Is.False);
            Assert.That(AccessTokenAuthentication.IsReadOnly(User(tokenId: "abc")), Is.False, "no claim at all: not an access token's user or one made before read-only existed");
            Assert.That(AccessTokenAuthentication.IsReadOnly(new ClaimsPrincipal()), Is.False);
        }
    }

    [TestCase("GET", true)]
    [TestCase("HEAD", true)]
    [TestCase("get", true)]
    [TestCase("POST", false)]
    [TestCase("PUT", false)]
    [TestCase("DELETE", false)]
    public void IsReadMethod_OnlyGetOrHead(string method, bool expected)
        => Assert.That(AccessTokenAuthentication.IsReadMethod(method), Is.EqualTo(expected));

    /// <summary>
    /// A stand-in for the real <see cref="IProblemDetailsService"/> (registered through <c>AddProblemDetails</c> in Program):
    /// just enough to let <see cref="AccessTokenAuthentication"/>'s refusals write their body in a unit test with no host.
    /// </summary>
    private sealed class StubProblemDetailsService : IProblemDetailsService
    {
        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            context.HttpContext.Response.ContentType = "application/problem+json";
            return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(context.ProblemDetails, context.HttpContext.RequestAborted));
        }
    }

    private static readonly IServiceProvider Services = new ServiceCollection()
        .AddSingleton<IProblemDetailsService, StubProblemDetailsService>()
        .BuildServiceProvider();

    private static DefaultHttpContext Context(string method, string path, ClaimsPrincipal? user = null)
    {
        var context = new DefaultHttpContext { User = user ?? new ClaimsPrincipal(new ClaimsIdentity()), RequestServices = Services };
        context.Request.Method = method;
        context.Request.Path = path;
        return context;
    }

    [TestCase("/mcp")]
    [TestCase("/mcp/")]
    [TestCase("/mcp/anything")]
    public void IsMcpRequest_TheMcpPathAndItsSubPaths(string path)
        => Assert.That(GrifMcp.IsMcpRequest(Context("POST", path)), Is.True);

    [TestCase("/")]
    [TestCase("/seasons")]
    [TestCase("/mcpx")]
    public void IsMcpRequest_NotOtherPaths(string path)
        => Assert.That(GrifMcp.IsMcpRequest(Context("POST", path)), Is.False);

    [Test]
    public void Caller_ReadsTheUserIdRolesAndName()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "7"),
            new Claim(ClaimTypes.Role, "Sysadmin"),
            new Claim(ClaimTypes.Role, "Commissioner"),
            new Claim(ClaimTypes.Name, "Noah"),
        ], "Bearer"));

        var caller = GrifMcp.Caller(user);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(caller.UserID, Is.EqualTo(7));
            Assert.That(caller.Roles, Is.EquivalentTo(new[] { "Sysadmin", "Commissioner" }));
            Assert.That(caller.Name, Is.EqualTo("Noah"));
        }
    }

    [Test]
    public void Caller_WithNoNameClaim_HasNoName()
    {
        // Identity.Name reads the ClaimTypes.Name claim: none here, so it (and so ToolCaller.Name) is null, not thrown.
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "7")], "Bearer"));

        Assert.That(GrifMcp.Caller(user).Name, Is.Null);
    }

    [Test]
    public async Task RefuseWritesFromReadOnlyTokens_PassesReadsAndNonReadOnlyUsers_AndTheMcpEndpoint()
    {
        var called = 0;
        Task Next(HttpContext _) { called++; return Task.CompletedTask; }

        await AccessTokenAuthentication.RefuseWritesFromReadOnlyTokens(Context("GET", "/seasons", User(tokenId: "a", readOnly: true)), Next);
        await AccessTokenAuthentication.RefuseWritesFromReadOnlyTokens(Context("POST", "/seasons", User(tokenId: "a", readOnly: false)), Next);
        await AccessTokenAuthentication.RefuseWritesFromReadOnlyTokens(Context("POST", "/seasons"), Next);
        await AccessTokenAuthentication.RefuseWritesFromReadOnlyTokens(Context("POST", "/mcp", User(tokenId: "a", readOnly: true)), Next);

        Assert.That(called, Is.EqualTo(4));
    }

    [Test]
    public async Task RefuseWritesFromReadOnlyTokens_Refuses403_AWriteFromAReadOnlyToken_OffMcp()
    {
        var called = false;
        var context = Context("POST", "/seasons", User(tokenId: "a", readOnly: true));
        context.Response.Body = new MemoryStream();

        await AccessTokenAuthentication.RefuseWritesFromReadOnlyTokens(context, _ => { called = true; return Task.CompletedTask; });

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(called, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            Assert.That(body, Does.Contain("read-only"));
        }
    }

    // ---------- RefuseHubConnections ----------

    private static DefaultHttpContext HubRequest(string method, string query = "")
    {
        var context = Context(method, "/" + GrifballWebApp.Server.Live.LiveHub.Path);
        context.Request.QueryString = new QueryString(query);
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new HubMetadata(typeof(GrifballWebApp.Server.Live.LiveHub))), "hub"));
        return context;
    }

    private static string AGrifToken => "grif_" + new string('0', 32) + "_secret";

    [Test]
    public async Task RefuseHubConnections_Refuses401_AGrifTokenInTheHeader()
    {
        var context = HubRequest("GET");
        context.Request.Headers.Authorization = "Bearer " + AGrifToken;
        context.Response.Body = new MemoryStream();
        var called = false;

        await AccessTokenAuthentication.RefuseHubConnections(context, _ => { called = true; return Task.CompletedTask; });

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(called, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
            Assert.That(body, Does.Contain("don't open hubs"));
        }
    }

    [Test]
    public async Task RefuseHubConnections_Refuses401_AGrifTokenInTheQuery()
    {
        // SignalR's browser client can't send a header on the WebSocket, so it sends the token in the query instead.
        var context = HubRequest("GET", "?access_token=" + Uri.EscapeDataString(AGrifToken));
        context.Response.Body = new MemoryStream();
        var called = false;

        await AccessTokenAuthentication.RefuseHubConnections(context, _ => { called = true; return Task.CompletedTask; });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(called, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
        }
    }

    [Test]
    public async Task RefuseHubConnections_PassesNonHubRequests_AndHubRequestsWithoutAGrifToken()
    {
        var called = 0;
        Task Next(HttpContext _) { called++; return Task.CompletedTask; }

        // Not a hub at all, even with a grif_ token
        var api = Context("GET", "/seasons");
        api.Request.Headers.Authorization = "Bearer " + AGrifToken;
        await AccessTokenAuthentication.RefuseHubConnections(api, Next);

        // A hub, but no bearer token at all (a browser's cookie)
        await AccessTokenAuthentication.RefuseHubConnections(HubRequest("GET"), Next);

        // A hub, with the API's own short-lived bearer token (not grif_)
        var ownBearer = HubRequest("GET");
        ownBearer.Request.Headers.Authorization = "Bearer not-a-grif-token";
        await AccessTokenAuthentication.RefuseHubConnections(ownBearer, Next);

        Assert.That(called, Is.EqualTo(3));
    }

    // ---------- RequireAValidToken ----------

    [Test]
    public async Task RequireAValidToken_PassesRequestsWithNoGrifTokenToAuthenticate()
    {
        // These all return before ever calling AuthenticateAsync, so no host or auth system is needed for this test;
        // AccessTokenScopeTests covers the "a grif_ token was presented but failed to authenticate" 401 end to end.
        var called = 0;
        Task Next(HttpContext _) { called++; return Task.CompletedTask; }

        await AccessTokenAuthentication.RequireAValidToken(Context("GET", "/seasons"), Next); // no bearer token at all
        var ownBearer = Context("GET", "/seasons");
        ownBearer.Request.Headers.Authorization = "Bearer not-a-grif-token";
        await AccessTokenAuthentication.RequireAValidToken(ownBearer, Next); // the API's own bearer token, not grif_
        await AccessTokenAuthentication.RequireAValidToken(HubRequest("GET"), Next); // a hub: RefuseHubConnections' job
        var hubWithToken = HubRequest("GET");
        hubWithToken.Request.Headers.Authorization = "Bearer " + AGrifToken;
        await AccessTokenAuthentication.RequireAValidToken(hubWithToken, Next); // a hub with a grif_ token too

        Assert.That(called, Is.EqualTo(4));
    }

    // ---------- DenyAccessTokenFilter ----------

    private static AuthorizationFilterContext FilterContext(HttpContext httpContext, List<object>? endpointMetadata = null)
    {
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor { EndpointMetadata = endpointMetadata ?? [] });
        return new AuthorizationFilterContext(actionContext, []);
    }

    [Test]
    public async Task DenyAccessTokenFilter_DoesNothing_WhenNotAnAccessToken()
    {
        var context = FilterContext(Context("POST", "/user-merges"), [new DenyAccessTokenAttribute()]);

        await new DenyAccessTokenFilter().OnAuthorizationAsync(context);

        Assert.That(context.Result, Is.Null);
    }

    [Test]
    public async Task DenyAccessTokenFilter_DoesNothing_WithoutTheAttribute()
    {
        var context = FilterContext(Context("POST", "/user-merges", User(tokenId: "a")));

        await new DenyAccessTokenFilter().OnAuthorizationAsync(context);

        Assert.That(context.Result, Is.Null);
    }

    [Test]
    public async Task DenyAccessTokenFilter_Refuses403_AnAccessTokenOnAMarkedAction_WhateverItsScopeOrReadOnlyFlag()
    {
        var httpContext = Context("POST", "/user-merges", User(tokenId: "a", readOnly: false));
        httpContext.Response.Body = new MemoryStream();
        var context = FilterContext(httpContext, [new DenyAccessTokenAttribute()]);

        await new DenyAccessTokenFilter().OnAuthorizationAsync(context);

        httpContext.Response.Body.Position = 0;
        var body = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.Result, Is.InstanceOf<EmptyResult>());
            Assert.That(httpContext.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            Assert.That(body, Does.Contain("may not do this"));
        }
    }

    // ---------- DenyAccessTokenFilter: the nearest attribute wins ----------

    [DenyAccessToken]
    private sealed class ClassDeniesFixture
    {
        [AllowAccessToken]
        public static void MethodAllows() { }
    }

    [AllowAccessToken]
    private sealed class ClassAllowsFixture
    {
        [DenyAccessToken]
        public static void MethodDenies() { }
    }

    private static ControllerActionDescriptor ControllerAction(Type controllerType, string methodName) => new()
    {
        MethodInfo = controllerType.GetMethod(methodName)!,
        ControllerTypeInfo = controllerType.GetTypeInfo(),
    };

    private static AuthorizationFilterContext ControllerFilterContext(HttpContext httpContext, ControllerActionDescriptor action)
        => new(new ActionContext(httpContext, new RouteData(), action), []);

    [Test]
    public void IsDenied_AMethodAllow_OverridesAClassDeny()
    {
        var action = ControllerAction(typeof(ClassDeniesFixture), nameof(ClassDeniesFixture.MethodAllows));

        Assert.That(DenyAccessTokenFilter.IsDenied(action), Is.False);
    }

    [Test]
    public void IsDenied_AMethodDeny_OverridesAClassAllow()
    {
        var action = ControllerAction(typeof(ClassAllowsFixture), nameof(ClassAllowsFixture.MethodDenies));

        Assert.That(DenyAccessTokenFilter.IsDenied(action), Is.True);
    }

    [Test]
    public async Task DenyAccessTokenFilter_PassesAnAccessToken_WhenTheMethodAllowsDespiteTheClassDenying()
    {
        var httpContext = Context("POST", "/anything", User(tokenId: "a"));
        var context = ControllerFilterContext(httpContext, ControllerAction(typeof(ClassDeniesFixture), nameof(ClassDeniesFixture.MethodAllows)));

        await new DenyAccessTokenFilter().OnAuthorizationAsync(context);

        Assert.That(context.Result, Is.Null);
    }

    [Test]
    public async Task DenyAccessTokenFilter_Refuses403_WhenTheMethodDeniesDespiteTheClassAllowing()
    {
        var httpContext = Context("POST", "/anything", User(tokenId: "a"));
        httpContext.Response.Body = new MemoryStream();
        var context = ControllerFilterContext(httpContext, ControllerAction(typeof(ClassAllowsFixture), nameof(ClassAllowsFixture.MethodDenies)));

        await new DenyAccessTokenFilter().OnAuthorizationAsync(context);

        Assert.That(context.Result, Is.InstanceOf<EmptyResult>());
    }
}
