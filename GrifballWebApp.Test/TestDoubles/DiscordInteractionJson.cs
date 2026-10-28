using System.Text.Json;
using System.Text.Json.Serialization;
using NetCord.JsonModels;

namespace GrifballWebApp.Test;

/// <summary>
/// NetCord interaction models read from Discord's JSON, as the gateway delivers them, so tests get fully filled-in models
/// (empty arrays included) without reflecting into NetCord. Application 2, token "tok", text channel 6.
/// </summary>
internal static class DiscordInteractionJson
{
    private static readonly JsonSerializerOptions Options = new() { NumberHandling = JsonNumberHandling.AllowReadingFromString };

    public static JsonInteraction Parse(string json) => JsonSerializer.Deserialize<JsonInteraction>(json, Options)!;

    /// <summary>A click on button <paramref name="customId"/> of bot message 5 (author 7).</summary>
    public static JsonInteraction Button(string customId, ulong userId, string username = "u") => Parse($$"""
        {"id":"1","application_id":"2","type":3,"data":{"custom_id":"{{customId}}","component_type":2},
         "channel_id":"6","channel":{"id":"6","type":0},
         "user":{"id":"{{userId}}","username":"{{username}}","discriminator":"0"},"token":"tok","version":1,
         "message":{"id":"5","channel_id":"6","author":{"id":"7","username":"bot","discriminator":"0"},"content":"","timestamp":"2024-01-01T00:00:00+00:00","tts":false,"mention_everyone":false,"mentions":[],"mention_roles":[],"attachments":[],"embeds":[],"pinned":false,"type":0},
         "locale":"en-US","entitlements":[],"authorizing_integration_owners":{},"context":1}
        """);

    /// <summary>Slash command <paramref name="name"/> (command id 9) with no options.</summary>
    public static JsonInteraction SlashCommand(string name, ulong userId) => Parse($$"""
        {"id":"1","application_id":"2","type":2,"data":{"id":"9","name":"{{name}}","type":1,"options":[]},
         "channel_id":"6","channel":{"id":"6","type":0},
         "user":{"id":"{{userId}}","username":"u","discriminator":"0"},"token":"tok","version":1,
         "locale":"en-US","entitlements":[],"authorizing_integration_owners":{},"context":1}
        """);
}
