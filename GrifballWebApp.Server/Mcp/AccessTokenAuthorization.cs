using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Mcp;

/// <summary>
/// The ASP.NET Core authorization requirement that carries <see cref="DenyAccessTokenAttribute"/> and the read-only
/// method rule into the framework's own authorization system (<see cref="AccessTokenAuthorizationHandler"/>), added to
/// the fallback policy (<see cref="GrifballWebApp.Server.Program"/>): an endpoint with no authorize metadata at all,
/// such as the minimal build-info endpoints, or a future one with neither <c>[Authorize]</c> nor
/// <c>[AllowAnonymous]</c>. That is genuinely every endpoint of that shape, which is what the fallback policy is for.
/// It is not added to the default policy: ASP.NET only combines that into a bare <c>[Authorize]</c>, never into
/// <c>[Authorize(Roles = "...")]</c> or a named <c>Policy</c> (the shape almost every sensitive MVC action here is
/// actually written in), so a default-policy requirement would look like it covers them and silently not.
/// <see cref="DenyAccessTokenFilter"/> (an MVC filter, which runs for every action regardless of how it is authorized)
/// and <see cref="AccessTokenAuthentication.RefuseWritesFromReadOnlyTokens"/> (middleware, before routing-dependent
/// authorization even runs) are what actually enforce both rules for MVC controllers, and stay registered unchanged.
/// Hubs are untouched too: they carry no authorize metadata naming this requirement, and
/// <see cref="AccessTokenAuthentication.RefuseHubConnections"/> already refuses any access token's hub request before
/// this would matter.
/// </summary>
public sealed class AccessTokenAuthorizationRequirement : IAuthorizationRequirement;

/// <summary>See <see cref="AccessTokenAuthorizationRequirement"/>.</summary>
public sealed class AccessTokenAuthorizationHandler : AuthorizationHandler<AccessTokenAuthorizationRequirement>
{
    private readonly IHttpContextAccessor _accessor;

    public AccessTokenAuthorizationHandler(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AccessTokenAuthorizationRequirement requirement)
    {
        if (!AccessTokenAuthentication.IsAccessToken(context.User))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Not context.Resource: depending on which caller evaluated the policy, that is an Endpoint for some and an
        // HttpContext for others, and the read-only rule below needs the request's method too, which only the
        // HttpContext carries. IHttpContextAccessor always gives the current request's, however it got here.
        var http = _accessor.HttpContext;
        var endpoint = http?.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<DenyAccessTokenAttribute>() is not null)
        {
            context.Fail(new AuthorizationFailureReason(this, "An access token may not do this."));
            return Task.CompletedTask;
        }

        if (http is not null && !GrifMcp.IsMcpRequest(http) && !AccessTokenAuthentication.IsReadMethod(http.Request.Method)
            && AccessTokenAuthentication.IsReadOnly(context.User))
        {
            context.Fail(new AuthorizationFailureReason(this, "The access token is read-only."));
            return Task.CompletedTask;
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Writes <see cref="AccessTokenAuthorizationHandler"/>'s own reason as the 403 body: the framework's default handler
/// answers an empty 403. Through <see cref="IProblemDetailsService"/>, like <see cref="AccessTokenAuthentication"/>'s
/// own refusals, so both share whatever the app customises on every problem response (the trace ID). Delegates to the
/// framework's own handler for every other policy result (a challenge, any other requirement's forbid), so this only
/// changes what this one requirement's failure looks like.
/// </summary>
public sealed class AccessTokenAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var reason = authorizeResult.Forbidden
            ? authorizeResult.AuthorizationFailure?.FailureReasons.Select(r => r.Message).FirstOrDefault(m => !string.IsNullOrEmpty(m))
            : null;
        if (reason is null)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = new ProblemDetails { Status = StatusCodes.Status403Forbidden, Title = reason } });
    }
}
