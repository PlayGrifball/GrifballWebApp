using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.SignalR;

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

    /// <summary>How often a cookie session is checked against the user's security stamp. Logging out changes the
    /// stamp, so a copy of the cookie stops working within this time (as do the user's other sessions).</summary>
    public static readonly TimeSpan SecurityStampValidationInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Whether the request's Origin header names another host than the one the request was sent to (or is "null",
    /// as from a sandboxed page). No Origin header is not "another": scripts and same-origin GETs send none.
    /// Only the host and port are compared: TLS ends at the ingress, so the backend may see http for an https page.
    /// </summary>
    public static bool IsFromAnotherOrigin(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin))
            return false;
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || !request.Host.HasValue)
            return true;
        // A Host without a port went to the scheme's default port, so the page must be on one too.
        var samePort = request.Host.Port is { } port ? uri.Port == port : uri.IsDefaultPort;
        return !string.Equals(uri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase) || !samePort;
    }

    /// <summary>
    /// A SignalR hub request (negotiate or the WebSocket upgrade) on a cookie session that did not come from this
    /// site's own pages: Fetch Metadata says other than same-origin, or the Origin is another host. The MVC
    /// antiforgery filter does not run for hubs, and SameSite=Lax still sends the cookie on a WebSocket opened by
    /// another subdomain. Bearer tokens and visitors are not checked (nothing a browser attaches by itself).
    /// </summary>
    public static bool IsForeignHubRequest(HttpContext context)
    {
        if (!CookieAntiforgeryFilter.IsCookieSession(context))
            return false;
        var site = context.Request.Headers["Sec-Fetch-Site"].ToString();
        return (site.Length > 0 && site != "same-origin") || IsFromAnotherOrigin(context.Request);
    }

    /// <summary>Middleware: refuses <see cref="IsForeignHubRequest"/> requests to any hub endpoint with 403.</summary>
    public static Task RefuseForeignHubRequests(HttpContext context, RequestDelegate next)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<HubMetadata>() is null || !IsForeignHubRequest(context))
            return next(context);
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(BrowserAuth).FullName!)
            .LogInformation("Refused hub request {Path} from another origin", context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

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
            // Identity's own handler, which these events replace: checks the session against the user's
            // security stamp every SecurityStampValidationInterval, so Logout ends copies of the cookie.
            OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync,
            OnRedirectToLogin = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = ctx =>
            {
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
/// same-site) is refused. SameSite=Lax still sends the cookie on a top-level navigation, so a link elsewhere must
/// not be able to act as the user (and no action that changes something is a GET).</item>
/// <item>A change from a browser without Fetch Metadata whose Origin header names another host is refused.</item>
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

        if (IsUnsafeFromAnotherOriginWithoutFetchMetadata(context.HttpContext))
        {
            logger.LogInformation("Refused {Method} {Path} from another origin", context.HttpContext.Request.Method, context.HttpContext.Request.Path);
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

    /// <summary>GET, HEAD, OPTIONS and TRACE: requests that should change nothing.</summary>
    public static bool IsSafeMethod(string method)
        => HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);

    /// <summary>
    /// A change on a cookie session, from a browser that sent no Fetch Metadata, whose Origin is another host.
    /// Antiforgery already refuses most of these; this also covers the actions exempt from it (Logout).
    /// </summary>
    public static bool IsUnsafeFromAnotherOriginWithoutFetchMetadata(HttpContext context)
        => !IsSafeMethod(context.Request.Method)
            && IsCookieSession(context)
            && string.IsNullOrEmpty(context.Request.Headers["Sec-Fetch-Site"].ToString())
            && BrowserAuth.IsFromAnotherOrigin(context.Request);

    /// <summary>Whether this request must carry an antiforgery token (see the class).</summary>
    public static bool RequiresToken(HttpContext context, IEnumerable<object> endpointMetadata)
    {
        if (IsSafeMethod(context.Request.Method))
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
