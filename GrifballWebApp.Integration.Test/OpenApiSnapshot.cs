using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GrifballWebApp.Integration.Test;

/// <summary>The committed OpenAPI document the client is generated from, and the served one in the same form.</summary>
public static class OpenApiSnapshot
{
    // Only what JSON itself needs escaped (not ', + or `), so the file reads as text and as JavaScript writes it
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// The served document with one server, the API's path on whichever host serves the page (its "servers" name the
    /// request's own host first): the generated client's root URL. Indented with two spaces, with a final newline.
    /// </summary>
    public static string Normalize(string document)
    {
        var root = JsonNode.Parse(document)!.AsObject();
        root["servers"] = new JsonArray(new JsonObject { ["url"] = "/api", ["description"] = "The page's own host" });
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
