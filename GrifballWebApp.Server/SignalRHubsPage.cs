namespace GrifballWebApp.Server;

/// <summary>
/// The hubs' page (TypedSignalR.Client.DevTools, what /swagger is to the API): their methods and receivers, to call and
/// watch. Served in every environment, as Swagger is, but to sysadmins only: it is a third-party page on the site's own
/// origin, where the session cookie works, so a flaw in it would act as whoever loaded it. A call made from it is that
/// sysadmin's (the cookie, or a bearer token pasted into it).
/// </summary>
public static class SignalRHubsPage
{
    /// <summary>The page and its specification (spec.json), on the backend and as the browser reaches them.</summary>
    public const string Path = "/signalr-dev";

    private const string Sysadmin = "Sysadmin";

    /// <summary>
    /// A request with no authenticated identity is refused (401); a signed-in user who is not a sysadmin is refused
    /// (403). Neither is redirected to the site's sign-in: it is a page of the backend's, not the site's own pages, and
    /// Program.cs's UseStatusCodePages fills the response body (as <see cref="Identity.BrowserAuth.RefuseForeignHubRequests"/>
    /// leaves it to for a hub request).
    /// </summary>
    public static Task RequireSysadmin(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments(Path) || context.User.IsInRole(Sysadmin))
            return next(context);

        context.Response.StatusCode = context.User.Identity?.IsAuthenticated == true
            ? StatusCodes.Status403Forbidden
            : StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
