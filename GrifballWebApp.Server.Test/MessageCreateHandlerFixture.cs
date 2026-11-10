using DiscordInterface.Generated;
using GrifballWebApp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetCord.Rest;
using NSubstitute;
using System.Text.Json;
using GrifballWebApp.Infrastructure.Ollama;

namespace GrifballWebApp.Server.Test;

/// <summary>The state and helpers MessageCreateHandlerTests's unit and integration tests share.</summary>
public abstract class MessageCreateHandlerFixture
{
    protected static readonly ulong[] MentionedSeven = [7ul];

    protected const ulong BotId = 1;

    protected IDiscordRestClient _discordClient = null!;
    protected IServiceScopeFactory _scopeFactory = null!;
    protected RecordingLogger<MessageCreateHandler> _logger = null!;
    protected MessageCreateHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _discordClient = Substitute.For<IDiscordRestClient>();
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _logger = new RecordingLogger<MessageCreateHandler>();
        _ambient = Substitute.For<IAmbientCurrentUser>();
        _ambient.IsActive.Returns(true);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Discord:ClientId"] = BotId.ToString() }).Build();
        _handler = new MessageCreateHandler(_discordClient, config, _scopeFactory, new AmbientCurrentUser(), _logger, new OllamaChatClients(config));
    }

    protected static NetCord.Gateway.Message Message(ulong authorId, ulong[]? mentions = null, (ulong ChannelId, ulong MessageId)? reference = null, RestClient? restClient = null)
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
        if (reference is { } r)
            json["message_reference"] = new { channel_id = r.ChannelId, message_id = r.MessageId };
        var model = JsonSerializer.Deserialize<NetCord.JsonModels.JsonMessage>(JsonSerializer.Serialize(json))!;
        return new NetCord.Gateway.Message(model, null, null, restClient ?? new RestClient());
    }

    protected void AssertNoResponse()
    {
        Assert.That(_scopeFactory.ReceivedCalls(), Is.Empty, "HandleResponse would have created a scope");
    }
    protected IAmbientCurrentUser _ambient = null!;
}
