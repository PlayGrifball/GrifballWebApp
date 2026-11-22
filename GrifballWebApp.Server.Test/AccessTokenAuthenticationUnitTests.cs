using System.Security.Claims;
using GrifballWebApp.Server.Mcp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The static rules <see cref="AccessTokenAuthentication"/> and its filter/middleware friends enforce: whether a request
/// was signed in by an access token, whether it's read-only, which requests go to MCP, and the global guards that use those
/// facts. No database, host or network: <see cref="GrifMcp"/>'s and <see cref="AccessTokenAuthentication"/>'s own sign-in
/// flow is covered by the integration tests (AccessTokenScopeTests, McpServerTests).
/// </summary>
[TestFixture]
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

    private static DefaultHttpContext Context(string method, string path, ClaimsPrincipal? user = null)
    {
        var context = new DefaultHttpContext { User = user ?? new ClaimsPrincipal(new ClaimsIdentity()) };
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(called, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            context.Response.Body.Position = 0;
            var body = new StreamReader(context.Response.Body).ReadToEnd();
            Assert.That(body, Does.Contain("read-only"));
        }
    }

    // ---------- DenyAccessTokenFilter ----------

    private static AuthorizationFilterContext FilterContext(HttpContext httpContext, List<object>? endpointMetadata = null)
    {
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor { EndpointMetadata = endpointMetadata ?? [] });
        return new AuthorizationFilterContext(actionContext, []);
    }

    [Test]
    public void DenyAccessTokenFilter_DoesNothing_WhenNotAnAccessToken()
    {
        var context = FilterContext(Context("POST", "/user-merges"), [new DenyAccessTokenAttribute()]);

        new DenyAccessTokenFilter().OnAuthorization(context);

        Assert.That(context.Result, Is.Null);
    }

    [Test]
    public void DenyAccessTokenFilter_DoesNothing_WithoutTheAttribute()
    {
        var context = FilterContext(Context("POST", "/user-merges", User(tokenId: "a")));

        new DenyAccessTokenFilter().OnAuthorization(context);

        Assert.That(context.Result, Is.Null);
    }

    [Test]
    public void DenyAccessTokenFilter_Refuses403_AnAccessTokenOnAMarkedAction_WhateverItsScopeOrReadOnlyFlag()
    {
        var context = FilterContext(Context("POST", "/user-merges", User(tokenId: "a", readOnly: false)), [new DenyAccessTokenAttribute()]);

        new DenyAccessTokenFilter().OnAuthorization(context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.Result, Is.InstanceOf<ObjectResult>());
            var result = (ObjectResult)context.Result!;
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            Assert.That(((ProblemDetails)result.Value!).Title, Does.Contain("may not do this"));
        }
    }
}
