using System.Security.Claims;
using GrifballWebApp.Server.Mcp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// <see cref="AccessTokenAuthorizationHandler"/>: the access token rules (deny metadata, read-only by method) as
/// ASP.NET's own authorization system sees them, through the fallback policy (<see cref="AccessTokenAuthorizationRequirement"/>'s
/// own remarks explain why only that one, not the default policy too). No host: a bare <see cref="HttpContextAccessor"/>
/// stands in for the request pipeline's own.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class AccessTokenAuthorizationHandlerTests
{
    private static ClaimsPrincipal TokenUser(bool readOnly = false)
        => new(new ClaimsIdentity(
        [
            new Claim(AccessTokenAuthentication.TokenIdClaimType, "a"),
            new Claim(AccessTokenAuthentication.ReadOnlyClaimType, readOnly ? "true" : "false"),
        ]));

    private static ClaimsPrincipal CookieUser() => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "Cookies"));

    private static DefaultHttpContext HttpContext(string method, string path, params object[] metadata)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test"));
        return context;
    }

    private static async Task<AuthorizationHandlerContext> Handle(ClaimsPrincipal user, HttpContext? http)
    {
        var handler = new AccessTokenAuthorizationHandler(new HttpContextAccessor { HttpContext = http });
        var context = new AuthorizationHandlerContext([new AccessTokenAuthorizationRequirement()], user, resource: null);
        await handler.HandleAsync(context);
        return context;
    }

    [Test]
    public async Task Succeeds_WhenNotAnAccessToken()
    {
        var context = await Handle(CookieUser(), HttpContext("POST", "/anything"));

        Assert.That(context.HasSucceeded, Is.True);
    }

    [Test]
    public async Task Fails_AnAccessTokenOnADenyAccessTokenEndpoint()
    {
        var context = await Handle(TokenUser(), HttpContext("GET", "/anything", new DenyAccessTokenAttribute()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.HasFailed, Is.True);
            Assert.That(context.FailureReasons.Select(r => r.Message), Has.Some.Contain("may not do this"));
        }
    }

    [Test]
    public async Task Fails_AReadOnlyTokensWrite_OffMcp()
    {
        var context = await Handle(TokenUser(readOnly: true), HttpContext("POST", "/anything"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.HasFailed, Is.True);
            Assert.That(context.FailureReasons.Select(r => r.Message), Has.Some.Contain("read-only"));
        }
    }

    [Test]
    public async Task Succeeds_AReadOnlyTokensWrite_OnMcp()
    {
        // MCP's own tool-call filter enforces read-only by tool, not by HTTP method: every MCP call is a POST.
        var context = await Handle(TokenUser(readOnly: true), HttpContext("POST", "/mcp"));

        Assert.That(context.HasSucceeded, Is.True);
    }

    [Test]
    public async Task Succeeds_AnAccessTokenReading_WithNoDenyAttribute()
    {
        var context = await Handle(TokenUser(), HttpContext("GET", "/anything"));

        Assert.That(context.HasSucceeded, Is.True);
    }

    [Test]
    public async Task Succeeds_WhenThereIsNoCurrentHttpContext()
    {
        // Defensive: IHttpContextAccessor.HttpContext can be null outside a request; this must not block on missing info.
        var context = await Handle(TokenUser(), null);

        Assert.That(context.HasSucceeded, Is.True);
    }

    // ---------- AccessTokenAuthorizationResultHandler ----------

    private sealed class StubProblemDetailsService : IProblemDetailsService
    {
        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            context.HttpContext.Response.ContentType = "application/problem+json";
            return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(context.ProblemDetails, context.HttpContext.RequestAborted));
        }
    }

    private static DefaultHttpContext ResultHandlerContext()
    {
        var services = new ServiceCollection().AddSingleton<IProblemDetailsService, StubProblemDetailsService>().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Test]
    public async Task ResultHandler_Forbidden_WritesTheFirstReasonAsAProblemBody()
    {
        var context = ResultHandlerContext();
        var failure = AuthorizationFailure.Failed([new AuthorizationFailureReason(this, "The access token is read-only.")]);
        var called = false;

        await new AccessTokenAuthorizationResultHandler().HandleAsync(_ => { called = true; return Task.CompletedTask; }, context,
            new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build(), PolicyAuthorizationResult.Forbid(failure));

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(called, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            Assert.That(body, Does.Contain("read-only"));
        }
    }

    [Test]
    public async Task ResultHandler_Success_CallsNext_AsTheDefaultHandlerWould()
    {
        var context = ResultHandlerContext();
        var called = false;

        await new AccessTokenAuthorizationResultHandler().HandleAsync(_ => { called = true; return Task.CompletedTask; }, context,
            new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build(), PolicyAuthorizationResult.Success());

        Assert.That(called, Is.True);
    }
}
