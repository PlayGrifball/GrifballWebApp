using Microsoft.AspNetCore.Mvc;

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
    /// A visitor is sent to the site's sign-in (not back here after it: the site follows a follow-up within its own pages,
    /// and this one is the backend's); a signed-in user who is not a sysadmin is refused (403).
    /// </summary>
    public static Task RequireSysadmin(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments(Path) || context.User.IsInRole(Sysadmin))
            return next(context);

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.Redirect("/login");
            return Task.CompletedTask;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Sysadmins only",
            Detail = "The hubs' page is for sysadmins.",
        }, options: null, contentType: "application/problem+json", context.RequestAborted);
    }
}
