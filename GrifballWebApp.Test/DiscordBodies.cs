using System.Text.Json;

namespace GrifballWebApp.Test;

/// <summary>What a Discord message's body (as the fake REST handler saw it) holds: its text and its string menus.</summary>
internal static class DiscordBodies
{
    internal static List<JsonElement> StringMenus(JsonElement body)
    {
        var result = new List<JsonElement>();
        void Walk(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Object)
            {
                if (e.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.Number && t.GetInt32() == 3)
                {
                    result.Add(e);
                    return;
                }
                foreach (var p in e.EnumerateObject()) Walk(p.Value);
            }
            else if (e.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in e.EnumerateArray()) Walk(x);
            }
        }
        if (body.TryGetProperty("components", out var comps)) Walk(comps);
        return result;
    }

    internal static string? Content(JsonElement body) =>
        body.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
}
