using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GrifballWebApp.Server.Mcp;

/// <summary>
/// Marks an action a personal access token may never call, whatever its scope or read-only flag: making, listing or revoking
/// tokens, the account's password, email or linked logins, and deleting the account stay the browser's (or, for a
/// password reset link, a one-time token's) own, never a stolen access token's. Merges are not marked this way: they
/// are refused at the one place every merge path reaches regardless of which endpoint or tool started it
/// (<see cref="GrifballWebApp.Application.Profile.UserMergeService.Merge"/>, <see cref="GrifballWebApp.Application.Profile.UserMergeAuditService.Undo"/>,
/// both checking <c>ICurrentUserService.IsAccessToken</c>), since an attribute on just the REST actions let a merge
/// reached through MCP, historical import or setting a gamertag onto a placeholder slip past it (#549 review).
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
/// Refuses an access-token-authenticated call to a <see cref="DenyAccessTokenAttribute"/> action with 403, whatever the
/// token's scope. A global MVC filter (registered once, like <see cref="GrifballWebApp.Server.Identity.CookieAntiforgeryFilter"/>),
/// since <see cref="AccessTokenAuthentication"/>'s own scheme has no endpoint-specific knowledge of this rule.
/// </summary>
public sealed class DenyAccessTokenFilter : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!AccessTokenAuthentication.IsAccessToken(context.HttpContext.User))
            return;
        if (!context.ActionDescriptor.EndpointMetadata.OfType<DenyAccessTokenAttribute>().Any())
            return;

        await AccessTokenAuthentication.WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden, "An access token may not do this.");
        context.Result = new EmptyResult();
    }
}
