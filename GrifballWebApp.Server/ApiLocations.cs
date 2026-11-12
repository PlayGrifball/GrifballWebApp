namespace GrifballWebApp.Server;

/// <summary>
/// The Location of a 201 or 202 as the browser reaches it. The API is served under <see cref="PublicPrefix"/> (the
/// Angular server, src/server.ts, and the dev proxy, proxy.conf.js, strip it before the request gets here), so the
/// links CreatedAtAction and AcceptedAtAction make from the app's own routes ("https://host/seasons/12/settings") get
/// it back. Only those, and only to this host: a redirect (a sign-in's challenge, /signin-discord's) is left alone.
/// </summary>
public static class ApiLocations
{
    /// <summary>Where the API is mounted for its callers.</summary>
    public const string PublicPrefix = "/api";

    /// <summary>The middleware: prefixes the Location of each created or accepted answer when it starts.</summary>
    public static Task UsePublicPrefix(HttpContext context, RequestDelegate next)
    {
        context.Response.OnStarting(static state =>
        {
            Prefix((HttpContext)state);
            return Task.CompletedTask;
        }, context);
        return next(context);
    }

    /// <summary>A 201's or 202's Location on this host (or app-relative), under <see cref="PublicPrefix"/>.</summary>
    public static void Prefix(HttpContext context)
    {
        var response = context.Response;
        if (response.StatusCode is not (StatusCodes.Status201Created or StatusCodes.Status202Accepted))
            return;
        var location = response.Headers.Location.ToString();
        if (!Uri.TryCreate(location, UriKind.RelativeOrAbsolute, out var uri) || location.Length == 0)
            return;

        if (uri.IsAbsoluteUri)
        {
            if (!IsThisHost(uri, context.Request) || UnderPrefix(uri.AbsolutePath))
                return;
            // The escaped forms throughout: a percent-encoded segment stays encoded
            response.Headers.Location = uri.GetLeftPart(UriPartial.Authority) + PublicPrefix + uri.PathAndQuery + uri.Fragment;
        }
        else if (location[0] == '/' && !location.StartsWith("//", StringComparison.Ordinal) && !UnderPrefix(location))
        {
            response.Headers.Location = PublicPrefix + location;
        }
    }

    /// <summary>The same host and port as the request's, a default port written or not ("host:443" is "host" over https).</summary>
    private static bool IsThisHost(Uri uri, HttpRequest request)
    {
        var host = request.Host;
        var port = host.Port ?? (string.Equals(request.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80);
        return string.Equals(uri.Host, host.Host, StringComparison.OrdinalIgnoreCase) && uri.Port == port;
    }

    private static bool UnderPrefix(string path)
        => path.Equals(PublicPrefix, StringComparison.OrdinalIgnoreCase) || path.StartsWith(PublicPrefix + "/", StringComparison.OrdinalIgnoreCase);
}
