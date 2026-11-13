using GrifballWebApp.Application;
using GrifballWebApp.Application.Availability;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Home;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Search;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Server.Commands;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Signups;
using GrifballWebApp.Server.Teams;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using NetCord;
using NetCord.JsonModels;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;
using NSubstitute;
using User = GrifballWebApp.Domain.Players.User;
using DiscordUser = GrifballWebApp.Domain.Players.DiscordUser;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The server's NetCord modules, as Program.cs registers them (AddModules on the server assembly), run in-process: a
/// command or component interaction read from Discord's JSON is executed by NetCord's own services, which bind its
/// options and build the module from DI, and the module hands it to its handler (real, on a temp-file SQLite
/// GrifballContext). Each case is one the handler answers without going further (no such season, not signed up), so the
/// reply shows the interaction reached the right handler method with its options read. No host or gateway.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordModulesRoutingUnitTests : SqliteGrifballContextFixture
{
    private const ulong Stranger = 909090;

    private static readonly ApplicationCommandService<ApplicationCommandContext, AutocompleteInteractionContext> Commands = CommandsOf();
    private static readonly ComponentInteractionService<ButtonInteractionContext> Buttons = ComponentsOf<ButtonInteractionContext>();
    private static readonly ComponentInteractionService<StringMenuInteractionContext> StringMenus = ComponentsOf<StringMenuInteractionContext>();
    private static readonly ComponentInteractionService<ModalInteractionContext> Modals = ComponentsOf<ModalInteractionContext>();

    private static ApplicationCommandService<ApplicationCommandContext, AutocompleteInteractionContext> CommandsOf()
    {
        var service = new ApplicationCommandService<ApplicationCommandContext, AutocompleteInteractionContext>();
        service.AddModules(typeof(Program).Assembly);
        return service;
    }

    private static ComponentInteractionService<TContext> ComponentsOf<TContext>() where TContext : IComponentInteractionContext
    {
        var service = new ComponentInteractionService<TContext>();
        service.AddModules(typeof(Program).Assembly);
        return service;
    }

    private readonly FakeRestRequestHandler _rest = new();
    private readonly List<InteractionCallbackProperties> _callbacks = [];
    private ServiceProvider? _services;

    [TearDown]
    public async Task TearDownServices()
    {
        _rest.Dispose();
        if (_services is not null)
            await _services.DisposeAsync();
    }

    private Task<InteractionCallbackResponse?> SendResponse(IInteraction _, InteractionCallbackProperties callback, bool __, RestRequestProperties? ___, CancellationToken ____)
    {
        _callbacks.Add(callback);
        return Task.FromResult<InteractionCallbackResponse?>(null);
    }

    private RestClient Rest() => new(new RestClientConfiguration { RequestHandler = _rest });

    /// <summary>Each module's handler, real over the test's database; the services past the answers these cases reach stay null.</summary>
    private ServiceProvider Services()
    {
        var urls = Urls();
        var lookups = new CommandLookups(Db, new SeasonService(Db, new SeasonStatsService(Db)));
        var caller = new DiscordCaller(Db);
        var seasons = new SeasonService(Db, new SeasonStatsService(Db));
        var membership = new QueueMembership(Substitute.For<IQueueRepository>(), Db, Substitute.For<IQueueNotifier>());
        // A site account linked to another Discord account: /setgamertag and the modal stop there, saving nothing
        var users = Substitute.For<UserManager<User>>(Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);
        var elsewhere = User.Register("someoneElse");
        elsewhere.LinkDiscord(DiscordUser.FromDiscord(111, "other"));
        users.FindByLoginAsync("Discord", Stranger.ToString()).Returns(elsewhere);
        var recentMatches = new DiscordCommandsHandler(Db, new RecordingLogger<DiscordCommandsHandler>(), Substitute.For<IDataPullService>(), Substitute.For<IGetsertXboxUserService>());
        _services = new ServiceCollection()
            .AddSingleton<ApplicationCommandService<ApplicationCommandContext>>(Commands)
            .AddSingleton(new HelpCommandsHandler(Commands, urls))
            .AddSingleton(new AdminCommandsHandler(caller, lookups, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, urls))
            .AddSingleton(new PersonalCommandsHandler(caller, lookups, membership, Substitute.For<IQueueRepository>(), null!, new RegionService(Db), null!, null!,
                seasons, new AvailabilityService(Db), null!, urls))
            .AddSingleton(new BrowseCommandsHandler(
                new LeaderboardsService(Db, new SeasonStatsService(Db), new MatchmakingStatsService(Db), Substitute.For<IDistributedCache>(), new RecordingLogger<LeaderboardsService>()),
                new MatchmakingStatsService(Db), new HomeEventsService(Db), new QuickSearchService(Db), lookups, urls))
            .AddSingleton(new SeasonCommandsHandler(lookups, seasons, null!, null!, null!, null!, null!, urls))
            .AddSingleton(new PlayerCommandsHandler(lookups, caller, null!, null!, null!, null!, recentMatches, urls))
            .AddSingleton(new TeamCommandsHandler(lookups, null!, null!, null!, urls))
            .AddSingleton(new MatchCommandsHandler(lookups, null!, null!, urls))
            .AddSingleton(recentMatches)
            .AddSingleton(new DiscordSetGamertag(Db, users, Substitute.For<ISetGamertagService>()))
            .AddSingleton(new MatchmakingButtonHandler(membership, Substitute.For<IServiceScopeFactory>(), Db, null!, null!, new RecordingLogger<MatchmakingButtonHandler>()))
            .AddSingleton(new SignupButtonHandler(Db, new SignupsService(Db), urls))
            .AddSingleton(new MatchmakingStringMenuHandler(Db, null!, null!, null!))
            .AddSingleton(new DraftStringMenuHandler(Db, null!, new RecordingLogger<DraftStringMenuHandler>()))
            .BuildServiceProvider();
        return _services;
    }

    /// <summary>The reply: the deferred response's edit (its text, else its embed's title), else the message sent.</summary>
    private string? Reply()
    {
        var edit = _rest.Requests.LastOrDefault(r => r.Method == HttpMethod.Patch);
        if (edit is null)
            return ((InteractionCallbackProperties<InteractionMessageProperties>)_callbacks[^1]).Data.Content;
        using var json = edit.Json;
        return json.RootElement.TryGetProperty("content", out var content) && content.ValueKind == System.Text.Json.JsonValueKind.String
            ? content.GetString()
            : json.RootElement.GetProperty("embeds")[0].GetProperty("title").GetString();
    }

    private static (string Name, string Value)[] Options(string options)
        => options.Length == 0 ? [] : options.Split('|').Select(o => o.Split('=', 2)).Select(p => (p[0], p[1])).ToArray();

    [TestCase("admin dashboard", "", DiscordCaller.NoGamertag)]
    [TestCase("admin overdue", "", DiscordCaller.NoGamertag)]
    [TestCase("admin medals", "", DiscordCaller.NoGamertag)]
    [TestCase("admin match report", "match=Grifs|match_id=abc|replaces=2", DiscordCaller.NoGamertag)]
    [TestCase("admin match forfeit", "match=Grifs|side=0", DiscordCaller.NoGamertag)]
    [TestCase("admin match undo", "match=Grifs", DiscordCaller.NoGamertag)]
    [TestCase("admin match pull", "match=Grifs", DiscordCaller.NoGamertag)]
    [TestCase("admin match time", "match=Grifs|time=2026-11-20 20:00", DiscordCaller.NoGamertag)]
    [TestCase("admin season generate", "season=Winter|home_matches=1|best_of=3", DiscordCaller.NoGamertag)]
    [TestCase("admin bracket create", "season=Winter|participants=8|best_of=3|double_elimination=true", DiscordCaller.NoGamertag)]
    [TestCase("admin bracket seed", "", DiscordCaller.NoGamertag)]
    [TestCase("admin draft lock", "", DiscordCaller.NoGamertag)]
    [TestCase("admin draft unlock", "", DiscordCaller.NoGamertag)]
    [TestCase("admin schedule show", "", DiscordCaller.NoGamertag)]
    [TestCase("admin schedule run", "", DiscordCaller.NoGamertag)]
    [TestCase("admin schedule apply", "recommendation=1", DiscordCaller.NoGamertag)]
    [TestCase("admin halo pull", "match_id=abc", DiscordCaller.NoGamertag)]
    [TestCase("admin jobs list", "", DiscordCaller.NoGamertag)]
    [TestCase("admin jobs run", "job=Maintenance/cleanup", DiscordCaller.NoGamertag)]
    [TestCase("admin jobs pause", "job=Maintenance/cleanup|paused=true", DiscordCaller.NoGamertag)]
    [TestCase("admin claims list", "", DiscordCaller.NoGamertag)]
    [TestCase("admin claims approve", "claim=1", DiscordCaller.NoGamertag)]
    [TestCase("admin claims reject", "claim=1|reason=No", DiscordCaller.NoGamertag)]
    [TestCase("reschedule pending", "", DiscordCaller.NoGamertag)]
    [TestCase("reschedule decide", "request=1|approve=true|notes=Fine", DiscordCaller.NoGamertag)]
    [TestCase("reschedule request", "match=Grifs|reason=Away|new_time=2026-11-20 20:00", DiscordCaller.NoGamertag)]
    [TestCase("queue join", "", DiscordCaller.NoGamertag)]
    [TestCase("queue leave", "", DiscordCaller.NoGamertag)]
    [TestCase("queue status", "", "0 players are in the Late League queue.")]
    [TestCase("me region", "region=Europe", DiscordCaller.NoGamertag)]
    [TestCase("me claim", "gamertag=OldTag", DiscordCaller.NoGamertag)]
    [TestCase("me claims", "", DiscordCaller.NoGamertag)]
    [TestCase("me logins", "", DiscordCaller.NoGamertag)]
    [TestCase("signup", "season=Winter|team_name=Grifs|will_captain=true|draft_help=false", DiscordCaller.NoGamertag)]
    [TestCase("availability show", "season=nonsense", CommandLookups.NoSuchSeason)]
    [TestCase("leaderboard top", "", "All-time Rings")]
    [TestCase("leaderboard rank", "player=nonsense", PlayerCommandsHandler.NoSuchPlayer)]
    [TestCase("lateleague seasons", "", "Late League seasons")]
    [TestCase("lateleague season", "season=nonsense", BrowseCommandsHandler.NoSuchLateLeagueSeason)]
    [TestCase("events", "", "Upcoming events")]
    [TestCase("search", "query=winter", "Search: winter")]
    [TestCase("season list", "", "Seasons")]
    [TestCase("season info", "season=nonsense", CommandLookups.NoSuchSeason)]
    [TestCase("season standings", "season=nonsense", CommandLookups.NoSuchSeason)]
    [TestCase("season schedule", "season=nonsense", CommandLookups.NoSuchSeason)]
    [TestCase("season stats", "season=nonsense", CommandLookups.NoSuchSeason)]
    [TestCase("season grades", "season=nonsense", CommandLookups.NoSuchSeason)]
    [TestCase("season signups", "season=nonsense", CommandLookups.NoSuchSeason)]
    [TestCase("season bracket", "season=nonsense", CommandLookups.NoSuchSeason)]
    [TestCase("season draft", "season=nonsense", CommandLookups.NoSuchSeason)]
    [TestCase("player profile", "player=nonsense", PlayerCommandsHandler.NoSuchPlayer)]
    [TestCase("player stats", "player=nonsense", PlayerCommandsHandler.NoSuchPlayer)]
    [TestCase("player commendations", "player=nonsense", PlayerCommandsHandler.NoSuchPlayer)]
    [TestCase("player history", "player=nonsense", PlayerCommandsHandler.NoSuchPlayer)]
    [TestCase("player vods", "player=nonsense", PlayerCommandsHandler.NoSuchPlayer)]
    [TestCase("player matches", "player=nonsense", PlayerCommandsHandler.NoSuchPlayer)]
    [TestCase("team info", "team=nonsense", TeamCommandsHandler.NoSuchTeam)]
    [TestCase("team roster", "team=nonsense", TeamCommandsHandler.NoSuchTeam)]
    [TestCase("team history", "team=nonsense", TeamCommandsHandler.NoSuchTeam)]
    [TestCase("team matches", "team=nonsense", TeamCommandsHandler.NoSuchTeam)]
    [TestCase("team vods", "team=nonsense", TeamCommandsHandler.NoSuchTeam)]
    [TestCase("match info", "match=nonsense", CommandLookups.NoSuchMatch)]
    [TestCase("match game", "match_id=not-a-guid", MatchCommandsHandler.NoSuchGame)]
    [TestCase("matches", "", "You must set your gamertag first, or provide someone else's gamertag to search for")]
    [TestCase("setgamertag", "gamertag=NewTag", DiscordSetGamertag.LinkedToOtherDiscord)]
    public async Task ACommand_ReachesItsHandler_WithItsOptions(string path, string options, string expected)
    {
        var services = Services();
        var interaction = new SlashCommandInteraction(DiscordInteractionJson.SubCommand(path, Stranger, Options(options)), null, SendResponse, Rest());

        var result = await Commands.ExecuteAsync(new ApplicationCommandContext(interaction, null!), services);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.InstanceOf<IFailResult>(), (result as IFailResult)?.Message);
            Assert.That(Reply(), Is.EqualTo(expected));
        }
    }

    [Test]
    public async Task Help_ListsTheCommands()
    {
        var services = Services();
        var interaction = new SlashCommandInteraction(DiscordInteractionJson.SubCommand("help", Stranger), null, SendResponse, Rest());

        var result = await Commands.ExecuteAsync(new ApplicationCommandContext(interaction, null!), services);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.InstanceOf<IFailResult>(), (result as IFailResult)?.Message);
            Assert.That(Reply(), Is.EqualTo("Commands"));
        }
    }

    [TestCase("join_queue", "You must set your gamertag first")]
    [TestCase("leave_queue", "You are not in the Late League queue.")]
    [TestCase("voteforwinner:1:Home", "You must set your gamertag first")]
    [TestCase("signup:1", "You must set your gamertag first")]
    public async Task AButton_ReachesItsHandler_WithItsArguments(string customId, string expected)
    {
        var services = Services();
        var interaction = new ButtonInteraction(DiscordInteractionJson.Button(customId, Stranger), null, SendResponse, Rest());

        var result = await Buttons.ExecuteAsync(new ButtonInteractionContext(interaction, null!), services);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.InstanceOf<IFailResult>(), (result as IFailResult)?.Message);
            Assert.That(Reply(), Is.EqualTo(expected));
        }
    }

    [Test]
    public async Task TheSetGamertagButton_OpensItsModal()
    {
        var services = Services();
        var interaction = new ButtonInteraction(DiscordInteractionJson.Button("set_gamertag", Stranger), null, SendResponse, Rest());

        var result = await Buttons.ExecuteAsync(new ButtonInteractionContext(interaction, null!), services);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.InstanceOf<IFailResult>(), (result as IFailResult)?.Message);
            Assert.That(_callbacks.Single().Type, Is.EqualTo(InteractionCallbackType.Modal));
        }
    }

    [TestCase("votetokick:1")]
    [TestCase("draftpick:1:2:0")]
    public async Task AStringMenu_ReachesItsHandler_WithItsArguments(string customId)
    {
        var services = Services();
        var interaction = new StringMenuInteraction(DiscordInteractionJson.StringMenu(customId, Stranger, "5"), null, SendResponse, Rest());

        var result = await StringMenus.ExecuteAsync(new StringMenuInteractionContext(interaction, null!), services);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.InstanceOf<IFailResult>(), (result as IFailResult)?.Message);
            Assert.That(Reply(), Is.EqualTo("You must set your gamertag first"));
        }
    }

    [Test]
    public async Task TheSetGamertagModal_ReadsItsTextInput()
    {
        var services = Services();
        var interaction = new ModalInteraction(DiscordInteractionJson.Modal("set_gamertag", "gamertag", "NewTag", Stranger), null, SendResponse, Rest());

        var result = await Modals.ExecuteAsync(new ModalInteractionContext(interaction, null!), services);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.InstanceOf<IFailResult>(), (result as IFailResult)?.Message);
            Assert.That(Reply(), Is.EqualTo(DiscordSetGamertag.LinkedToOtherDiscord));
        }
    }
}
