namespace GrifballWebApp.Server;

/// <summary>
/// The hubs' page (TypedSignalR.Client.DevTools, what /swagger is to the API): their methods and receivers, to call and
/// watch. Served in every environment, as Swagger is, to signed-in users only; a visitor is sent to sign in.
/// </summary>
public static class SignalRHubsPage
{
    /// <summary>The page and its specification (spec.json), on the backend and as the browser reaches them.</summary>
    public const string Path = "/signalr-dev";

    /// <summary>
    /// Sends a visitor asking for the page to the site's sign-in. Not back here after it: the site follows a follow-up
    /// within its own pages, and this one is the backend's.
    /// </summary>
    public static Task RequireSignIn(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments(Path) || context.User.Identity?.IsAuthenticated == true)
            return next(context);

        context.Response.Redirect("/login");
        return Task.CompletedTask;
    }
}
