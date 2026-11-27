using DiscordInterface.Generated;
using GrifballWebApp.Application.Chat;
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
using NSubstitute.ExceptionExtensions;
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
    private static readonly string[] TapIt = ["Tap it to join."];
    private static readonly string[] ConfirmOrCancelA1 = ["chatconfirm:a1:yes", "chatconfirm:a1:no"];
    private static readonly string[] OfferedOnly = [GrifballWebApp.Infrastructure.Discord.DiscordButtonContants.JoinQueue];

    /// <summary>The bot's commands, as its registry has them: offer_button is for a host that has some.</summary>
    private sealed class BotCommands : ISlashCommandCatalog
    {
        public IReadOnlyList<SlashCommandDto> Commands() => [new("/help", "Help")];
    }

    /// <summary>The model's call of offer_button for a <paramref name="kind"/> button.</summary>
    private static FunctionCallContent OfferButton(ChatButtonKind kind)
        => new("offer-1", GrifTools.OfferButton, new Dictionary<string, object?> { ["kind"] = kind });
    private static readonly string[] ShownInFull = ["This first chunk is over 25 characters!"];
    private static readonly string[] ChannelQuestion = ["Channel question"];
    private static readonly string[] ChannelQuestionThenHello = ["Channel question", "hello"];
    private static readonly string[] PublicAnswer = ["Public answer"];

    private readonly RecordingLogger<MessageCreateHandler> _logger = new();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IAmbientCurrentUser _ambient = Substitute.For<IAmbientCurrentUser>();
    private readonly IGetsertXboxUserService _xboxUsers = Substitute.For<IGetsertXboxUserService>();
    private readonly IDataPullService _dataPull = Substitute.For<IDataPullService>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
    private bool _modelConfigured = true;
    /// <summary>What reading the models throws as a reply starts (as the author's choice and month, read from the database, may).</summary>
    private Exception? _modelsFail;
    private readonly ChatLimits _limits = new();
    private readonly IDiscordRestClient _rest = Substitute.For<IDiscordRestClient>();
    private readonly FakeRestRequestHandler _discord = new() { EchoContent = true, UniqueIds = true };
    private ServiceProvider _services = null!;
    /// <summary>The chat's models, as <see cref="Handler"/> gives the factory; the single Ollama model unless a test sets more.</summary>
    private IReadOnlyList<ChatModelOption>? _configuredModels;
    private IChatClientFactory _chatClients = null!;

    [TearDown]
    public async Task TearDownServices()
    {
        _limits.Dispose();
        _discord.Dispose();
        if (_services is not null)
            await _services.DisposeAsync();
    }

    /// <summary>The handler, its scopes giving the test's database (as Program.cs's scoped GrifballContext would) and
    /// <paramref name="models"/> as the chat model, one per message.</summary>
    private MessageCreateHandler Handler(params IChatClient[] models)
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
            // get_recent_games finds the account by gamertag through it
            .AddTransient<GrifballWebApp.Application.XboxAccounts.XboxAccountService>()
            // get_player_image's, and the pictures a reply names
            .AddTransient<GrifballWebApp.Application.XboxAccounts.XboxImageService>()
            .AddTransient<GrifballWebApp.Application.Seasons.SeasonVisibility>()
            // get_season's
            .AddTransient<GrifballWebApp.Application.Seasons.SeasonService>()
            .AddTransient<GrifballWebApp.Application.Stats.SeasonStatsService>()
            .AddTransient<GrifballWebApp.Application.TeamStandings.TeamStandingsService>()
            .AddTransient<GrifballWebApp.Application.MatchPlanner.MatchPlannerService>()
            .AddTransient<GrifballWebApp.Application.Grades.GradesService>()
            .AddTransient<GrifballWebApp.Application.Signups.SignupsService>()
            .AddSingleton(Substitute.For<GrifballWebApp.Application.Brackets.IBracketService>())
            .AddSingleton<IChatWaiting, MemoryChatWaiting>()
            // offer_button's: the bot's commands are there
            .AddSingleton<GrifballWebApp.Application.Tools.ISlashCommandCatalog>(new BotCommands())
            // The season tools' draft helper, with the leaderboards' careers it reads
            .AddTransient<GrifballWebApp.Application.Draft.DraftHelperService>()
            .AddTransient<GrifballWebApp.Application.Leaderboards.LeaderboardsService>()
            .AddTransient<GrifballWebApp.Application.LateLeague.MatchmakingStatsService>()
            .AddSingleton<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions())))
            .BuildServiceProvider();
        var chatClients = Substitute.For<IChatClientFactory>();
        chatClients.Available.Returns(_modelConfigured);
        chatClients.Models.Returns(_configuredModels ?? [new ChatModelOption("ollama", "llama3.2:3b", ChatProvider.Ollama, "llama3.2:3b")]);
        if (_modelsFail is { } fail)
            chatClients.Models.Returns(_ => throw fail);
        chatClients.Create(Arg.Any<ChatModelOption>()).Returns(models.Length == 1 ? models[0] : new ChatClientSequence(models));
        _chatClients = chatClients;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Discord:ClientId"] = BotId.ToString(), ["BaseUrl"] = "https://grif.test" }).Build();
        var scopes = _services.GetRequiredService<IServiceScopeFactory>();
        return new MessageCreateHandler(_rest, config, scopes, _ambient, _logger, new GrifChat(new ChatModel(chatClients, NullLoggerFactory.Instance), scopes, _limits,
            new ChatAccounts(scopes, _time, NullLogger<ChatAccounts>.Instance)), _time);
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

    [TestCase(false)]
    [TestCase(true)]
    public async Task HandleAsync_TracesTheMessage_TheRepliesSpansInItsTrace(bool direct)
    {
        using var spans = new ChatSpans();
        System.Diagnostics.Activity? modelCall = null;
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi there")) { BeforeReply = _ => { modelCall = System.Diagnostics.Activity.Current; return Task.CompletedTask; } };
        if (direct)
            DmHolds();

        await Handler(model).HandleAsync(direct ? DirectMessage() : MessageToTheBot());

        AssertNothingLogged();

        var message = modelCall is null ? null : ChatSpans.Ancestors(modelCall).LastOrDefault();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(modelCall?.Source.Name, Is.EqualTo(ChatModel.SourceName), "the model's call is traced");
            Assert.That(message?.Source.Name, Is.EqualTo(MessageCreateHandler.ActivitySourceName), "under the message's span, the trace's root");
            Assert.That(message?.OperationName, Is.EqualTo(MessageCreateHandler.MessageSpanName));
            Assert.That(message?.GetTagItem("discord.direct"), Is.EqualTo(direct));
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
            Assert.That(model.Options!.Instructions, Does.Contain(ChatHost.Discord.About));
            // The author has no linked account: every chat tool but the pull tools, which only some roles may use
            Assert.That(model.Options!.Tools!.Select(x => x.Name), Is.EquivalentTo(GrifTools.Chat.Where(t => !t.StartsWith("pull_", StringComparison.Ordinal))));
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
    public async Task HandleAsync_InAChannel_ALinkedAuthorWithAGamertag_GetsTheSignedInOnlyModel()
    {
        await AddUser(AuthorId, "Linked");
        var signedInOnly = new ChatModelOption("claude-haiku", "Claude Haiku", ChatProvider.Anthropic, "claude-haiku-5-5") { SignedIn = true };
        var free = new ChatModelOption("ollama", "llama3.2:3b", ChatProvider.Ollama, "llama3.2:3b");
        _configuredModels = [signedInOnly, free];
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi there"));

        await Handler(model).HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            _chatClients.Received(1).Create(signedInOnly);
            _chatClients.DidNotReceive().Create(free);
        }
    }

    [Test]
    public async Task HandleAsync_InAChannel_ALinkedAuthorWithNoGamertag_StillGetsTheSignedInOnlyModel()
    {
        // A real account, linked only through /setgamertag or a site sign-in with Discord: either signs them in on the
        // site too, so they count as signed in here whether or not they ever set a gamertag (unlike a dummy placeholder).
        await AddDiscordOnlyUser(AuthorId);
        var signedInOnly = new ChatModelOption("claude-haiku", "Claude Haiku", ChatProvider.Anthropic, "claude-haiku-5-5") { SignedIn = true };
        var free = new ChatModelOption("ollama", "llama3.2:3b", ChatProvider.Ollama, "llama3.2:3b");
        _configuredModels = [signedInOnly, free];
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi there"));

        await Handler(model).HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            _chatClients.Received(1).Create(signedInOnly);
            _chatClients.DidNotReceive().Create(free);
        }
    }

    [Test]
    public async Task HandleAsync_InAChannel_AnUnlinkedAuthor_GetsTheOpenModel_NeverTheSignedInOnlyOne()
    {
        var signedInOnly = new ChatModelOption("claude-haiku", "Claude Haiku", ChatProvider.Anthropic, "claude-haiku-5-5") { SignedIn = true };
        var free = new ChatModelOption("ollama", "llama3.2:3b", ChatProvider.Ollama, "llama3.2:3b");
        _configuredModels = [signedInOnly, free];
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi there"));

        await Handler(model).HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            _chatClients.Received(1).Create(free);
            _chatClients.DidNotReceive().Create(signedInOnly);
        }
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
            Assert.That(AsJson(games).GetProperty("games").GetArrayLength(), Is.Zero);
            Assert.That(refused?.ToString(), Does.StartWith("Refused: The site has no games saved for the gamertag Nobody"));
            // A small model can't turn the tools' ISO times into Discord's epoch timestamps reliably: it writes them as given
            Assert.That(model.Options!.Instructions, Does.Contain("in UTC, as the tools give them").And.Not.Contain("<t:"));
            // The bot runs tools without asking first, as an MCP client would before one that isn't read-only
            Assert.That(model.Options!.Instructions, Does.Contain("pull_xbox_account_games) only when the person asks"));
        }
        // Reading the saved games pulls nothing from Halo
        await _dataPull.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
    }

    private NetCord.Gateway.Message DirectMessage()
        => GatewayMessages.Build(AuthorId, restClient: new RestClient(new RestClientConfiguration { RequestHandler = _discord }), direct: true);

    /// <summary>What Discord has in the DM's channel (20), newest first as it gives them: the message being answered (10) too.</summary>
    private void DmHolds(params (ulong Id, ulong AuthorId, string Content)[] earlier)
    {
        var messages = earlier.Append((Id: 10ul, AuthorId: AuthorId, Content: "hello")).OrderByDescending(m => m.Id).Select(m =>
        {
            var message = Substitute.For<IDiscordRestMessage>();
            message.Id.Returns(m.Id);
            message.Author.Id.Returns(m.AuthorId);
            message.Content.Returns(m.Content);
            return message;
        }).ToList();
        _rest.GetMessagesAsync(20ul, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>()).Returns(_ => messages.ToAsyncEnumerable());
    }

    /// <summary>A season starting in 40 days and announced in 10: staff's alone until then (#435).</summary>
    private async Task<GrifballWebApp.Domain.League.Season> SeasonNotAnnouncedYet()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var season = GrifballWebApp.Domain.League.Season.Seed(seasonName: "Secret").WithDates(publicAt: now.AddDays(10), signupsOpen: now.AddDays(10),
            signupsClose: now.AddDays(20), draftStart: now.AddDays(25), seasonStart: now.AddDays(40), seasonEnd: now.AddDays(100));
        Db.Seasons.Add(season);
        await Db.SaveChangesAsync();
        return season;
    }

    /// <summary>A model that looks up <paramref name="seasonID"/> with get_season before it answers, the result kept in <paramref name="seen"/>.</summary>
    private static ScriptedChatClient LooksUpSeason(int seasonID, Action<object?> seen) => new((TimeSpan.Zero, "Done"))
    {
        BeforeReply = async options => seen(await Tool(options, "get_season").InvokeAsync(new AIFunctionArguments { ["seasonID"] = seasonID })),
    };

    private static List<string> ToolNames(ScriptedChatClient model) => model.Options!.Tools!.Select(t => t.Name).ToList();

    [Test]
    public async Task HandleAsync_StaffInADm_GetTheirStaffTools_AndSeeASeasonNotAnnouncedYet()
    {
        await AddUser(AuthorId, "Staffer", GrifballWebApp.Application.Identity.SiteRoles.Commissioner);
        var secret = await SeasonNotAnnouncedYet();
        DmHolds();
        object? seen = null;
        var model = LooksUpSeason(secret.SeasonID, x => seen = x);

        await Handler(model).HandleAsync(DirectMessage());

        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ToolNames(model), Does.Contain("get_commissioner_dashboard").And.Contain("report_season_match_game"));
            Assert.That(ToolNames(model), Does.Not.Contain("list_pending_gamertag_claims"), "a sysadmin's");
            Assert.That(AsJson(seen).GetProperty("seasonName").GetString(), Is.EqualTo("Secret"));
            Assert.That(model.Options!.Instructions, Does.Contain(GrifChat.PrivateLine));
            Assert.That(_discord.SentMessages().Single().Body.GetProperty("content").GetString(), Is.EqualTo("Done"));
        }
    }

    [Test]
    public async Task HandleAsync_APlayerInADm_GetsNoStaffTools()
    {
        await AddUser(AuthorId, "Player1", GrifballWebApp.Application.Identity.SiteRoles.Player);
        DmHolds();
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi"));

        await Handler(model).HandleAsync(DirectMessage());

        AssertNothingLogged();
        Assert.That(ToolNames(model), Is.EquivalentTo(GrifTools.Chat));
    }

    [Test]
    public async Task HandleAsync_StaffInAChannel_SeeAsThePublicDoes_WithNoStaffTools()
    {
        await AddUser(AuthorId, "Staffer", GrifballWebApp.Application.Identity.SiteRoles.Commissioner, GrifballWebApp.Application.Identity.SiteRoles.Sysadmin);
        var secret = await SeasonNotAnnouncedYet();
        object? seen = null;
        var model = LooksUpSeason(secret.SeasonID, x => seen = x);

        await Handler(model).HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ToolNames(model), Is.EquivalentTo(GrifTools.Chat));
            Assert.That(seen?.ToString(), Does.StartWith($"Refused: There is no season {secret.SeasonID}"));
            Assert.That(model.Options!.Instructions, Does.Not.Contain(GrifChat.PrivateLine));
        }
        _ = _rest.DidNotReceive().GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>?>(), Arg.Any<RestRequestProperties?>());
    }

    [Test]
    public async Task HandleAsync_ADmsConversation_IsTheDmsOwn_AndNeverFeedsAChannelsReply()
    {
        // The channels' conversation so far, kept in DiscordMessages
        Db.DiscordUsers.AddRange(DiscordUser.FromDiscord((long)AuthorId, "user5"), DiscordUser.FromDiscord((long)BotId, "GrifballWebApp"));
        Db.DiscordMessages.Add(new DiscordMessage { Id = 1, FromDiscordUserId = (long)AuthorId, ToDiscordUserId = (long)BotId, Content = "Channel question", Timestamp = new DateTime(2014, 12, 31, 0, 0, 0, DateTimeKind.Utc) });
        await Db.SaveChangesAsync();
        DmHolds((2, AuthorId, "DM question"), (3, BotId, "DM secret answer"));
        var inTheDm = new ScriptedChatClient((TimeSpan.Zero, "Another secret"));
        var inTheChannel = new ScriptedChatClient((TimeSpan.Zero, "Public answer"));
        var handler = Handler(inTheDm, inTheChannel);

        await handler.HandleAsync(DirectMessage());
        Db.ChangeTracker.Clear();
        var keptAfterTheDm = await Db.DiscordMessages.Select(m => m.Content).ToListAsync();
        await handler.HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            // The DM's own history, from Discord's DM channel: none of the channels'
            Assert.That(inTheDm.Messages.Select(x => (x.Role, x.Text)), Is.EqualTo(new[]
            {
                (ChatRole.User, "DM question"),
                (ChatRole.Assistant, "DM secret answer"),
                (ChatRole.User, "hello"),
            }));
            // Nothing of the DM kept where a channel's reply reads its history
            Assert.That(keptAfterTheDm, Is.EqualTo(ChannelQuestion));
            Assert.That(inTheChannel.Messages.Select(x => x.Text), Is.EqualTo(ChannelQuestionThenHello));
            Assert.That(await SavedReplies(), Is.EqualTo(PublicAnswer));
        }
    }

    [Test]
    public async Task HandleAsync_NoModelConfigured_SaysTheChatIsUnavailable_AndAsksNoModel()
    {
        _modelConfigured = false;
        var model = new ScriptedChatClient((TimeSpan.Zero, "Never"));

        await Handler(model).HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_discord.SentMessages().Single().Body.GetProperty("content").GetString(), Is.EqualTo(GrifChat.Unavailable));
            Assert.That(model.Options, Is.Null, "the model isn't asked");
            Assert.That(await Db.DiscordMessages.AnyAsync(), Is.False);
        }
    }

    [Test]
    public async Task HandleAsync_DmsOverTheirQuestionsAMinute_AreRefused_AsOnTheSite()
    {
        DmHolds();
        var models = Enumerable.Range(0, ChatLimits.OthersPerMinute).Select(_ => (IChatClient)new ScriptedChatClient((TimeSpan.Zero, "Hi"))).ToArray();
        var handler = Handler(models);

        for (var i = 0; i <= ChatLimits.OthersPerMinute; i++)
            await handler.HandleAsync(DirectMessage());

        AssertNothingLogged();
        var replies = _discord.SentMessages().Select(m => m.Body.GetProperty("content").GetString()).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(replies, Has.Count.EqualTo(ChatLimits.OthersPerMinute + 1));
            Assert.That(replies[..^1], Is.All.EqualTo("Hi"));
            Assert.That(replies[^1], Is.EqualTo(ChatLimits.TooMany));
        }
    }

    [Test]
    public async Task HandleAsync_AQuestionOverTheChatsLimit_IsRefused_AsOnTheSite()
    {
        var model = new ScriptedChatClient((TimeSpan.Zero, "Never"));
        var question = GatewayMessages.Build(AuthorId, MentionsTheBot, restClient: new RestClient(new RestClientConfiguration { RequestHandler = _discord }),
            content: new string('a', GrifChat.MaxQuestionLength + 1));

        await Handler(model).HandleAsync(question);

        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_discord.SentMessages().Single().Body.GetProperty("content").GetString(), Is.EqualTo("Ask in 2000 characters or fewer."));
            Assert.That(model.Options, Is.Null, "the model isn't asked");
            Assert.That(await Db.DiscordMessages.AnyAsync(), Is.False, "a question refused is kept for no later conversation");
        }
    }

    [Test]
    public async Task HandleAsync_ADmsRefusals_AndTheQuestionsTheyRefused_AreNoPartOfItsHistory()
    {
        // The DM channel holds everything said there: the bot's refusals too
        DmHolds((2, AuthorId, "Too much at once"), (3, BotId, ChatLimits.TooMany), (4, AuthorId, "Who won?"), (5, BotId, "The Grifs."));
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi"));

        await Handler(model).HandleAsync(DirectMessage());

        AssertNothingLogged();
        Assert.That(model.Messages.Select(x => (x.Role, x.Text)), Is.EqualTo(new[]
        {
            (ChatRole.User, "Who won?"),
            (ChatRole.Assistant, "The Grifs."),
            (ChatRole.User, "hello"),
        }));
    }

    [Test]
    public async Task HandleAsync_AReplyTheChatCutsShort_SaysWhy()
    {
        var model = new ScriptedChatClient { BeforeReply = _ => throw new ChatRefusedException(GrifChat.TookTooLong) };

        await Handler(model).HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_discord.SentMessages().Single().Body.GetProperty("content").GetString(), Is.EqualTo(GrifChat.TookTooLong));
            Assert.That(await SavedReplies(), Is.Empty, "why is no part of the conversation");
        }
    }

    [Test]
    public async Task HandleAsync_AReplyThatFailsToStart_SaysTheChatCouldntAnswer_AndLogsWhy()
    {
        _modelsFail = new InvalidOperationException("The database is down");
        var model = new ScriptedChatClient((TimeSpan.Zero, "Never"));

        await Handler(model).HandleAsync(MessageToTheBot());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_discord.SentMessages().Single().Body.GetProperty("content").GetString(), Is.EqualTo(GrifballWebApp.Server.Chat.ChatHub.Failed),
                "The author is told, not left with no answer");
            Assert.That(_logger.Entries.Single().Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Error));
            Assert.That(model.Options, Is.Null, "the model isn't asked");
        }
    }

    [Test]
    public async Task HandleAsync_AReplyTheChatCutsShort_IsAnErrorOnTheMessagesSpan()
    {
        using var spans = new ChatSpans();
        System.Diagnostics.Activity? modelCall = null;
        var model = new ScriptedChatClient
        {
            BeforeReply = _ =>
            {
                modelCall = System.Diagnostics.Activity.Current;
                throw new ChatRefusedException(GrifChat.TookTooLong);
            },
        };

        await Handler(model).HandleAsync(MessageToTheBot());

        var message = modelCall is null ? null : ChatSpans.Ancestors(modelCall).LastOrDefault();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message?.OperationName, Is.EqualTo(MessageCreateHandler.MessageSpanName));
            Assert.That(message?.Status, Is.EqualTo(System.Diagnostics.ActivityStatusCode.Error), "a slow reply is found by its status");
            Assert.That(message?.StatusDescription, Is.EqualTo(GrifChat.TookTooLong));
        }
    }

    [Test]
    public async Task HandleAsync_ANoInADm_AnswersTheChangeWaitingForTheAuthor_AndRunsNothing()
    {
        var user = await AddUser(AuthorId, "Staffer", GrifballWebApp.Application.Identity.SiteRoles.Commissioner);
        DmHolds();
        var model = new ScriptedChatClient((TimeSpan.Zero, "Left as it was."));
        var handler = Handler(model);
        var accounts = new ChatAccounts(_services!.GetRequiredService<IServiceScopeFactory>(), _time, NullLogger<ChatAccounts>.Instance);
        var asker = ChatLimits.Asker(new ToolCaller(user.Id, []), "");
        await accounts.SaveWaitingAsync(ChatHost.DiscordDirect, asker, new ChatAction("a1", "ollama", "forfeit_season_match(seasonMatchID: 12)",
            [new ChatActionCall("r1", "call-1", "forfeit_season_match", "{\"seasonMatchID\":12}")]));

        await handler.HandleAsync(GatewayMessages.Build(AuthorId, restClient: new RestClient(new RestClientConfiguration { RequestHandler = _discord }),
            direct: true, content: "no"));

        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.Messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single().Result?.ToString(), Does.Contain("rejected"));
            Assert.That(_discord.SentMessages().Single().Body.GetProperty("content").GetString(), Is.EqualTo("Left as it was."));
            Assert.That(await accounts.WaitingAsync(ChatHost.DiscordDirect, asker), Is.Null, "Answered");
        }
    }

    /// <summary>
    /// The DM holds the bot's message 5 with <paramref name="rows"/> of buttons (each row its custom IDs), before the author's "hello" (10):
    /// the message, to see what is done to it.
    /// </summary>
    private IDiscordRestMessage DmHoldsButtons(params string[][] rows)
    {
        var built = GatewayMessages.Build(BotId, direct: true,
            components: [.. rows.Select(row => (object)new { type = 1, components = row.Select(id => new { type = 2, style = 1, label = id, custom_id = id }).ToArray() })]);
        var wrapped = new List<IDiscordMessageComponent>();
        foreach (var component in built.Components)
        {
            var wrapper = Substitute.For<IDiscordMessageComponent>();
            wrapper.Original.Returns(component);
            wrapped.Add(wrapper);
        }
        var withButtons = Substitute.For<IDiscordRestMessage>();
        withButtons.Id.Returns(5ul);
        withButtons.Author.Id.Returns(BotId);
        withButtons.Content.Returns("This waits for you.");
        withButtons.Components.Returns(wrapped);
        var hello = Substitute.For<IDiscordRestMessage>();
        hello.Id.Returns(10ul);
        hello.Author.Id.Returns(AuthorId);
        hello.Content.Returns("hello");
        IDiscordRestMessage[] messages = [hello, withButtons];
        _rest.GetMessagesAsync(20ul, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>()).Returns(_ => messages.ToAsyncEnumerable());
        return withButtons;
    }

    /// <summary>A change waiting for <paramref name="user"/>'s answer in their DM, as "a1".</summary>
    private async Task<string> WaitingFor(GrifballWebApp.Domain.Players.User user)
    {
        var accounts = new ChatAccounts(_services!.GetRequiredService<IServiceScopeFactory>(), _time, NullLogger<ChatAccounts>.Instance);
        var asker = ChatLimits.Asker(new ToolCaller(user.Id, []), "");
        await accounts.SaveWaitingAsync(ChatHost.DiscordDirect, asker, new ChatAction("a1", "ollama", "forfeit_season_match(seasonMatchID: 12)",
            [new ChatActionCall("r1", "call-1", "forfeit_season_match", "{\"seasonMatchID\":12}")]));
        return asker;
    }

    private NetCord.Gateway.Message TypedInTheDm(string content)
        => GatewayMessages.Build(AuthorId, restClient: new RestClient(new RestClientConfiguration { RequestHandler = _discord }), direct: true, content: content);

    private static int Modified(IDiscordRestMessage message) => message.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IDiscordRestMessage.ModifyAsync));

    [Test]
    public async Task HandleAsync_ATypedAnswer_TakesConfirmAndCancelOffTheEarlierMessage_TheOfferedButtonsStay()
    {
        var user = await AddUser(AuthorId, "Staffer", GrifballWebApp.Application.Identity.SiteRoles.Commissioner);
        var withButtons = DmHoldsButtons(ConfirmOrCancelA1, OfferedOnly);
        Action<IDiscordMessageOptions>? edit = null;
        withButtons.ModifyAsync(Arg.Do<Action<IDiscordMessageOptions>>(a => edit = a), Arg.Any<RestRequestProperties?>(), Arg.Any<CancellationToken>()).Returns(withButtons);
        var handler = Handler(new ScriptedChatClient((TimeSpan.Zero, "Left as it was.")));
        await WaitingFor(user);

        await handler.HandleAsync(TypedInTheDm("no"));

        var options = Substitute.For<IDiscordMessageOptions>();
        IEnumerable<IMessageComponentProperties>? kept = null;
        options.WithComponents(Arg.Do<IEnumerable<IMessageComponentProperties>?>(c => kept = c)).Returns(options);
        edit?.Invoke(options);
        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(kept?.OfType<ActionRowProperties>().Select(row => string.Join(",", row.OfType<ButtonProperties>().Select(b => b.CustomId))),
                Is.EqualTo(OfferedOnly));
            Assert.That(_discord.SentMessages().Single().Body.GetProperty("content").GetString(), Is.EqualTo("Left as it was."));
        }
    }

    [Test]
    public async Task HandleAsync_ATypedAnswerTheChatRefuses_LeavesConfirmAndCancel_AsTheChangeStillWaits()
    {
        var user = await AddUser(AuthorId, "Staffer", GrifballWebApp.Application.Identity.SiteRoles.Commissioner);
        var withButtons = DmHoldsButtons(ConfirmOrCancelA1);
        var handler = Handler(new ScriptedChatClient((TimeSpan.Zero, "Never")));
        var asker = await WaitingFor(user);
        var (busy, _) = _limits.TryStart(asker);

        using (busy)
            await handler.HandleAsync(TypedInTheDm("yes"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_discord.SentMessages().Single().Body.GetProperty("content").GetString(), Is.EqualTo(ChatLimits.Busy));
            Assert.That(Modified(withButtons), Is.Zero);
        }
    }

    [Test]
    public async Task HandleAsync_ConfirmAndCancelThatCantBeTakenOff_AreLogged_AndTheReplyGoesOn()
    {
        var user = await AddUser(AuthorId, "Staffer", GrifballWebApp.Application.Identity.SiteRoles.Commissioner);
        var withButtons = DmHoldsButtons(ConfirmOrCancelA1);
        withButtons.ModifyAsync(Arg.Any<Action<IDiscordMessageOptions>>(), Arg.Any<RestRequestProperties?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Discord is down"));
        var handler = Handler(new ScriptedChatClient((TimeSpan.Zero, "Left as it was.")));
        await WaitingFor(user);

        await handler.HandleAsync(TypedInTheDm("no"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(_logger.Entries.Single().Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(_discord.SentMessages().Single().Body.GetProperty("content").GetString(), Is.EqualTo("Left as it was."));
        }
    }

    [Test]
    public async Task HandleAsync_AButtonTheModelOffered_IsUnderTheReply()
    {
        var model = new ScriptedChatClient((TimeSpan.Zero, "Tap it to join.")) { Call = OfferButton(ChatButtonKind.JoinQueue) };

        await Handler(model).HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        var body = _discord.SentMessages().Single().Body;
        var button = body.GetProperty("components")[0].GetProperty("components")[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(body.GetProperty("content").GetString(), Is.EqualTo("Tap it to join."));
            Assert.That(button.GetProperty("custom_id").GetString(), Is.EqualTo(GrifballWebApp.Infrastructure.Discord.DiscordButtonContants.JoinQueue));
            Assert.That(await SavedReplies(), Is.EqualTo(TapIt));
        }
    }

    [Test]
    public async Task HandleAsync_AButtonTokenInTheModelsText_IsNoButton()
    {
        var model = new ScriptedChatClient((TimeSpan.Zero, "Here [button:join_queue]"));

        await Handler(model).HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        var body = _discord.SentMessages().Single().Body;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(body.GetProperty("content").GetString(), Is.EqualTo("Here [button:join_queue]"), "shown as written");
            Assert.That(body.TryGetProperty("components", out var rows) && rows.GetArrayLength() > 0, Is.False);
        }
    }

    [Test]
    public async Task HandleAsync_AStreamedReplyOfferingAButton_GetsItInTheLastEdit()
    {
        var model = new ScriptedChatClient(
            (TimeSpan.Zero, "This first chunk is over 25 characters"),
            (TimeSpan.FromMilliseconds(1500), "!"))
        {
            Clock = _time,
            Call = OfferButton(ChatButtonKind.JoinQueue),
        };

        await Handler(model).HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        var edit = _discord.Requests.Last(r => r.Method == HttpMethod.Patch).Json.RootElement;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(edit.GetProperty("content").GetString(), Is.EqualTo("This first chunk is over 25 characters!"));
            Assert.That(edit.GetProperty("components")[0].GetProperty("components")[0].GetProperty("custom_id").GetString(),
                Is.EqualTo(GrifballWebApp.Infrastructure.Discord.DiscordButtonContants.JoinQueue));
        }
    }

    [Test]
    public async Task HandleClickAsync_ACancelButton_AnswersTheWaitingChangeAsANoDoes()
    {
        var user = await AddUser(AuthorId, "Staffer", GrifballWebApp.Application.Identity.SiteRoles.Commissioner);
        DmHolds();
        var model = new ScriptedChatClient((TimeSpan.Zero, "Left as it was."));
        var handler = Handler(model);
        var accounts = new ChatAccounts(_services!.GetRequiredService<IServiceScopeFactory>(), _time, NullLogger<ChatAccounts>.Instance);
        var asker = ChatLimits.Asker(new ToolCaller(user.Id, []), "");
        await accounts.SaveWaitingAsync(ChatHost.DiscordDirect, asker, new ChatAction("a1", "ollama", "forfeit_season_match(seasonMatchID: 12)",
            [new ChatActionCall("r1", "call-1", "forfeit_season_match", "{\"seasonMatchID\":12}")]));

        await handler.HandleClickAsync(GatewayMessages.Build(BotId, restClient: new RestClient(new RestClientConfiguration { RequestHandler = _discord }), direct: true),
            AuthorId, "user5", new MessageCreateHandler.ChatClick("a1", false));

        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.Messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single().Result?.ToString(), Does.Contain("rejected"));
            Assert.That(_discord.SentMessages().Single().Body.GetProperty("content").GetString(), Is.EqualTo("Left as it was."));
            Assert.That(await accounts.WaitingAsync(ChatHost.DiscordDirect, asker), Is.Null, "Answered");
        }
    }

    [Test]
    public async Task HandleClickAsync_AClickTheChatRefuses_GivesConfirmAndCancelAgain_AsTheChangeStillWaits()
    {
        var user = await AddUser(AuthorId, "Staffer", GrifballWebApp.Application.Identity.SiteRoles.Commissioner);
        DmHolds();
        var handler = Handler(new ScriptedChatClient((TimeSpan.Zero, "Never")));
        var accounts = new ChatAccounts(_services!.GetRequiredService<IServiceScopeFactory>(), _time, NullLogger<ChatAccounts>.Instance);
        var asker = ChatLimits.Asker(new ToolCaller(user.Id, []), "");
        await accounts.SaveWaitingAsync(ChatHost.DiscordDirect, asker, new ChatAction("a1", "ollama", "forfeit_season_match(seasonMatchID: 12)",
            [new ChatActionCall("r1", "call-1", "forfeit_season_match", "{\"seasonMatchID\":12}")]));
        // Another reply of theirs under way: the yes waits for it
        var (busy, _) = _limits.TryStart(asker);

        using (busy)
        {
            await handler.HandleClickAsync(GatewayMessages.Build(BotId, restClient: new RestClient(new RestClientConfiguration { RequestHandler = _discord }), direct: true),
                AuthorId, "user5", new MessageCreateHandler.ChatClick("a1", true));
        }

        AssertNothingLogged();
        var body = _discord.SentMessages().Single().Body;
        var buttons = body.GetProperty("components")[0].GetProperty("components").EnumerateArray().Select(b => b.GetProperty("custom_id").GetString()).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(body.GetProperty("content").GetString(), Is.EqualTo(ChatLimits.Busy));
            Assert.That(buttons, Is.EqualTo(ConfirmOrCancelA1));
            Assert.That((await accounts.WaitingAsync(ChatHost.DiscordDirect, asker))?.Id, Is.EqualTo("a1"), "Still waits");
        }
    }

    [Test]
    public async Task HandleClickAsync_ANoChangeWaiting_SaysSo()
    {
        DmHolds();
        var model = new ScriptedChatClient((TimeSpan.Zero, "Never"));

        await Handler(model).HandleClickAsync(GatewayMessages.Build(BotId, restClient: new RestClient(new RestClientConfiguration { RequestHandler = _discord }), direct: true),
            AuthorId, "user5", new MessageCreateHandler.ChatClick("gone", true));

        AssertNothingLogged();
        Assert.That(_discord.SentMessages().Single().Body.GetProperty("content").GetString(), Is.EqualTo(GrifChat.NothingWaiting));
    }

    [Test]
    public async Task HandleClickAsync_AFailure_IsLogged_NotThrown_AndAnErrorOnItsSpan()
    {
        using var spans = new ChatSpans();
        System.Diagnostics.Activity? current = null;
        _rest.GetMessagesAsync(20ul, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>()).Returns(_ =>
        {
            current = System.Diagnostics.Activity.Current;
            throw new InvalidOperationException("Discord is down");
        });

        await Handler(new ScriptedChatClient((TimeSpan.Zero, "Never"))).HandleClickAsync(
            GatewayMessages.Build(BotId, restClient: new RestClient(new RestClientConfiguration { RequestHandler = _discord }), direct: true),
            AuthorId, "user5", new MessageCreateHandler.ChatClick("a1", true));

        var click = current is null ? null : ChatSpans.Ancestors(current).LastOrDefault() ?? current;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_logger.Entries.Single().Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(click?.OperationName, Is.EqualTo(MessageCreateHandler.MessageSpanName));
            Assert.That(click?.Status, Is.EqualTo(System.Diagnostics.ActivityStatusCode.Error));
            Assert.That(click?.StatusDescription, Is.EqualTo(nameof(InvalidOperationException)));
        }
    }

    [Test]
    public async Task HandleAsync_StaffInADm_TheirSettingDecides_WhetherAChangeWaits()
    {
        var user = await AddUser(AuthorId, "Staffer", GrifballWebApp.Application.Identity.SiteRoles.Commissioner);
        var choice = GrifballWebApp.Domain.Players.ChatChoice.Make(user.Id);
        choice.SetAutoApprove(true);
        Db.ChatChoices.Add(choice);
        await Db.SaveChangesAsync();
        DmHolds();
        var model = new ScriptedChatClient((TimeSpan.Zero, "Hi"));

        await Handler(model).HandleAsync(DirectMessage());

        AssertNothingLogged();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.Options!.Instructions, Does.Contain(GrifChat.AutoApprovedLine));
            Assert.That(model.Options.Tools, Has.None.InstanceOf<ApprovalRequiredAIFunction>());
        }
    }

    [Test]
    public async Task HandleAsync_AReplyNamingAPlayersPicture_AttachesItAndLeavesTheAddressOut()
    {
        Db.XboxUsers.Add(XboxUser.FromXbox(500, "Grifter"));
        var player = User.Seed(userName: "grif", xboxUserID: 500);
        Db.Users.Add(player);
        Db.XboxUserImages.Add(new XboxUserImage { XboxUserID = 500, Kind = XboxUserImageKind.HaloEmblem, Status = XboxUserImageStatus.Found, ContentType = "image/png", Content = [1, 2, 3], CheckedAt = _time.GetUtcNow().UtcDateTime });
        await Db.SaveChangesAsync();
        var model = new ScriptedChatClient((TimeSpan.Zero, $"Here is the emblem, friend: https://grif.test/api/users/{player.Id}/halo-emblem And /api/users/{player.Id}/gamer-picture"));

        await Handler(model).HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        var last = _discord.Requests.Last(r => r.Method != HttpMethod.Get);
        var sent = last.Json.RootElement;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent.GetProperty("content").GetString(), Is.EqualTo("Here is the emblem, friend: And"));
            Assert.That(sent.GetProperty("attachments").GetArrayLength(), Is.EqualTo(1), "The gamer picture isn't stored: only the emblem goes");
            Assert.That(sent.GetProperty("attachments")[0].GetProperty("filename").GetString(), Is.EqualTo($"{player.Id}-halo-emblem.png"));
            Assert.That(await SavedReplies(), Has.None.Contain("/api/users"));
        }
    }

    [Test]
    public async Task HandleAsync_AReplyThatIsOnlyAPicture_IsSentAsTheFileAlone()
    {
        Db.XboxUsers.Add(XboxUser.FromXbox(500, "Grifter"));
        var player = User.Seed(userName: "grif", xboxUserID: 500);
        Db.Users.Add(player);
        Db.XboxUserImages.Add(new XboxUserImage { XboxUserID = 500, Kind = XboxUserImageKind.GamerPicture, Status = XboxUserImageStatus.Found, ContentType = "image/jpeg", Content = [9], CheckedAt = _time.GetUtcNow().UtcDateTime });
        await Db.SaveChangesAsync();
        var model = new ScriptedChatClient((TimeSpan.Zero, $"/api/users/{player.Id}/gamer-picture"));

        await Handler(model).HandleAsync(MessageToTheBot());

        AssertNothingLogged();
        var sent = _discord.Requests.Last(r => r.Method != HttpMethod.Get).Json.RootElement;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent.GetProperty("attachments")[0].GetProperty("filename").GetString(), Is.EqualTo($"{player.Id}-gamer-picture.jpg"));
            Assert.That(sent.TryGetProperty("content", out var content) ? content.GetString() : "", Is.Empty);
        }
    }

    [Test]
    public async Task HandleAsync_APictureShown_IsKeptInTheSavedReply_SoLaterTurnsKnow()
    {
        var players = await PlayersWithEmblems(1);
        var model = new ScriptedChatClient((TimeSpan.Zero, $"Here you go: {Emblem(players[0])}"));

        await Handler(model).HandleAsync(MessageToTheBot());

        var saved = await SavedReplies();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved, Has.Count.EqualTo(1));
            Assert.That(saved[0], Does.StartWith("Here you go:"));
            Assert.That(saved[0], Does.Contain($"{players[0]}-halo-emblem.png"));
            Assert.That(saved[0], Does.Not.Contain("/api/users"));
        }
    }

    [Test]
    public async Task HandleAsync_AReplyThatIsOnlyAPicture_IsSavedAsTheNoteOfWhatWasShown()
    {
        var players = await PlayersWithEmblems(1);
        var model = new ScriptedChatClient((TimeSpan.Zero, Emblem(players[0])));

        await Handler(model).HandleAsync(MessageToTheBot());

        var saved = await SavedReplies();
        Assert.That(saved.Single(), Is.EqualTo($"[Pictures shown with this reply: {players[0]}-halo-emblem.png]"));
    }

    [Test]
    public async Task HandleAsync_PicturesDiscordRefuses_LeaveNoNoteOfAPictureShown()
    {
        var players = await PlayersWithEmblems(1);
        _discord.FailWhenAttaching = true;
        var model = new ScriptedChatClient((TimeSpan.Zero, $"Here you go: {Emblem(players[0])}"));

        await Handler(model).HandleAsync(MessageToTheBot());

        Assert.That(await SavedReplies(), Has.None.Contain("halo-emblem"));
    }

    /// <summary>Players with a stored Halo emblem of <paramref name="size"/> bytes each, as their user IDs.</summary>
    private async Task<List<int>> PlayersWithEmblems(int count, int size = 3)
    {
        var players = new List<int>();
        for (var i = 0; i < count; i++)
        {
            Db.XboxUsers.Add(XboxUser.FromXbox(600 + i, $"Gamer{i}"));
            var player = User.Seed(userName: $"player{i}", xboxUserID: 600 + i);
            Db.Users.Add(player);
            Db.XboxUserImages.Add(new XboxUserImage { XboxUserID = 600 + i, Kind = XboxUserImageKind.HaloEmblem, Status = XboxUserImageStatus.Found, ContentType = "image/png", Content = new byte[size], CheckedAt = _time.GetUtcNow().UtcDateTime });
            await Db.SaveChangesAsync();
            players.Add(player.Id);
        }
        return players;
    }

    private static string Emblem(int userID) => $"/api/users/{userID}/halo-emblem";

    [Test]
    public async Task HandleAsync_AReplyNamingMoreThanTenPictures_AttachesTenAndSendsTheText()
    {
        var players = await PlayersWithEmblems(12);
        var model = new ScriptedChatClient((TimeSpan.Zero, "All of them: " + string.Join(' ', players.Select(Emblem))));

        await Handler(model).HandleAsync(MessageToTheBot());

        var sent = _discord.Requests.Last(r => r.Method != HttpMethod.Get).Json.RootElement;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent.GetProperty("attachments").GetArrayLength(), Is.EqualTo(10));
            Assert.That(sent.GetProperty("content").GetString(), Is.EqualTo("All of them:"));
        }
    }

    [Test]
    public async Task HandleAsync_PicturesOverAMessagesSize_AreLeftOffPastTheLimit()
    {
        var players = await PlayersWithEmblems(3, size: 4_000_000);
        var model = new ScriptedChatClient((TimeSpan.Zero, "Big: " + string.Join(' ', players.Select(Emblem))));

        await Handler(model).HandleAsync(MessageToTheBot());

        var sent = _discord.Requests.Last(r => r.Method != HttpMethod.Get).Json.RootElement;
        Assert.That(sent.GetProperty("attachments").GetArrayLength(), Is.EqualTo(2));
    }

    [Test]
    public async Task HandleAsync_APictureDiscordRefuses_StillDeliversAndSavesTheText()
    {
        var players = await PlayersWithEmblems(1);
        _discord.FailWhenAttaching = true;
        var model = new ScriptedChatClient((TimeSpan.Zero, $"Here you go: {Emblem(players[0])}"));

        await Handler(model).HandleAsync(MessageToTheBot());

        var sent = _discord.Requests.Last(r => r.Method != HttpMethod.Get).Json.RootElement;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent.GetProperty("content").GetString(), Is.EqualTo("Here you go:"));
            Assert.That(sent.TryGetProperty("attachments", out var files) ? files.GetArrayLength() : 0, Is.Zero);
            Assert.That((await SavedReplies()).Single(), Is.EqualTo("Here you go:"));
        }
    }

    [Test]
    public async Task HandleAsync_APictureNotStored_IsLeftOutOfTheText()
    {
        var players = await PlayersWithEmblems(1);
        var model = new ScriptedChatClient((TimeSpan.Zero, $"Here: /api/users/{players[0]}/gamer-picture and https://evil.test/api/users/{players[0]}/halo-emblem"));

        await Handler(model).HandleAsync(MessageToTheBot());

        var sent = _discord.Requests.Last(r => r.Method != HttpMethod.Get).Json.RootElement;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent.GetProperty("content").GetString(), Is.EqualTo($"Here: and https://evil.test/api/users/{players[0]}/halo-emblem"));
            Assert.That(sent.TryGetProperty("attachments", out var files) ? files.GetArrayLength() : 0, Is.Zero, "Nothing stored, and no other host's address counts");
        }
    }

    [TestCase(1000)]
    [TestCase(1991)]
    public async Task HandleAsync_APictureInAnEarlierMessageOfALongReply_IsAttachedToTheLastAndLeftOutOfTheText(int addressAt)
    {
        var players = await PlayersWithEmblems(1);
        var text = new string('a', addressAt - 1) + " " + Emblem(players[0]) + " " + new string('b', 1500);
        var model = new ScriptedChatClient((TimeSpan.Zero, text), (TimeSpan.FromMilliseconds(1500), " more"), (TimeSpan.Zero, " end")) { Clock = _time };

        await Handler(model).HandleAsync(MessageToTheBot());

        var saved = await SavedReplies();
        var last = _discord.Requests.Last(r => r.Method != HttpMethod.Get).Json.RootElement;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved, Has.None.Contain("/api/users"));
            Assert.That(saved[0], Does.StartWith(new string('a', addressAt - 1)));
            Assert.That(last.GetProperty("attachments").GetArrayLength(), Is.EqualTo(1));
        }
    }
}
