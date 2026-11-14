using DiscordInterface.Generated;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using System.Text.Json;
using DiscordUser = GrifballWebApp.Domain.Players.DiscordUser;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// MessageCreateHandler answering a message, on a temp-file SQLite GrifballContext with a scripted chat model in place of
/// Ollama (<see cref="ScriptedChatClient"/>) and a fake Discord (<see cref="FakeRestRequestHandler"/>): the conversation it
/// sends the model, the reply it shows (in messages of 2000 characters at most, edited as it streams in), what it saves,
/// and the tools it gives the model. No host, network or Ollama.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MessageCreateHandlerConversationUnitTests : SqliteGrifballContextFixture
{
    private const ulong BotId = 1;
    private const ulong AuthorId = 5;
    private static readonly ulong[] MentionsTheBot = [BotId];
    private static readonly string[] HiThere = ["Hi there"];
    private static readonly string[] ShownInFull = ["This first chunk is over 25 characters!"];

    private readonly RecordingLogger<MessageCreateHandler> _logger = new();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IAmbientCurrentUser _ambient = Substitute.For<IAmbientCurrentUser>();
    private readonly IGetsertXboxUserService _xboxUsers = Substitute.For<IGetsertXboxUserService>();
    private readonly IDataPullService _dataPull = Substitute.For<IDataPullService>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeRestRequestHandler _discord = new() { EchoContent = true, UniqueIds = true };
    private ServiceProvider _services = null!;

    [TearDown]
    public async Task TearDownServices()
    {
        _discord.Dispose();
        if (_services is not null)
            await _services.DisposeAsync();
    }

    /// <summary>The handler, its scopes giving the test's database (as Program.cs's scoped GrifballContext would) and
    /// <paramref name="model"/> as the chat model.</summary>
    private MessageCreateHandler Handler(IChatClient model)
    {
        _ambient.IsActive.Returns(true);
        _services = new ServiceCollection()
            .AddSingleton<GrifballContext>(Db)
            .AddSingleton<IApplicationDbContext>(Db)
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(_currentUser)
            .AddSingleton(_ambient)
            .AddSingleton(_xboxUsers)
            .AddSingleton(_dataPull)
            .BuildServiceProvider();
        var chatClients = Substitute.For<IBotChatClients>();
        chatClients.Create().Returns(model);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Discord:ClientId"] = BotId.ToString() }).Build();
        return new MessageCreateHandler(Substitute.For<IDiscordRestClient>(), config, _services.GetRequiredService<IServiceScopeFactory>(), _ambient, _logger, chatClients, _time);
    }

    private NetCord.Gateway.Message MessageToTheBot()
        => GatewayMessages.Build(AuthorId, MentionsTheBot, restClient: new RestClient(new RestClientConfiguration { RequestHandler = _discord }));

    private async Task<List<string>> SavedReplies()
    {
        Db.ChangeTracker.Clear();
        return await Db.DiscordMessages.Where(x => x.FromDiscordUserId == (long)BotId).OrderBy(x => x.Id).Select(x => x.Content).ToListAsync();
    }

    private void AssertNothingLogged()
        => Assert.That(_logger.Entries, Is.Empty, string.Join("\n", _logger.Entries.Select(x => x.Exception?.ToString() ?? x.Message)));

    /// <summary>A tool's result as JSON, whether the function gave it marshalled already or as its own value.</summary>
    private static JsonElement AsJson(object? result) => result is JsonElement json ? json : JsonSerializer.SerializeToElement(result);

    private static AIFunction Tool(ChatOptions options, string name)
        => options.Tools!.OfType<AIFunction>().Single(x => x.Name == name);

    [Test]
    public async Task HandleAsync_NewAuthor_SavesThemTheBotTheirMessageAndTheReply()
    {
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi there"));

        await Handler(model).HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        Db.ChangeTracker.Clear();
        var users = await Db.DiscordUsers.OrderBy(x => x.DiscordUserID).Select(x => new { x.DiscordUserID, x.DiscordUsername }).ToListAsync();
        var asked = await Db.DiscordMessages.SingleAsync(x => x.Id == 10);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(users.Select(x => (x.DiscordUserID, x.DiscordUsername)), Is.EqualTo(new[] { ((long)BotId, "GrifballWebApp"), ((long)AuthorId, "user5") }));
            Assert.That((asked.FromDiscordUserId, asked.ToDiscordUserId, asked.Content), Is.EqualTo(((long)AuthorId, (long)BotId, "hello")));
            Assert.That(await SavedReplies(), Is.EqualTo(HiThere));
            Assert.That(_discord.SentMessages().Single().Body.GetProperty("content").GetString(), Is.EqualTo("Hi there"));
        }
    }

    [Test]
    public async Task HandleAsync_SendsTheModelTheConversationSoFar_OldestFirst_WithItsInstructionsAndTools()
    {
        Db.DiscordUsers.AddRange(DiscordUser.FromDiscord((long)AuthorId, "user5"), DiscordUser.FromDiscord((long)BotId, "GrifballWebApp"));
        // Earlier than the new message, which is stamped with its snowflake ID's time (ID 10: just after Discord's 2015 epoch)
        Db.DiscordMessages.AddRange(
            new DiscordMessage { Id = 2, FromDiscordUserId = (long)BotId, ToDiscordUserId = (long)AuthorId, Content = "Earlier answer", Timestamp = new DateTime(2014, 12, 31, 0, 1, 0, DateTimeKind.Utc) },
            new DiscordMessage { Id = 1, FromDiscordUserId = (long)AuthorId, ToDiscordUserId = (long)BotId, Content = "Earlier question", Timestamp = new DateTime(2014, 12, 31, 0, 0, 0, DateTimeKind.Utc) });
        await Db.SaveChangesAsync();
        var model = new ScriptedChatClient((TimeSpan.Zero, "Answer"));

        await Handler(model).HandleAsync(MessageToTheBot());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.Messages.Select(x => (x.Role, x.Text)), Is.EqualTo(new[]
            {
                (ChatRole.User, "Earlier question"),
                (ChatRole.Assistant, "Earlier answer"),
                (ChatRole.User, "hello"),
            }));
            Assert.That(model.Options!.Instructions, Does.Contain("You are a Discord bot"));
            // The author has no linked account: every chat tool but the one only some roles may use
            Assert.That(model.Options!.Tools!.Select(x => x.Name), Is.EquivalentTo(GrifTools.Chat.Where(t => t != "pull_season_match_games")));
            // The two users were there already: not added again
            Assert.That(await Db.DiscordUsers.CountAsync(), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task HandleAsync_LinkedAuthor_IsTheCurrentAndAmbientUserWhileTheMessageIsHandled()
    {
        var user = await AddUser(AuthorId, "Linked");
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi there"));

        await Handler(model).HandleAsync(MessageToTheBot());

        Assert.That(await SavedReplies(), Is.EqualTo(HiThere));
        _currentUser.Received(1).SetCurrentUserId(user.Id);
        Received.InOrder(() =>
        {
            _ambient.Begin();
            _ambient.UserId = user.Id;
            _ambient.End();
        });
    }

    [Test]
    public async Task HandleAsync_ReplyOverDiscordsLimit_IsSentAndSavedInMessagesOf2000Characters()
    {
        var model = new ScriptedChatClient((TimeSpan.Zero, new string('a', 2100)));

        await Handler(model).HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await SavedReplies(), Is.EqualTo(new[] { new string('a', 2000), new string('a', 100) }));
            Assert.That(_discord.SentMessages(), Has.Count.EqualTo(2));
        }
    }

    [Test]
    public async Task HandleAsync_EmptyReply_SendsAndSavesNothing()
    {
        var model = new ScriptedChatClient((TimeSpan.Zero, ""));

        await Handler(model).HandleAsync(MessageToTheBot());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await SavedReplies(), Is.Empty);
            Assert.That(_discord.Requests, Is.Empty);
        }
    }

    [Test]
    public async Task HandleAsync_StreamedReply_IsShownAsItComes_SplitAtTheLimit_AndEditedToTheWhole()
    {
        // Each pause (on the fake clock) is over the second between streamed updates: the first update splits off a full
        // message and starts the next, the second edits it, and the last chunk (no pause) is shown by the final edit.
        var model = new ScriptedChatClient(
            (TimeSpan.Zero, new string('a', 2100)),
            (TimeSpan.FromMilliseconds(1500), "!"),
            (TimeSpan.FromMilliseconds(1500), new string('b', 30)),
            (TimeSpan.Zero, " The end."))
        {
            Clock = _time,
        };

        await Handler(model).HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await SavedReplies(), Is.EqualTo(new[] { new string('a', 2000), new string('a', 100) + "!" + new string('b', 30) + " The end." }));
            Assert.That(_discord.SentMessages(), Has.Count.EqualTo(2), "The full message and the start of the next");
            Assert.That(_discord.Requests.Count(r => r.Method == HttpMethod.Patch), Is.EqualTo(2), "The streamed edit and the final one");
        }
    }

    [Test]
    public async Task HandleAsync_StreamedReplyAlreadyShownInFull_IsSavedWithoutAnotherEdit()
    {
        var model = new ScriptedChatClient(
            (TimeSpan.Zero, "This first chunk is over 25 characters"),
            (TimeSpan.FromMilliseconds(1500), "!"))
        {
            Clock = _time,
        };

        await Handler(model).HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await SavedReplies(), Is.EqualTo(ShownInFull));
            Assert.That(_discord.Requests.Count(r => r.Method == HttpMethod.Patch), Is.Zero);
        }
    }

    [Test]
    public async Task TheSharedTools_RunInTheMessagesScope_AndARefusalReachesTheModel()
    {
        var xbox = XboxUser.FromXbox(77, "Grunt Padre");
        Db.XboxUsers.Add(xbox);
        await Db.SaveChangesAsync();
        object? now = null, games = null, refused = null;
        var model = new ScriptedChatClient((TimeSpan.Zero, "Done"))
        {
            BeforeReply = async options =>
            {
                now = await Tool(options, "get_current_time").InvokeAsync(new AIFunctionArguments());
                games = await Tool(options, "get_recent_games").InvokeAsync(new AIFunctionArguments { ["gamertag"] = "Grunt Padre" });
                refused = await Tool(options, "get_recent_games").InvokeAsync(new AIFunctionArguments { ["gamertag"] = "Nobody" });
            },
        };

        await Handler(model).HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(AsJson(now).GetProperty("unixSeconds").GetInt64(), Is.EqualTo(_time.GetUtcNow().ToUnixTimeSeconds()));
            Assert.That(AsJson(games).GetArrayLength(), Is.Zero);
            Assert.That(refused?.ToString(), Does.StartWith("Refused: The site has no games saved for the gamertag Nobody"));
            // A small model can't turn the tools' ISO times into Discord's epoch timestamps reliably: it writes them as given
            Assert.That(model.Options!.Instructions, Does.Contain("in UTC, as the tools give them").And.Not.Contain("<t:"));
        }
        // Reading the saved games pulls nothing from Halo
        await _dataPull.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
    }
}
