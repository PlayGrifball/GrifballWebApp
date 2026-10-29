using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GrifballWebApp.Server.Identity;

/// <summary>
/// How the API knows who is calling. Browsers sign in with an HttpOnly cookie (<see cref="CookieName"/>), which
/// scripts on the page cannot read, so an XSS bug cannot steal a session. Other clients (the post-deploy smoke
/// suite, scripts) can still ask <c>/Identity/Login?useCookies=false</c> for a bearer token and send it in the
/// Authorization header. <see cref="Scheme"/> picks one per request: bearer when the request has that header,
/// the cookie otherwise.
/// </summary>
public static class BrowserAuth
{
    /// <summary>The default scheme: forwards to bearer or the Identity cookie (see <see cref="SelectScheme"/>).</summary>
    public const string Scheme = "Identity.CookieOrBearer";

    /// <summary>
    /// The sign-in cookie. "__Host-" makes browsers refuse it unless it is Secure, for path / and without a Domain,
    /// so no other host (a subdomain, say) can set or overwrite it.
    /// </summary>
    public const string CookieName = "__Host-Grifball";

    /// <summary>The antiforgery cookie, which pairs with the request token in <see cref="AntiforgeryHeader"/>.</summary>
    public const string AntiforgeryCookieName = "__Host-Grifball.Antiforgery";

    /// <summary>The header the client sends the antiforgery request token in (from <c>GET /Identity/Antiforgery</c>).</summary>
    public const string AntiforgeryHeader = "X-XSRF-TOKEN";

    /// <summary>Set on the 400 for a missing or stale antiforgery token, so the client knows to fetch a new one and retry.</summary>
    public const string AntiforgeryRejectedHeader = "X-Antiforgery-Rejected";

    /// <summary>How long a session lasts without a visit; every request in the second half of it renews it.</summary>
    public static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(14);

    /// <summary>Whether the request carries a bearer token (and so is not a browser's cookie session).</summary>
    public static bool HasBearerToken(HttpRequest request)
        => request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

    /// <summary>The scheme that authenticates this request.</summary>
    public static string SelectScheme(HttpContext context)
        => HasBearerToken(context.Request) ? IdentityConstants.BearerScheme : IdentityConstants.ApplicationScheme;

    /// <summary>The sign-in cookie's settings. API calls without a session get 401 and 403, not a redirect to a login page.</summary>
    public static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = CookieLifetime;
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents()
        {
            OnRedirectToLogin = ctx =>
            {
                if (ctx.Response.StatusCode == StatusCodes.Status200OK)
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = ctx =>
            {
                if (ctx.Response.StatusCode == StatusCodes.Status200OK)
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            },
        };
    }

    /// <summary>The antiforgery cookie and header. The cookie is only ever needed by this site, so it is SameSite=Strict.</summary>
    public static void ConfigureAntiforgery(AntiforgeryOptions options)
    {
        options.HeaderName = AntiforgeryHeader;
        options.Cookie.Name = AntiforgeryCookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Path = "/";
    }
}

/// <summary>
/// Cross-site request forgery protection for cookie sessions (requests with a bearer token are not checked: a
/// browser never attaches one by itself; nor are requests without a session, which have nothing to abuse).
/// <list type="bullet">
/// <item>A request that changes something (anything but GET, HEAD, OPTIONS and TRACE) must carry the antiforgery
/// request token in <see cref="BrowserAuth.AntiforgeryHeader"/>; another site cannot read it, so it cannot forge
/// the request. SameSite=Lax already keeps the cookie off cross-site POSTs; this also covers same-site ones (other
/// grifball.xyz subdomains, such as the PR previews) and older browsers. Actions marked
/// <see cref="IgnoreAntiforgeryTokenAttribute"/> are exempt.</item>
/// <item>Any request the browser says came from another site or subdomain (Sec-Fetch-Site: cross-site or
/// same-site) is refused. SameSite=Lax still sends the cookie on a top-level navigation, and a few GET actions
/// change things (Admin/SetCode, Teams/LockCaptains, ...), so a link elsewhere must not be able to call them.</item>
/// </list>
/// </summary>
public sealed class CookieAntiforgeryFilter(IAntiforgery antiforgery, ILogger<CookieAntiforgeryFilter> logger) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (IsCookieSession(context.HttpContext) && IsFromAnotherSite(context.HttpContext.Request))
        {
            logger.LogInformation("Refused {Method} {Path} from another site", context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Requests from other sites are not accepted",
            })
            { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        if (!RequiresToken(context.HttpContext, context.ActionDescriptor.EndpointMetadata))
            return;

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException ex)
        {
            logger.LogInformation("Refused {Method} {Path}: {Reason}", context.HttpContext.Request.Method, context.HttpContext.Request.Path, ex.Message);
            context.HttpContext.Response.Headers[BrowserAuth.AntiforgeryRejectedHeader] = "true";
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Missing or invalid antiforgery token",
            })
            { StatusCode = StatusCodes.Status400BadRequest };
        }
    }

    /// <summary>Whether this request must carry an antiforgery token (see the class).</summary>
    public static bool RequiresToken(HttpContext context, IEnumerable<object> endpointMetadata)
    {
        var method = context.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
            return false;
        if (!IsCookieSession(context))
            return false;
        return !endpointMetadata.OfType<IgnoreAntiforgeryTokenAttribute>().Any();
    }

    /// <summary>Signed in, and not by a bearer token: the browser's cookie.</summary>
    public static bool IsCookieSession(HttpContext context)
        => !BrowserAuth.HasBearerToken(context.Request) && context.User.Identity?.IsAuthenticated is true;

    /// <summary>
    /// The browser says the request came from another site or subdomain (Fetch Metadata). "same-origin" (the app's
    /// own calls) and "none" (typed in the address bar, a bookmark) are fine, as is no header at all (older browsers,
    /// and the app's server calling the API while it renders a page).
    /// </summary>
    public static bool IsFromAnotherSite(HttpRequest request)
        => request.Headers["Sec-Fetch-Site"].ToString() is "cross-site" or "same-site";
}

/// <summary>The antiforgery request token, for <see cref="BrowserAuth.AntiforgeryHeader"/>.</summary>
public class AntiforgeryTokenResponse
{
    public required string Token { get; set; }
}
