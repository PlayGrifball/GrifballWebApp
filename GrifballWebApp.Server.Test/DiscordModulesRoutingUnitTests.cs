using GrifballWebApp.Application;
using GrifballWebApp.Application.Availability;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.Grades;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.Home;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Application.Search;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.TeamPage;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.SeasonMatchPage;
using GrifballWebApp.Server.Commands;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Signups;
using GrifballWebApp.Server.Teams;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;
using NSubstitute;
using Quartz;
using Quartz.Extensibility;
using MsOptions = Microsoft.Extensions.Options.Options;
using Season = GrifballWebApp.Domain.League.Season;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The server's NetCord modules, as Program.cs registers them (AddModules on the server assembly), run in-process: a
/// command or component interaction read from Discord's JSON is executed by NetCord's own services, which bind its
/// options and build the module from DI, and the module hands it to its handler, real over a temp-file SQLite
/// GrifballContext (Quartz, Discord's REST and the Halo pull substituted). The caller is staff with a gamertag, and each
/// case is set up so its handler method gives a reply no other method gives: the reply shows the interaction reached the
/// right method, with its options bound to the right parameters. No host or gateway.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordModulesRoutingUnitTests : SqliteGrifballContextFixture
{
    private const ulong CallerDiscordID = 909090;
    private const string NotSignedUp = "77";
    private static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly JobKey Cleanup = new("cleanup", "Maintenance");
    private static readonly JobKey PullJob = OnDemandJobs.JobKeyOf(typeof(SeasonMatchPullJob));

    private static readonly ApplicationCommandService<ApplicationCommandContext, AutocompleteInteractionContext> SlashCommands = CommandsOf();
    private static readonly ComponentInteractionService<ButtonInteractionContext> ButtonModules = ComponentsOf<ButtonInteractionContext>();
    private static readonly ComponentInteractionService<StringMenuInteractionContext> StringMenuModules = ComponentsOf<StringMenuInteractionContext>();
    private static readonly ComponentInteractionService<ModalInteractionContext> ModalModules = ComponentsOf<ModalInteractionContext>();

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
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));
    private readonly ISetGamertagService _setGamertag = Substitute.For<ISetGamertagService>();
    private ServiceProvider? _services;

    /// <summary>The seeded rows the cases name: the caller, the season, the match, two pending claims, the pull job.</summary>
    private Dictionary<string, string> _seeded = [];

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

    /// <summary>
    /// A staff caller (Sysadmin, Commissioner, Player) with a gamertag; season Winter, its signups open, with teams Grifs
    /// and Rivals and a match between them; two pending gamertag claims.
    /// </summary>
    private async Task Seed()
    {
        var caller = await AddUser(CallerDiscordID, "Staffer", "Sysadmin", "Commissioner", "Player");
        var season = Season.Seed(seasonName: "Winter").WithDates(signupsOpen: Now.AddDays(-1), signupsClose: Now.AddDays(5), draftStart: Now.AddDays(8),
            seasonStart: Now.AddDays(10), seasonEnd: Now.AddDays(60));
        Db.Seasons.Add(season);
        await Db.SaveChangesAsync();
        var match = await AddSeasonMatch(season, await AddTeam(season, "Grifs"), await AddTeam(season, "Rivals"));
        var claimant = await AddDiscordOnlyUser(7, "Claimant");
        var approve = GamertagClaim.ClaimedByUser(claimantUserID: claimant.Id, gamertag: "OldTagA", normalizedGamertag: "OLDTAGA");
        var reject = GamertagClaim.ClaimedByUser(claimantUserID: claimant.Id, gamertag: "OldTagB", normalizedGamertag: "OLDTAGB");
        Db.Set<GamertagClaim>().AddRange(approve, reject);
        await Db.SaveChangesAsync();
        _seeded = new()
        {
            ["{caller}"] = caller.Id.ToString(),
            ["{season}"] = season.SeasonID.ToString(),
            ["{match}"] = match.SeasonMatchID.ToString(),
            ["{claimA}"] = approve.GamertagClaimID.ToString(),
            ["{claimB}"] = reject.GamertagClaimID.ToString(),
            ["{pullJob}"] = $"{PullJob.Group}/{PullJob.Name}",
        };
    }

    private string Fill(string text) => _seeded.Aggregate(text, (filled, row) => filled.Replace(row.Key, row.Value, StringComparison.Ordinal));

    /// <summary>Each module's handler over the test's database: the services real, past what touches Quartz, Discord or Halo.</summary>
    private ServiceProvider Services()
    {
        var urls = Urls();
        var stats = new SeasonStatsService(Db);
        var seasons = new SeasonService(Db, stats, _time);
        var lookups = new CommandLookups(Db, seasons);
        var caller = new DiscordCaller(Db);
        var queue = Substitute.For<IQueueRepository>();
        // In the queue already: /queue join and its button are refused; /queue leave too, as nothing is removed
        queue.PlayerState(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(c => new QueuePlayerState(c.Arg<int>(), true, InQueue: true, InMatch: false, QueueCount: 1));
        queue.PlayerStateByDiscord((long)CallerDiscordID, Arg.Any<CancellationToken>()).Returns(new QueuePlayerState(1, true, InQueue: true, InMatch: false, QueueCount: 1));
        var membership = new QueueMembership(queue, Db, Substitute.For<IQueueNotifier>());
        var (schedulers, history) = QuartzStore();
        var brackets = Substitute.For<IBracketService>();
        brackets.GetBracketsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new BracketDto { WinnerRounds = [], LoserRounds = [] });
        var dataPull = Substitute.For<IDataPullService>();
        var seasonMatches = new SeasonMatchService(Db, dataPull, brackets, new ChampionshipService(Db));
        var teams = new TeamService(Db, Substitute.For<IPublisher>(), Substitute.For<IDraftChannel>(), Substitute.For<IDatabaseLocks>());
        var reschedules = new RescheduleService(Db, Substitute.For<IOutbox>());
        var signups = new SignupsService(Db, _time);
        var vods = new VodService(Db, null!, MsOptions.Create(new YouTubeOptions()), null!);
        var leaderboards = new LeaderboardsService(Db, stats, new MatchmakingStatsService(Db), Substitute.For<IDistributedCache>(), new RecordingLogger<LeaderboardsService>());
        var recentMatches = new DiscordCommandsHandler(Db, new RecordingLogger<DiscordCommandsHandler>(), dataPull, Substitute.For<IGetsertXboxUserService>());
        _setGamertag.SetGamertag(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        var admin = new AdminCommandsHandler(caller, lookups, reschedules, seasonMatches, new MatchPlannerService(Db), brackets, teams,
            new ScheduleRecommendations(Db, _time), new OnDemandJobs(schedulers, history),
            new JobsService(schedulers, history, new RecurringJobs().AddOnDemand<SeasonMatchPullJob>("season-match-pull", "Pulls a match's games"), _time),
            new GamertagClaimService(Db, Substitute.For<IUserMergeService>(), null, _time), dataPull, urls);
        _services = new ServiceCollection()
            .AddSingleton<ApplicationCommandService<ApplicationCommandContext>>(SlashCommands)
            .AddSingleton(new HelpCommandsHandler(SlashCommands, urls))
            .AddSingleton(admin)
            .AddSingleton(new PersonalCommandsHandler(caller, lookups, membership, queue, null!, new RegionService(Db),
                new UserGamertagClaimService(Db, new GamertagMatching(Db), _time, MsOptions.Create(new UserClaimOptions())), signups, seasons,
                new AvailabilityService(Db), reschedules, urls))
            .AddSingleton(new BrowseCommandsHandler(leaderboards, new MatchmakingStatsService(Db), new HomeEventsService(Db, _time), new QuickSearchService(Db, _time), lookups, urls))
            .AddSingleton(new SeasonCommandsHandler(lookups, seasons, new TeamStandingsService(Db), new MatchPlannerService(Db), new GradesService(Db), signups, brackets, urls))
            .AddSingleton(new PlayerCommandsHandler(lookups, caller, new ProfileStatsService(Db, stats, new MatchmakingStatsService(Db)), new ProfileCommendationsService(Db),
                new DeploymentHistoryService(Db, _time), vods, recentMatches, urls))
            .AddSingleton(new TeamCommandsHandler(lookups, new TeamPageService(Db, _time), new TeamHistoryService(Db, _time), vods, urls))
            .AddSingleton(new MatchCommandsHandler(lookups, seasonMatches, new MatchDetailsService(Db), urls))
            .AddSingleton(recentMatches)
            .AddSingleton(new DiscordSetGamertag(Db, Substitute.For<UserManager<User>>(Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null), _setGamertag))
            .AddSingleton(new MatchmakingButtonHandler(membership, Substitute.For<IServiceScopeFactory>(), Db, null!, null!, new RecordingLogger<MatchmakingButtonHandler>()))
            .AddSingleton(new SignupButtonHandler(Db, signups, urls))
            .AddSingleton(new MatchmakingStringMenuHandler(Db, null!, null!, null!))
            .AddSingleton(new DraftStringMenuHandler(Db, teams, new RecordingLogger<DraftStringMenuHandler>()))
            .BuildServiceProvider();
        return _services;
    }

    /// <summary>
    /// Quartz's store as a worker left it: nothing running, each on-demand run queued (a request adds nothing), the recurring
    /// job Maintenance/cleanup and the season match pull's on-demand job there.
    /// </summary>
    private static (ISchedulerFactory Schedulers, IExecutionHistoryStore History) QuartzStore()
    {
        var schedulers = Substitute.For<ISchedulerFactory>();
        var scheduler = Substitute.For<IScheduler>();
        schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(scheduler);
        scheduler.SchedulerName.Returns("routing-test");
        scheduler.QueryFireInstances(Arg.Any<FireInstanceQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<FireInstance>([], false));
        scheduler.GetTriggerState(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(TriggerState.Normal);
        scheduler.GetTrigger(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns((ITrigger?)null);
        scheduler.QueryJobs(Arg.Any<JobQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobHeader>([new JobHeader(Cleanup, "Cleans up", "Cleanup", true, false, false, false)], false));
        scheduler.QueryClusterNodes(Arg.Any<CancellationToken>()).Returns(new List<ClusterNode>());
        scheduler.Exists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>()).Returns(true);
        var history = Substitute.For<IExecutionHistoryStore>();
        history.QueryExecutions(Arg.Any<ExecutionHistoryQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<ExecutionHistoryEntry>([], false));
        history.QueryJobRunStatuses(Arg.Any<JobRunStatusQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<JobRunStatus>([], false));
        return (schedulers, history);
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

    private (string Name, string Value)[] OptionsOf(string options)
        => options.Length == 0 ? [] : Fill(options).Split('|').Select(o => o.Split('=', 2)).Select(p => (p[0], p[1])).ToArray();

    /// <summary>The interaction's result was a success and its reply <paramref name="expected"/>; or, for an expected "!message",
    /// the handler threw that (the pipeline replies with it).</summary>
    private void AssertReplied(IExecutionResult result, string expected)
    {
        using (Assert.EnterMultipleScope())
        {
            if (expected.StartsWith('!'))
            {
                Assert.That(result, Is.InstanceOf<IExceptionResult>());
                Assert.That((result as IExceptionResult)?.Exception.Message, Is.EqualTo(expected[1..]));
            }
            else
            {
                Assert.That(result, Is.Not.InstanceOf<IFailResult>(), (result as IFailResult)?.Message);
                Assert.That(Reply(), Is.EqualTo(Fill(expected)));
            }
        }
    }

    [TestCase("admin dashboard", "", "Commissioner dashboard")]
    [TestCase("admin overdue", "", "Overdue matches")]
    [TestCase("admin medals", "", "The medals are refreshed.")]
    [TestCase("admin match report", "match={match}|match_id=not-a-guid|replaces=2", MatchCommandsHandler.NoSuchGame)]
    [TestCase("admin match forfeit", "match={match}|side=1", "Forfeited (away).")]
    [TestCase("admin match undo", "match={match}", "!This match has no result to undo")]
    [TestCase("admin match pull", "match={match}", "The pull is queued. Its games show on [the match page](https://grif.test/seasonmatch/{match}).")]
    [TestCase("admin match time", "match={match}|time=2026-11-20 20:00", "The match is set for <t:1795204800>.")]
    [TestCase("admin season generate", "season=Winter|home_matches=1|best_of=3", "The matches are made. Schedule them on [the season page](https://grif.test/season/{season}).")]
    [TestCase("admin bracket create", "season=Winter|participants=8|best_of=3|double_elimination=true", "The bracket is made. Seed it with /admin bracket seed, or set custom seeds on the site.")]
    [TestCase("admin bracket seed", "season=Winter", "The bracket is seeded by the standings.")]
    [TestCase("admin draft lock", "season=Winter", "The captains are locked.")]
    [TestCase("admin draft unlock", "season=Winter", "The captains are unlocked.")]
    [TestCase("admin schedule show", "season=Winter", "Schedule (queued)")]
    [TestCase("admin schedule run", "season=Winter", "The recommendation is queued. See it with /admin schedule show.")]
    [TestCase("admin schedule apply", "recommendation=999", "That recommendation doesn't exist or was applied already.")]
    [TestCase("admin halo pull", "match_id=6f0c1c7e-1a52-4c11-9d7e-3f1c2b9a0a01", "Pulled: [the game](https://grif.test/match/6f0c1c7e-1a52-4c11-9d7e-3f1c2b9a0a01).")]
    [TestCase("admin jobs list", "", "Jobs")]
    [TestCase("admin jobs run", "job=Maintenance/cleanup", AdminCommandsHandler.Done)]
    [TestCase("admin jobs pause", "job={pullJob}|paused=true", "The job can't do that now (it is running or paused).")]
    [TestCase("admin claims list", "", "Pending gamertag claims")]
    [TestCase("admin claims approve", "claim={claimA}", "Approved.")]
    [TestCase("admin claims reject", "claim={claimB}|reason=No", "Rejected.")]
    [TestCase("reschedule pending", "", "Pending reschedules")]
    [TestCase("reschedule decide", "request=999|approve=true|notes=Fine", "Reschedule request not found")]
    [TestCase("reschedule request", "match={match}|reason=Away|new_time=2026-11-20 20:00", "Your reschedule request is in, for <t:1795204800>. A commissioner will decide it.")]
    [TestCase("queue join", "", "You are already in the Late League queue!")]
    [TestCase("queue leave", "", "You are not in the Late League queue.")]
    [TestCase("queue status", "", "1 player is in the Late League queue.\nYou are in the queue.")]
    [TestCase("me region", "region=Europe", "Your region is set.")]
    [TestCase("me claim", "gamertag=OldTag", "No past records use that gamertag")]
    [TestCase("me claims", "", "Your gamertag claims")]
    [TestCase("me logins", "", "Your sign-ins")]
    [TestCase("signup", "season=Winter|team_name=Grifs|will_captain=true|draft_help=false",
        "You have signed up for Winter. Set the times you can play on [the signup form](https://grif.test/login?followUp=/season/{season}/signupForm).")]
    [TestCase("availability show", "season=Winter", "Winter availability")]
    [TestCase("leaderboard top", "", "All-time Rings")]
    [TestCase("leaderboard rank", "player=Staffer", "Staffer: all-time ranks")]
    [TestCase("lateleague seasons", "", "Late League seasons")]
    [TestCase("lateleague season", "season=nonsense", BrowseCommandsHandler.NoSuchLateLeagueSeason)]
    [TestCase("events", "", "Upcoming events")]
    [TestCase("search", "query=winter", "Search: winter")]
    [TestCase("season list", "", "Seasons")]
    [TestCase("season info", "season=Winter", "Winter")]
    [TestCase("season standings", "season=Winter", "Winter standings")]
    [TestCase("season schedule", "season=Winter", "Winter schedule")]
    [TestCase("season stats", "season=Winter", "Winter: top Goals")]
    [TestCase("season grades", "season=Winter", "Winter player grades")]
    [TestCase("season signups", "season=Winter", "Winter signups: 0")]
    [TestCase("season bracket", "season=Winter", "Winter bracket")]
    [TestCase("season draft", "season=Winter", "Winter draft")]
    [TestCase("player profile", "player=Staffer", "Staffer")]
    [TestCase("player stats", "player=Staffer", "Staffer stats")]
    [TestCase("player commendations", "player=Staffer", "Staffer commendations")]
    [TestCase("player history", "player=Staffer", "Staffer deployment history")]
    [TestCase("player vods", "player=Staffer", "Staffer VODs")]
    [TestCase("team info", "team=Grifs", "Grifs")]
    [TestCase("team roster", "team=Grifs", "Grifs roster")]
    [TestCase("team history", "team=Grifs", "Grifs history")]
    [TestCase("team matches", "team=Grifs", "Grifs matches")]
    [TestCase("team vods", "team=Grifs", "Grifs VODs")]
    [TestCase("match info", "match={match}", "Grifs vs Rivals")]
    [TestCase("match game", "match_id=not-a-guid", MatchCommandsHandler.NoSuchGame)]
    [TestCase("matches", "", "I found no matches for Staffer")]
    [TestCase("help", "", "Commands")]
    public async Task ACommand_ReachesItsHandlerMethod_WithItsOptions(string path, string options, string expected)
    {
        await Seed();
        var services = Services();
        var interaction = new SlashCommandInteraction(DiscordInteractionJson.SubCommand(path, CallerDiscordID, OptionsOf(options)), null, SendResponse, Rest());

        var result = await SlashCommands.ExecuteAsync(new ApplicationCommandContext(interaction, null!), services);

        AssertReplied(result, expected);
    }

    [Test]
    public async Task SetGamertag_SetsTheGamertagTyped_ForTheCaller()
    {
        await Seed();
        var services = Services();
        var interaction = new SlashCommandInteraction(DiscordInteractionJson.SubCommand("setgamertag", CallerDiscordID, OptionsOf("gamertag=NewTag")), null, SendResponse, Rest());

        var result = await SlashCommands.ExecuteAsync(new ApplicationCommandContext(interaction, null!), services);

        AssertReplied(result, "Gamertag has been set");
        await _setGamertag.Received(1).SetGamertag(int.Parse(_seeded["{caller}"]), "NewTag", Arg.Any<CancellationToken>());
    }

    [TestCase("join_queue", "You are already in the Late League queue!")]
    [TestCase("leave_queue", "You are not in the Late League queue.")]
    [TestCase("voteforwinner:999:Home", "Match does not exist or it is no longer active")]
    [TestCase("signup:{season}", "You have signed up for the season. Remember you can do more things like set your availability [here](https://grif.test/login?followUp=/season/{season}/signupForm)")]
    public async Task AButton_ReachesItsHandlerMethod_WithItsArguments(string customId, string expected)
    {
        await Seed();
        var services = Services();
        var interaction = new ButtonInteraction(DiscordInteractionJson.Button(Fill(customId), CallerDiscordID), null, SendResponse, Rest());

        var result = await ButtonModules.ExecuteAsync(new ButtonInteractionContext(interaction, null!), services);

        AssertReplied(result, expected);
    }

    [Test]
    public async Task TheSetGamertagButton_OpensItsModal()
    {
        await Seed();
        var services = Services();
        var interaction = new ButtonInteraction(DiscordInteractionJson.Button("set_gamertag", CallerDiscordID), null, SendResponse, Rest());

        var result = await ButtonModules.ExecuteAsync(new ButtonInteractionContext(interaction, null!), services);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.InstanceOf<IFailResult>(), (result as IFailResult)?.Message);
            Assert.That(_callbacks.Single().Type, Is.EqualTo(InteractionCallbackType.Modal));
        }
    }

    [TestCase("votetokick:999", "This match does not exist")]
    [TestCase("draftpick:{season}:{caller}:0", "Hey <@909090>, This player has not signed up")]
    public async Task AStringMenu_ReachesItsHandlerMethod_WithItsArguments(string customId, string expected)
    {
        await Seed();
        var services = Services();
        var interaction = new StringMenuInteraction(DiscordInteractionJson.StringMenu(Fill(customId), CallerDiscordID, NotSignedUp), null, SendResponse, Rest());

        var result = await StringMenuModules.ExecuteAsync(new StringMenuInteractionContext(interaction, null!), services);

        AssertReplied(result, expected);
    }

    [Test]
    public async Task TheSetGamertagModal_SetsTheGamertagInItsTextInput()
    {
        await Seed();
        var services = Services();
        var interaction = new ModalInteraction(DiscordInteractionJson.Modal("set_gamertag", "gamertag", "NewTag", CallerDiscordID), null, SendResponse, Rest());

        var result = await ModalModules.ExecuteAsync(new ModalInteractionContext(interaction, null!), services);

        AssertReplied(result, "Gamertag has been set");
        await _setGamertag.Received(1).SetGamertag(int.Parse(_seeded["{caller}"]), "NewTag", Arg.Any<CancellationToken>());
    }
}
