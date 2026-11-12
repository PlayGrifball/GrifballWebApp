using System.Text.Json;
using System.Text.Json.Nodes;

namespace GrifballWebApp.Integration.Test;

/// <summary>The committed OpenAPI document the client is generated from, and the served one in the same form.</summary>
public static class OpenApiSnapshot
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>
    /// The served document without its "servers" (the request's own host first), indented with two spaces, with a
    /// final newline.
    /// </summary>
    public static string Normalize(string document)
    {
        var root = JsonNode.Parse(document)!.AsObject();
        root.Remove("servers");
        return root.ToJsonString(Indented).ReplaceLineEndings("\n") + "\n";
    }

    /// <summary>grifballwebapp.client/openapi.json in the repository this test was built from.</summary>
    public static string CommittedPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GrifballWebApp.slnx")))
            directory = directory.Parent;
        if (directory is null)
            throw new InvalidOperationException($"No GrifballWebApp.slnx above {AppContext.BaseDirectory}");
        return Path.Combine(directory.FullName, "grifballwebapp.client", "openapi.json");
    }
}
