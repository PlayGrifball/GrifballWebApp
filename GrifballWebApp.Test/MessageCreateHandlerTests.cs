using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using NSubstitute;
using System.Text.Json;

namespace GrifballWebApp.Test;

/// <summary>
/// Only the routing in HandleAsync, and that a failure inside HandleResponse is logged rather than
/// thrown, is tested. HandleResponse otherwise talks to a local Ollama instance and is deliberately
/// never reached.
/// </summary>
[TestFixture]
public class MessageCreateHandlerTests
{
    private const ulong BotId = 1;

    private IDiscordRestClient _discordClient;
    private IDbContextFactory<GrifballContext> _contextFactory;
    private IServiceScopeFactory _scopeFactory;
    private RecordingLogger<MessageCreateHandler> _logger;
    private MessageCreateHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _discordClient = Substitute.For<IDiscordRestClient>();
        _contextFactory = Substitute.For<IDbContextFactory<GrifballContext>>();
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _logger = new RecordingLogger<MessageCreateHandler>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Discord:ClientId"] = BotId.ToString() }).Build();
        _handler = new MessageCreateHandler(_discordClient, _contextFactory, config, _scopeFactory, _logger);
    }

    private static NetCord.Gateway.Message Message(ulong authorId, ulong[]? mentions = null, (ulong ChannelId, ulong MessageId)? reference = null)
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
        return new NetCord.Gateway.Message(model, null, null, new RestClient());
    }

    private void AssertNoResponse()
    {
        _contextFactory.DidNotReceiveWithAnyArgs().CreateDbContextAsync(default);
        Assert.That(_contextFactory.ReceivedCalls(), Is.Empty, "HandleResponse would have created a context");
    }

    [Test]
    public void Message_IsBuiltFromJsonModel()
    {
        var message = Message(5, [7], (40, 30));

        Assert.Multiple(() =>
        {
            Assert.That(message.Author.Id, Is.EqualTo(5ul));
            Assert.That(message.MentionedUsers.Select(x => x.Id), Is.EqualTo(new[] { 7ul }));
            Assert.That(message.MessageReference!.ChannelId, Is.EqualTo(40ul));
            Assert.That(message.MessageReference!.MessageId, Is.EqualTo(30ul));
        });
    }

    [Test]
    public async Task HandleAsync_IgnoresBotsOwnMessages_EvenIfTheyMentionTheBot()
    {
        await _handler.HandleAsync(Message(BotId, [BotId], (40, 30)));

        Assert.That(_discordClient.ReceivedCalls(), Is.Empty);
        AssertNoResponse();
    }

    [Test]
    public async Task HandleAsync_PlainMessageNotMentioningBot_DoesNothing()
    {
        await _handler.HandleAsync(Message(5, [6]));

        Assert.That(_discordClient.ReceivedCalls(), Is.Empty);
        AssertNoResponse();
    }

    [Test]
    public async Task HandleAsync_ReplyToNonBotMessage_DoesNothing()
    {
        var referenced = Substitute.For<IDiscordRestMessage>();
        referenced.Author.Id.Returns(99ul);
        _discordClient.GetMessageAsync(40ul, 30ul, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(referenced);

        await _handler.HandleAsync(Message(5, reference: (40, 30)));

        await _discordClient.Received(1).GetMessageAsync(40ul, 30ul, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        AssertNoResponse();
    }

    [Test]
    public async Task HandleAsync_ReplyWhereReferencedMessageIsMissing_DoesNothing()
    {
        _discordClient.GetMessageAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns((IDiscordRestMessage)null!);

        await _handler.HandleAsync(Message(5, reference: (40, 30)));

        await _discordClient.Received(1).GetMessageAsync(40ul, 30ul, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        AssertNoResponse();
    }

    [Test]
    public void RecentMatchesUrl_EscapesGamertag()
    {
        var id = Guid.Parse("0b7e8f4e-58b0-4b4d-9a52-0d1c8f6e5b11");

        var url = _handler.RecentMatchesUrl("Grunt Padre", id);

        Assert.That(url, Is.EqualTo($"https://www.halowaypoint.com/halo-infinite/players/Grunt%20Padre/matches/{id}"));
    }

    [Test]
    public async Task HandleAsync_WhenHandlingTheMessageFails_LogsInsteadOfThrowing()
    {
        // _contextFactory is an unconfigured substitute, so CreateDbContextAsync completes with a null
        // GrifballContext: HandleResponse's first real use of it (context.DiscordUsers) throws a
        // NullReferenceException, standing in for any failure (a down Ollama, a DB error, ...).
        var message = Message(5, [BotId]);

        await _handler.HandleAsync(message);

        var entry = _logger.Entries.Single();
        Assert.Multiple(() =>
        {
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Exception, Is.TypeOf<NullReferenceException>());
            Assert.That(entry.Message, Does.Contain("Failed to handle message").And.Contain(message.Author.Id.ToString()));
        });
    }
}
