using NetCord.Rest;
using System.Text.Json;

namespace GrifballWebApp.Server.Test;

/// <summary>A gateway message as Discord sends it (its JSON model), for MessageCreateHandler's tests.</summary>
internal static class GatewayMessages
{
    /// <summary>Message 10 in channel 20 of guild 30 (a DM, in no guild, when <paramref name="direct"/>) from <paramref name="authorId"/>, saying "hello",
    /// mentioning <paramref name="mentions"/>; a reply to <paramref name="reference"/> when given. Its replies go through <paramref name="restClient"/>.</summary>
    internal static NetCord.Gateway.Message Build(ulong authorId, ulong[]? mentions = null, (ulong ChannelId, ulong MessageId)? reference = null, RestClient? restClient = null,
        bool direct = false)
    {
        var json = new Dictionary<string, object?>
        {
            ["id"] = 10,
            ["channel_id"] = 20,
            ["author"] = new { id = authorId, username = $"user{authorId}" },
            ["content"] = "hello",
            ["timestamp"] = "2024-01-01T00:00:00+00:00",
            ["mentions"] = (mentions ?? []).Select(id => new { id, username = $"user{id}" }).ToArray(),
            ["mention_roles"] = Array.Empty<ulong>(),
            ["mention_channels"] = Array.Empty<object>(),
            ["attachments"] = Array.Empty<object>(),
            ["embeds"] = Array.Empty<object>(),
            ["reactions"] = Array.Empty<object>(),
            ["components"] = Array.Empty<object>(),
            ["sticker_items"] = Array.Empty<object>(),
            ["stickers"] = Array.Empty<object>(),
            ["message_snapshots"] = Array.Empty<object>(),
        };
        if (!direct)
            json["guild_id"] = 30;
        if (reference is { } r)
            json["message_reference"] = new { channel_id = r.ChannelId, message_id = r.MessageId };
        var model = JsonSerializer.Deserialize<NetCord.JsonModels.JsonMessage>(JsonSerializer.Serialize(json))!;
        return new NetCord.Gateway.Message(model, null, null, restClient ?? new RestClient());
    }
}
