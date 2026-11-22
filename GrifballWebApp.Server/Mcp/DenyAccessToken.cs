using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GrifballWebApp.Server.Mcp;

/// <summary>
/// Marks an action a personal access token may never call, whatever its scope or read-only flag: making, listing or revoking
/// tokens, the account's password, email or linked logins, merges, and deleting the account stay the browser's (or, for a
/// password reset link, a one-time token's) own, never a stolen access token's.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class DenyAccessTokenAttribute : Attribute;

/// <summary>
/// Marks a sensitive action (Sysadmin-only, or on an Identity or external-login controller) that a reasonable script may
/// still call with a token: a read, or a normal staff write that doesn't touch sign-in, roles, or the account itself. The
/// architecture test that requires one of this or <see cref="DenyAccessTokenAttribute"/> on every sensitive action is the
/// record of that choice having been made on purpose, not an extra check at request time.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AllowAccessTokenAttribute : Attribute;

/// <summary>
/// Marks an action that merges one user into another, or undoes such a merge, wherever it lives (a dedicated merge
/// endpoint, or one that merges as a side effect, such as approving a gamertag claim). The architecture test requires
/// every one of these to also be <see cref="DenyAccessTokenAttribute"/>: a leaked token must never merge, or undo a
/// merge, whatever else its controller lets a token do.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MergesUsersAttribute : Attribute;

/// <summary>
/// Refuses an access-token-authenticated call to a <see cref="DenyAccessTokenAttribute"/> action with 403, whatever the
/// token's scope. A global MVC filter (registered once, like <see cref="GrifballWebApp.Server.Identity.CookieAntiforgeryFilter"/>),
/// since <see cref="AccessTokenAuthentication"/>'s own scheme has no endpoint-specific knowledge of this rule, and this
/// is the enforcement for MVC controllers: <see cref="AccessTokenAuthorizationHandler"/> covers the same rule through
/// ASP.NET's own authorization system too, but only for endpoints that reach the fallback policy (no authorize metadata
/// at all), which almost no MVC action here is - they are all <see cref="AllowAnonymousAttribute"/> or
/// <c>[Authorize(Roles = "...")]</c>, and ASP.NET does not combine the fallback or default policy into either (see
/// <see cref="AccessTokenAuthorizationRequirement"/>'s own remarks). This filter runs for every action regardless of
/// which it is, so it is unaffected either way.
/// </summary>
public sealed class DenyAccessTokenFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!AccessTokenAuthentication.IsAccessToken(context.HttpContext.User))
            return;
        if (!context.ActionDescriptor.EndpointMetadata.OfType<DenyAccessTokenAttribute>().Any())
            return;

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "An access token may not do this.",
        })
        { StatusCode = StatusCodes.Status403Forbidden };
    }
}
