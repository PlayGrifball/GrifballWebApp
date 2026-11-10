using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Server;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetCord.Rest;
using NSubstitute;
using System.Text.Json;
using GrifballWebApp.Infrastructure.Ollama;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The routing in HandleAsync, that a failure inside HandleResponse is logged rather than thrown, and
/// the audited writes HandleResponse makes before it asks Ollama for a reply. Ollama is pointed at a
/// port nothing listens on, so the reply itself is never produced.
/// </summary>
[TestFixture]
public class MessageCreateHandlerTests : MessageCreateHandlerFixture
{
    // Registered like Program.cs: a scoped GrifballContext with the scope's AuditInterceptor and ICurrentUserService
    private GrifballContext? _db;

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
        return new MessageCreateHandler(_discordClient, config, services.GetRequiredService<IServiceScopeFactory>(), _ambient, _logger, new OllamaChatClients(config));
    }

    [Test]
    public async Task HandleAsync_LinkedAuthor_WritesAreAuditedAsTheirAppUser()
    {
        var handler = await HandlerWithDatabase();
        var user = Domain.Players.User.Seed(userName: "linked", discordUser: Domain.Players.DiscordUser.FromDiscord(5, "user5"));
        _db!.Users.Add(user);
        await _db.SaveChangesAsync();

        await handler.HandleAsync(Message(5, [BotId]));

        using var read = _db.NewContextLike();
        var saved = await read.DiscordMessages.SingleAsync(x => x.Id == 10);
        var bot = await read.DiscordUsers.SingleAsync(x => x.DiscordUserID == (long)BotId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.CreatedAt, Is.Not.Default);
            Assert.That(saved.ModifiedAt, Is.Not.Default);
            Assert.That(saved.CreatedByID, Is.EqualTo(user.Id));
            Assert.That(saved.ModifiedByID, Is.EqualTo(user.Id));
            Assert.That(bot.CreatedAt, Is.Not.Default);
            Assert.That(bot.CreatedByID, Is.EqualTo(user.Id));
            // Only Ollama failed, after the writes
            Assert.That(_logger.Entries.Single().Message, Does.Contain("Failed to handle message"));
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.CreatedAt, Is.Not.Default);
            Assert.That(saved.CreatedByID, Is.Null);
            Assert.That(saved.ModifiedByID, Is.Null);
            Assert.That(author.CreatedAt, Is.Not.Default);
            Assert.That(author.ModifiedAt, Is.Not.Default);
            Assert.That(author.CreatedByID, Is.Null);
        }
    }

    [Test]
    public async Task HandleAsync_StreamedReplyAlreadyShownInFull_IsStillSaved()
    {
        // The first chunk is long enough for a streamed update, which is sent once a second has passed with the second
        // chunk: that update already shows the whole reply, so the final step has nothing left to change.
        const string first = "This first chunk is over 25 characters";
        const string second = "!";
        using var ollama = FakeOllama.Start((TimeSpan.Zero, first), (TimeSpan.FromMilliseconds(1300), second));
        var handler = await HandlerWithDatabase(ollama.Url);
        var rest = new RestClient(new RestClientConfiguration { RequestHandler = new FakeRestRequestHandler { EchoContent = true } });

        await handler.HandleAsync(Message(5, [BotId], restClient: rest));

        Assert.That(_logger.Entries, Is.Empty, string.Join("\n", _logger.Entries.Select(x => x.Exception?.ToString() ?? x.Message)));
        using var read = _db!.NewContextLike();
        var reply = await read.DiscordMessages.SingleOrDefaultAsync(x => x.FromDiscordUserId == (long)BotId);
        Assert.That(reply?.Content, Is.EqualTo(first + second), "The bot's reply is kept for the conversation's history");
    }

    [Test]
    public async Task HandleAsync_StreamedReplyGrewAfterTheLastUpdate_IsEditedAndSaved()
    {
        // The streamed update shows the first two chunks; the third comes too soon after it for another, so the final step
        // edits the reply to the whole of it.
        const string first = "This first chunk is over 25 characters";
        using var ollama = FakeOllama.Start((TimeSpan.Zero, first), (TimeSpan.FromMilliseconds(1300), "!"), (TimeSpan.Zero, " The end."));
        var handler = await HandlerWithDatabase(ollama.Url);
        var discord = new FakeRestRequestHandler { EchoContent = true };
        var rest = new RestClient(new RestClientConfiguration { RequestHandler = discord });

        await handler.HandleAsync(Message(5, [BotId], restClient: rest));

        using var read = _db!.NewContextLike();
        var reply = await read.DiscordMessages.SingleOrDefaultAsync(x => x.FromDiscordUserId == (long)BotId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reply?.Content, Is.EqualTo(first + "! The end."));
            Assert.That(discord.Requests.Count(r => r.Method == HttpMethod.Patch), Is.EqualTo(1), "The reply was edited once, at the end");
        }
    }

    [Test]
    public async Task HandleAsync_StreamedReplyOverDiscordsLimit_IsSentAndSavedInMessagesOf2000Characters()
    {
        // The streamed update once a second has passed with the second chunk: its first 2000 characters go as a message of
        // their own (saved), the rest as the reply still being written, which the final step saves.
        var first = new string('a', 2100);
        using var ollama = FakeOllama.Start((TimeSpan.Zero, first), (TimeSpan.FromMilliseconds(1300), "!"));
        var handler = await HandlerWithDatabase(ollama.Url);
        var rest = new RestClient(new RestClientConfiguration { RequestHandler = new FakeRestRequestHandler { EchoContent = true, UniqueIds = true } });

        await handler.HandleAsync(Message(5, [BotId], restClient: rest));

        using var read = _db!.NewContextLike();
        var replies = await read.DiscordMessages.Where(x => x.FromDiscordUserId == (long)BotId).OrderBy(x => x.Id).Select(x => x.Content).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_logger.Entries, Is.Empty, string.Join("\n", _logger.Entries.Select(x => x.Exception?.ToString() ?? x.Message)));
            Assert.That(replies, Is.EqualTo(new[] { new string('a', 2000), new string('a', 100) + "!" }));
        }
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

        /// <summary>Streams each chunk after its delay.</summary>
        public static FakeOllama Start(params (TimeSpan Delay, string Text)[] chunks)
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
                    foreach (var (delay, text) in chunks)
                    {
                        await Task.Delay(delay);
                        await writer.WriteLineAsync(Chunk(text, done: false));
                        await writer.FlushAsync();
                    }
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
