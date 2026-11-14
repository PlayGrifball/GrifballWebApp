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
    private static readonly string[] ChannelQuestion = ["Channel question"];
    private static readonly string[] ChannelQuestionThenHello = ["Channel question", "hello"];
    private static readonly string[] PublicAnswer = ["Public answer"];

    private readonly RecordingLogger<MessageCreateHandler> _logger = new();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IAmbientCurrentUser _ambient = Substitute.For<IAmbientCurrentUser>();
    private readonly IGetsertXboxUserService _xboxUsers = Substitute.For<IGetsertXboxUserService>();
    private readonly IDataPullService _dataPull = Substitute.For<IDataPullService>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly IDiscordRestClient _rest = Substitute.For<IDiscordRestClient>();
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
            .AddTransient<GrifballWebApp.Application.Seasons.SeasonVisibility>()
            // get_season's
            .AddTransient<GrifballWebApp.Application.Seasons.SeasonService>()
            .AddTransient<GrifballWebApp.Application.Stats.SeasonStatsService>()
            .AddTransient<GrifballWebApp.Application.TeamStandings.TeamStandingsService>()
            .AddTransient<GrifballWebApp.Application.MatchPlanner.MatchPlannerService>()
            .AddTransient<GrifballWebApp.Application.Grades.GradesService>()
            .AddTransient<GrifballWebApp.Application.Signups.SignupsService>()
            .AddSingleton(Substitute.For<GrifballWebApp.Application.Brackets.IBracketService>())
            .BuildServiceProvider();
        var chatClients = Substitute.For<IChatClientFactory>();
        chatClients.Create().Returns(models[0], models[1..]);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Discord:ClientId"] = BotId.ToString() }).Build();
        return new MessageCreateHandler(_rest, config, _services.GetRequiredService<IServiceScopeFactory>(), _ambient, _logger, chatClients, _time);
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
}
