using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Interceptors;
using GrifballWebApp.Database.Services;
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
/// The routing in HandleAsync, that a failure inside HandleResponse is logged rather than thrown, and
/// the audited writes HandleResponse makes before it asks Ollama for a reply. Ollama is pointed at a
/// port nothing listens on, so the reply itself is never produced.
/// </summary>
[TestFixture]
public class MessageCreateHandlerTests
{
    private const ulong BotId = 1;

    private IDiscordRestClient _discordClient;
    private IServiceScopeFactory _scopeFactory;
    private RecordingLogger<MessageCreateHandler> _logger;
    private MessageCreateHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _discordClient = Substitute.For<IDiscordRestClient>();
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _logger = new RecordingLogger<MessageCreateHandler>();
        _ambient = Substitute.For<IAmbientCurrentUser>();
        _ambient.IsActive.Returns(true);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Discord:ClientId"] = BotId.ToString() }).Build();
        _handler = new MessageCreateHandler(_discordClient, config, _scopeFactory, new AmbientCurrentUser(), _logger);
    }

    private static NetCord.Gateway.Message Message(ulong authorId, ulong[]? mentions = null, (ulong ChannelId, ulong MessageId)? reference = null, RestClient? restClient = null)
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

    private void AssertNoResponse()
    {
        Assert.That(_scopeFactory.ReceivedCalls(), Is.Empty, "HandleResponse would have created a scope");
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
        // Stands in for any failure (a down Ollama, a DB error, ...)
        _scopeFactory.CreateScope().Returns(_ => throw new InvalidOperationException("boom"));
        var message = Message(5, [BotId]);

        await _handler.HandleAsync(message);

        var entry = _logger.Entries.Single();
        Assert.Multiple(() =>
        {
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(entry.Message, Does.Contain("Failed to handle message").And.Contain(message.Author.Id.ToString()));
        });
    }

    // Registered like Program.cs: a scoped GrifballContext with the scope's AuditInterceptor and ICurrentUserService
    private GrifballContext? _db;
    private IAmbientCurrentUser _ambient = null!;

    [TearDown]
    public async Task TearDown()
    {
        // One fixture instance runs every test, and only some use a database
        if (_db is not null)
            await _db.DropDatabaseAndDispose();
        _db = null;
    }

    private async Task<MessageCreateHandler> HandlerWithDatabase(string ollamaUrl = "http://127.0.0.1:9/")
    {
        _db = await SetUpFixture.NewGrifballContext();
        var connectionString = _db.Database.GetConnectionString()!;
        var services = new ServiceCollection()
            .AddHttpContextAccessor()
            .AddSingleton(_ambient)
            .AddScoped<ICurrentUserService, CurrentUserService>()
            .AddScoped<AuditInterceptor>()
            .AddDbContext<GrifballContext>((sp, options) => options
                .UseGrifballDatabase(TestDatabase.Provider, connectionString)
                .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()))
            .BuildServiceProvider();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Discord:ClientId"] = BotId.ToString(),
            ["Ollama:Url"] = ollamaUrl, // by default refused at once
        }).Build();
        return new MessageCreateHandler(_discordClient, config, services.GetRequiredService<IServiceScopeFactory>(), _ambient, _logger);
    }

    [Test]
    public async Task HandleAsync_LinkedAuthor_WritesAreAuditedAsTheirAppUser()
    {
        var handler = await HandlerWithDatabase();
        var user = new Database.Models.User { UserName = "linked", DiscordUser = new Database.Models.DiscordUser { DiscordUserID = 5, DiscordUsername = "user5" } };
        _db!.Users.Add(user);
        await _db.SaveChangesAsync();

        await handler.HandleAsync(Message(5, [BotId]));

        using var read = _db.NewContextLike();
        var saved = await read.DiscordMessages.SingleAsync(x => x.Id == 10);
        var bot = await read.DiscordUsers.SingleAsync(x => x.DiscordUserID == (long)BotId);
        Assert.Multiple(() =>
        {
            Assert.That(saved.CreatedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(saved.ModifiedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(saved.CreatedByID, Is.EqualTo(user.Id));
            Assert.That(saved.ModifiedByID, Is.EqualTo(user.Id));
            Assert.That(bot.CreatedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(bot.CreatedByID, Is.EqualTo(user.Id));
            // Only Ollama failed, after the writes
            Assert.That(_logger.Entries.Single().Message, Does.Contain("Failed to handle message"));
        });
        // The author is also the ambient user while the message is handled, and only then
        Received.InOrder(() =>
        {
            _ambient.Begin();
            _ambient.UserId = user.Id;
            _ambient.End();
        });
    }

    [Test]
    public async Task HandleAsync_UnlinkedAuthor_WritesAreTimestampedWithNoUser()
    {
        var handler = await HandlerWithDatabase();

        await handler.HandleAsync(Message(5, [BotId]));

        using var read = _db!.NewContextLike();
        var saved = await read.DiscordMessages.SingleAsync(x => x.Id == 10);
        var author = await read.DiscordUsers.SingleAsync(x => x.DiscordUserID == 5);
        Assert.Multiple(() =>
        {
            Assert.That(saved.CreatedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(saved.CreatedByID, Is.Null);
            Assert.That(saved.ModifiedByID, Is.Null);
            Assert.That(author.CreatedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(author.ModifiedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(author.CreatedByID, Is.Null);
        });
    }

    [Test]
    public async Task HandleAsync_StreamedReplyAlreadyShownInFull_IsStillSaved()
    {
        // The first chunk is long enough for a streamed update, which is sent once a second has passed with the second
        // chunk: that update already shows the whole reply, so the final step has nothing left to change.
        const string first = "This first chunk is over 25 characters";
        const string second = "!";
        using var ollama = FakeOllama.Start(first, second, delayBeforeSecond: TimeSpan.FromMilliseconds(1300));
        var handler = await HandlerWithDatabase(ollama.Url);
        var rest = new RestClient(new RestClientConfiguration { RequestHandler = new FakeRestRequestHandler { EchoContent = true } });

        await handler.HandleAsync(Message(5, [BotId], restClient: rest));

        Assert.That(_logger.Entries, Is.Empty, string.Join("\n", _logger.Entries.Select(x => x.Exception?.ToString() ?? x.Message)));
        using var read = _db!.NewContextLike();
        var reply = await read.DiscordMessages.SingleOrDefaultAsync(x => x.FromDiscordUserId == (long)BotId);
        Assert.That(reply?.Content, Is.EqualTo(first + second), "The bot's reply is kept for the conversation's history");
    }

    /// <summary>Answers Ollama's /api/chat with a streamed reply in two chunks.</summary>
    private sealed class FakeOllama : IDisposable
    {
        private readonly System.Net.HttpListener _listener = new();
        public string Url { get; }

        private FakeOllama(string url)
        {
            Url = url;
            _listener.Prefixes.Add(url);
        }

        public static FakeOllama Start(string first, string second, TimeSpan delayBeforeSecond)
        {
            var port = FreePort();
            var fake = new FakeOllama($"http://127.0.0.1:{port}/");
            fake._listener.Start();
            _ = Task.Run(async () =>
            {
                while (fake._listener.IsListening)
                {
                    System.Net.HttpListenerContext ctx;
                    try { ctx = await fake._listener.GetContextAsync(); }
                    catch { return; }
                    ctx.Response.ContentType = "application/x-ndjson";
                    ctx.Response.SendChunked = true;
                    await using var writer = new StreamWriter(ctx.Response.OutputStream);
                    await writer.WriteLineAsync(Chunk(first, done: false));
                    await writer.FlushAsync();
                    await Task.Delay(delayBeforeSecond);
                    await writer.WriteLineAsync(Chunk(second, done: false));
                    await writer.WriteLineAsync(Chunk("", done: true));
                    await writer.FlushAsync();
                }
            });
            return fake;
        }

        private static string Chunk(string content, bool done) => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["model"] = "llama3.1:latest",
            ["created_at"] = "2024-01-01T00:00:00Z",
            ["message"] = new { role = "assistant", content },
            ["done"] = done,
            ["done_reason"] = done ? "stop" : null,
        });

        private static int FreePort()
        {
            var socket = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            socket.Start();
            var port = ((System.Net.IPEndPoint)socket.LocalEndpoint).Port;
            socket.Stop();
            return port;
        }

        public void Dispose() => _listener.Close();
    }
}
