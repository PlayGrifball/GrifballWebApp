using System.Text.Json;
using System.Text.Json.Serialization;
using NetCord.JsonModels;

namespace GrifballWebApp.Test;

/// <summary>
/// NetCord interaction models read from Discord's JSON, as the gateway delivers them, so tests get fully filled-in models
/// (empty arrays included) without reflecting into NetCord. Interaction 1, application 2, token "tok", text channel 6.
/// </summary>
internal static class DiscordInteractionJson
{
    private static readonly JsonSerializerOptions Options = new() { NumberHandling = JsonNumberHandling.AllowReadingFromString };

    public static JsonInteraction Parse(string json) => JsonSerializer.Deserialize<JsonInteraction>(json, Options)!;

    /// <summary>A click on button <paramref name="customId"/> of bot message 5 (author 7).</summary>
    public static JsonInteraction Button(string customId, ulong userId, string username = "u") =>
        Parse(Envelope(3, $$"""{"custom_id":"{{customId}}","component_type":2}""", userId, username, OnBotMessage));

    /// <summary>A choice of <paramref name="values"/> in string menu <paramref name="customId"/> of bot message 5 (author 7).</summary>
    public static JsonInteraction StringMenu(string customId, ulong userId, params string[] values) =>
        Parse(Envelope(3, $$"""{"custom_id":"{{customId}}","component_type":3,"values":{{JsonSerializer.Serialize(values)}}}""", userId, "u", OnBotMessage));

    /// <summary>
    /// A submit of modal <paramref name="customId"/> with one text input, <paramref name="inputId"/> = <paramref name="value"/>,
    /// wrapped in a Label as Discord sends it.
    /// </summary>
    public static JsonInteraction Modal(string customId, string inputId, string value, ulong userId, string username = "u") =>
        Parse(Envelope(5, $$$"""
            {"custom_id":"{{{customId}}}","components":[{"type":18,"id":1,"component":{"type":4,"id":2,"custom_id":"{{{inputId}}}","value":{{{JsonSerializer.Serialize(value)}}}}}]}
            """, userId, username));

    /// <summary>Slash command <paramref name="name"/> (command id 9) with no options.</summary>
    public static JsonInteraction SlashCommand(string name, ulong userId) => SlashCommand(name, userId, "u");

    /// <summary>Slash command <paramref name="name"/> (command id 9) with string <paramref name="options"/>.</summary>
    public static JsonInteraction SlashCommand(string name, ulong userId, string username, params (string Name, string Value)[] options) =>
        Parse(Envelope(2, $$"""{"id":"9","name":"{{name}}","type":1,"options":{{StringOptions(options, focused: null)}}}""", userId, username));

    /// <summary>
    /// Autocomplete for string option <paramref name="option"/> of slash command <paramref name="name"/> (command id 9),
    /// with <paramref name="typed"/> typed so far.
    /// </summary>
    public static JsonInteraction Autocomplete(string name, string option, string typed, ulong userId) =>
        Parse(Envelope(4, $$"""{"id":"9","name":"{{name}}","type":1,"options":{{StringOptions([(option, typed)], focused: option)}}}""", userId, "u"));

    private const string OnBotMessage = """
        "message":{"id":"5","channel_id":"6","author":{"id":"7","username":"bot","discriminator":"0"},"content":"","timestamp":"2024-01-01T00:00:00+00:00","tts":false,"mention_everyone":false,"mentions":[],"mention_roles":[],"attachments":[],"embeds":[],"pinned":false,"type":0},
        """;

    private static string StringOptions((string Name, string Value)[] options, string? focused) =>
        JsonSerializer.Serialize(options.Select(o => o.Name == focused
            ? (object)new { name = o.Name, type = 3, value = o.Value, focused = true }
            : new { name = o.Name, type = 3, value = o.Value }));

    /// <summary>An interaction of <paramref name="type"/> (2 command, 3 component, 4 autocomplete, 5 modal) in text channel 6.</summary>
    private static string Envelope(int type, string data, ulong userId, string username, string message = "") => $$"""
        {"id":"1","application_id":"2","type":{{type}},"data":{{data}},
         "channel_id":"6","channel":{"id":"6","type":0},
         "user":{"id":"{{userId}}","username":{{JsonSerializer.Serialize(username)}},"discriminator":"0"},"token":"tok","version":1,
         {{message}}
         "locale":"en-US","entitlements":[],"authorizing_integration_owners":{},"context":1}
        """;
}
