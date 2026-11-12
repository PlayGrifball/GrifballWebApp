using Microsoft.OpenApi;

namespace GrifballWebApp.Server;

/// <summary>One of "OpenApi:Servers" in configuration: an environment's full URL (with its /api suffix) and a human
/// description, offered in the OpenAPI document's "servers" alongside the one the document was requested from.</summary>
public sealed class OpenApiServerEntry
{
    public required string Url { get; set; }
    public required string Description { get; set; }
}

/// <summary>"OpenApi:Servers" in configuration: every environment this API runs on, for Postman and client generators
/// (a browser only ever needs the one it is talking to, <see cref="OpenApiServers.Build"/>'s first entry).</summary>
public sealed class OpenApiServersOptions
{
    public List<OpenApiServerEntry> Servers { get; set; } = [];
}

/// <summary>Builds the OpenAPI document's "servers" list (Program.cs, AddSwaggerGen's PreSerializeFilters).</summary>
public static class OpenApiServers
{
    /// <summary>
    /// First the server this document was requested from (so "Try it out" and a browser's own calls default to where
    /// they already are), then every configured environment that isn't that one (Postman and client generators can
    /// switch between them), then in Development only, the backend itself with no proxy in front.
    /// </summary>
    /// <param name="requestUrl">The request's own scheme, host and the API's path prefix, e.g. "https://grifball.xyz/api".</param>
    /// <param name="configured">"OpenApi:Servers" from configuration.</param>
    public static List<OpenApiServer> Build(string requestUrl, IReadOnlyList<OpenApiServerEntry> configured, bool isDevelopment)
    {
        var servers = new List<OpenApiServer> { new() { Url = requestUrl, Description = "This environment" } };

        foreach (var entry in configured)
        {
            if (!SameUrl(entry.Url, requestUrl))
                servers.Add(new OpenApiServer { Url = entry.Url, Description = entry.Description });
        }

        if (isDevelopment)
            servers.Add(new OpenApiServer { Url = "/", Description = "The backend itself, with no proxy in front (development)" });

        return servers;
    }

    private static bool SameUrl(string a, string b)
        => string.Equals(a.TrimEnd('/'), b.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
}
