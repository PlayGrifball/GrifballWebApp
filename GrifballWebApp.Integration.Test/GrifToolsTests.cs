using System.Net;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.Grades;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Search;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.TeamPage;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Mcp;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Database;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Team = GrifballWebApp.Domain.League.Team;
using User = GrifballWebApp.Domain.Players.User;
using ToolDescription = System.ComponentModel.DescriptionAttribute;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The agents' tools (<see cref="GrifTools"/>): each class against a database, as the MCP server calls them, and the rules every
/// tool keeps (a description on it and on each parameter, a unique snake_case name).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GrifToolsTests
{
    private GrifballContext _context = null!;
    // Inside the seasons AddSeason makes (2025-01-01 to 2025-03-01)
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 2, 1, 12, 0, 0, TimeSpan.Zero));
    private SeasonService _seasons = null!;
    private Season _season = null!;
    private Team _team = null!;
    private User _captain = null!;

    private static readonly string[] Alpha = ["Alpha"];
    private static readonly string[] PlayerRole = ["Player"];
    private static readonly string[] Pullers = ["Player", "Commissioner", "Sysadmin"];
    private static readonly string[] Staff = ["Commissioner", "Sysadmin"];
    private static readonly string[] CommissionerOnly = ["Commissioner"];
    private static readonly string[] SysadminOnly = ["Sysadmin"];

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
        _seasons = new SeasonService(_context, new SeasonStatsService(_context), new GrifballWebApp.Application.Seasons.SeasonVisibility(_context, _time), _time);
        _season = await _context.AddSeason("Spring");
        _captain = await _context.AddUser("cap", gamertag: "CaptainGT");
        _team = await _context.AddTeam(_season, "Alpha", _captain, 1);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private SeasonTools SeasonTools(ToolCaller? caller = null) => new(caller ?? ToolCaller.Anonymous, _seasons, new GrifballWebApp.Application.Seasons.SeasonVisibility(_context, _time), new TeamStandingsService(_context), new MatchPlannerService(_context), new GradesService(_context),
        new SignupsService(_context, _time), Substitute.For<IBracketService>());

    private VodService Vods() => new(_context, new YouTubeClient(new HttpClient(new FakeSourcesHandler(_ => FakeSourcesHandler.Status(HttpStatusCode.NotFound)))),
        Options.Create(new YouTubeOptions()), NullLogger<VodService>.Instance);

    private LeaderboardsService Leaderboards() => new(_context, new SeasonStatsService(_context), new MatchmakingStatsService(_context),
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), NullLogger<LeaderboardsService>.Instance);

    private PlayerTools PlayerTools(ToolCaller caller) => new(caller,
        new ProfileStatsService(_context, new SeasonStatsService(_context), new MatchmakingStatsService(_context), _time),
        new ProfileCommendationsService(_context), new DeploymentHistoryService(_context, _time), Vods(), Leaderboards());

    private MatchTools MatchTools(ToolCaller caller) => new(caller,
        new SeasonMatchService(_context, Substitute.For<IDataPullService>(),
            new BracketService(_context, new TeamStandingsService(_context), new ChampionshipService(_context)), new ChampionshipService(_context)),
        new MatchDetailsService(_context), new GrifballWebApp.Application.Seasons.SeasonVisibility(_context, _time));

    [Test]
    public async Task ASeasonTool_UsesTheRunningSeason_WhenGivenNone()
    {
        var standings = await SeasonTools().GetSeasonStandings();
        var summary = await SeasonTools().GetSeason();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(standings.Select(s => s.TeamName), Is.EqualTo(Alpha));
            Assert.That(summary.SeasonName, Is.EqualTo("Spring"));
        }
    }

    [Test]
    public async Task ASeasonTool_RefusesAnUnknownSeason_AndBetweenSeasonsUsesTheLatest()
    {
        var unknown = await Assert.ThrowsAsync<ToolException>(() => SeasonTools().GetSeasonSchedule(_season.SeasonID + 1000));
        Assert.That(unknown!.Message, Does.Contain($"no season {_season.SeasonID + 1000}").And.Contain("list_seasons"));

        _time.SetUtcNow(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        // Between seasons: the latest, as the slash commands and the season pages show
        var latest = await SeasonTools().GetSeasonStandings();
        var byID = await SeasonTools().GetSeasonStandings(_season.SeasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(latest.Select(s => s.TeamName), Is.EqualTo(Alpha));
            Assert.That(byID.Select(s => s.TeamName), Is.EqualTo(Alpha), "a season given by ID still answers");
        }
    }

    [Test]
    public async Task EverySeasonPage_ReadsTheRunningSeason()
    {
        var tools = SeasonTools();

        var schedule = await tools.GetSeasonSchedule();
        var stats = await tools.GetSeasonPlayerStats();
        var grades = await tools.GetSeasonGrades();
        var signups = await tools.GetSeasonSignups();
        await tools.GetSeasonBracket();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(schedule.Scheduled, Is.Empty);
            Assert.That(schedule.Unscheduled, Is.Empty);
            Assert.That(stats, Is.Empty);
            Assert.That(grades.Totals, Is.Empty);
            Assert.That(signups, Is.Empty);
        }
    }

    [Test]
    public async Task ListSeasons_ListsThem()
    {
        var seasons = await SeasonTools().ListSeasons();

        Assert.That(seasons.Results.Select(s => s.SeasonName), Does.Contain("Spring"));
    }

    [Test]
    public async Task TeamTools_ShowATeam_AndRefuseAnUnknownOne()
    {
        var tools = new TeamTools(ToolCaller.Anonymous, new GrifballWebApp.Application.Seasons.SeasonVisibility(_context, _time), new TeamPageService(_context, _time), new TeamHistoryService(_context, new GrifballWebApp.Application.Seasons.SeasonVisibility(_context, _time), _time), Vods());

        var team = await tools.GetTeam(_team.TeamID);
        var matches = await tools.GetTeamMatches(_team.TeamID);
        var history = await tools.GetTeamHistory(_team.TeamID);
        var vods = await tools.GetTeamVods(_team.TeamID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(team.TeamName, Is.EqualTo("Alpha"));
            Assert.That(matches, Is.Empty);
            Assert.That(history.Seasons.Select(t => t.TeamName), Is.EqualTo(Alpha));
            Assert.That(vods, Is.Empty);
            Assert.That((await Assert.ThrowsAsync<ToolException>(() => tools.GetTeam(_team.TeamID + 1000)))!.Message, Does.Contain("search"));
            await Assert.ThrowsAsync<ToolException>(() => tools.GetTeamMatches(_team.TeamID + 1000));
            await Assert.ThrowsAsync<ToolException>(() => tools.GetTeamHistory(_team.TeamID + 1000));
            await Assert.ThrowsAsync<ToolException>(() => tools.GetTeamVods(_team.TeamID + 1000));
        }
    }

    [Test]
    public async Task PlayerTools_AreTheCallersOwn_WhenNoPlayerIsNamed()
    {
        var tools = PlayerTools(new ToolCaller(_captain.Id, ["Player"]));

        var me = await tools.WhoAmI();
        var profile = await tools.GetPlayer();
        var history = await tools.GetPlayerHistory();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(me.UserID, Is.EqualTo(_captain.Id));
            Assert.That(me.Gamertag, Is.EqualTo("CaptainGT"));
            Assert.That(me.Roles, Is.EqualTo(PlayerRole));
            Assert.That(profile.UserID, Is.EqualTo(_captain.Id));
            Assert.That(history.Select(h => h.TeamName), Is.EqualTo(Alpha));
        }
    }

    [Test]
    public async Task PlayerTools_RefuseAnUnknownPlayer()
    {
        var tools = PlayerTools(new ToolCaller(_captain.Id, []));
        var unknown = _captain.Id + 1000;

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await Assert.ThrowsAsync<ToolException>(() => tools.GetPlayer(unknown)))!.Message, Does.Contain($"no player {unknown}"));
            await Assert.ThrowsAsync<ToolException>(() => tools.GetPlayerCommendations(unknown));
            await Assert.ThrowsAsync<ToolException>(() => tools.GetPlayerHistory(unknown));
            await Assert.ThrowsAsync<ToolException>(() => tools.GetPlayerVods(unknown));
            Assert.That((await Assert.ThrowsAsync<ToolException>(() => tools.GetPlayerRank(gamertag: "NobodyHasThis")))!.Message, Does.Contain("NobodyHasThis"));
        }
        var mine = await tools.GetPlayerRank();
        var byID = await tools.GetPlayerRank(userID: _captain.Id);
        var nobody = await PlayerTools(new ToolCaller(unknown, [])).WhoAmI();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(mine.UserID, Is.EqualTo(_captain.Id), "no player named: the caller");
            Assert.That(byID.UserID, Is.EqualTo(_captain.Id));
            Assert.That((await Assert.ThrowsAsync<ToolException>(() => tools.GetPlayerRank(userID: unknown)))!.Message, Does.Contain("on no leaderboard"));
            Assert.That(nobody.Gamertag, Is.Null, "a caller with no profile has no gamertag");
        }
    }

    [Test]
    public async Task MatchTools_ShowASeasonMatch_AndRefuseUnknownOnes()
    {
        var other = await _context.AddTeamWithCaptain(_season, "OtherGT", 9_100_001);
        var match = SeasonMatch.Seed(_season.SeasonID, _team.TeamID, other.TeamID);
        _context.SeasonMatches.Add(match);
        await _context.SaveChangesAsync();
        var tools = MatchTools(new ToolCaller(_captain.Id, []));

        var page = await tools.GetSeasonMatch(match.SeasonMatchID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.HomeTeamName, Is.EqualTo("Alpha"));
            await Assert.ThrowsAsync<ToolException>(() => tools.GetSeasonMatch(match.SeasonMatchID + 1000));
            await Assert.ThrowsAsync<ToolException>(() => tools.GetGame(Guid.NewGuid()));
        }
    }

    [Test]
    public async Task BrowseTools_Search_AndRefuseAnUnknownLateLeagueSeason()
    {
        var tools = new BrowseTools(ToolCaller.Anonymous, new QuickSearchService(_context, new GrifballWebApp.Application.Seasons.SeasonVisibility(_context, _time)), Leaderboards(), new MatchmakingStatsService(_context));

        var hits = await tools.Search("Captain");
        var leaderboards = await tools.GetLeaderboards();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hits.Players.Select(p => p.UserID), Does.Contain(_captain.Id));
            Assert.That(leaderboards.RingLeaders, Is.Empty, "no titles yet");
            Assert.That(await tools.ListLateLeagueSeasons(), Is.Empty);
            Assert.That((await Assert.ThrowsAsync<ToolException>(() => tools.GetLateLeagueSeason(4242)))!.Message, Does.Contain("list_late_league_seasons"));
        }
    }

    private static IEnumerable<System.Reflection.MethodInfo> AllTools() => GrifTools.All.SelectMany(set => GrifTools.Methods(set.Type));

    [Test]
    public void EveryTool_AndEachOfItsParameters_IsDescribed()
    {
        var undescribed = AllTools()
            .SelectMany(m => m.GetParameters()
                .Where(p => p.ParameterType != typeof(CancellationToken) && p.GetCustomAttributes(typeof(ToolDescription), false).Length == 0)
                .Select(p => $"{m.DeclaringType!.Name}.{m.Name}({p.Name})"))
            .ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(AllTools(), Is.Not.Empty);
            Assert.That(undescribed, Is.Empty, "the model reads these to pick a tool and fill it in");
        }
    }

    [Test]
    public void ToolNames_AreSnakeCase_AndUnique()
    {
        var names = AllTools().Select(GrifTools.Name).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(names, Is.Unique);
            Assert.That(names, Has.All.Match("^[a-z]+(_[a-z]+)*$"));
            Assert.That(names, Does.Contain("get_season_standings").And.Contain("who_am_i"));
        }
    }

    [Test]
    public void TheMcpServer_HasOneToolPerMethod_MarkedWithWhatItDoes()
    {
        var tools = GrifMcp.Tools(GrifTools.All).ToDictionary(t => t.ProtocolTool.Name, t => t.ProtocolTool.Annotations);
        var names = GrifTools.All.SelectMany(s => GrifTools.Methods(s.Type).Select(m => (Name: GrifTools.Name(m), s.Effect))).ToList();
        var reading = names.Where(n => n.Effect is ToolEffect.ReadsOnly).Select(n => tools[n.Name]).ToList();
        var changing = names.Where(n => n.Effect is ToolEffect.ChangesData).Select(n => tools[n.Name]).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tools.Keys, Is.EquivalentTo(AllTools().Select(GrifTools.Name)));
            Assert.That(reading.Select(a => a?.ReadOnlyHint), Has.All.True);
            Assert.That(reading.Select(a => a?.OpenWorldHint), Has.All.False);
            Assert.That(tools["pull_season_match_games"]?.ReadOnlyHint, Is.False, "its pull saves the games it finds: clients ask first");
            Assert.That(tools["pull_season_match_games"]?.OpenWorldHint, Is.True, "it reaches Halo");
            Assert.That(tools["pull_season_match_games"]?.IdempotentHint, Is.False, "a second pull can save games played since");
            Assert.That(tools["pull_xbox_account_games"]?.ReadOnlyHint, Is.False);
            Assert.That(tools["pull_xbox_account_games"]?.OpenWorldHint, Is.True);
            Assert.That(changing, Is.Not.Empty);
            Assert.That(changing.Select(a => a?.ReadOnlyHint), Has.All.False, "staff's actions change data: clients ask first");
            Assert.That(changing.Select(a => a?.DestructiveHint), Has.All.True, "a result undone, a claim decided");
            Assert.That(names.Where(n => n.Effect is not ToolEffect.ChangesData).Select(n => tools[n.Name]?.DestructiveHint), Has.All.False, "reads and pulls change or delete nothing");
        }
    }

    [Test]
    public void TheChatTools_AreAllTools()
    {
        Assert.That(GrifTools.Chat, Is.SubsetOf(AllTools().Select(GrifTools.Name)).And.Unique);
    }

    [Test]
    public async Task AnAnonymousCaller_NamesThePlayer_ForEveryToolAboutThem()
    {
        var tools = PlayerTools(ToolCaller.Anonymous);

        var me = await tools.WhoAmI();
        var named = await tools.GetPlayer(_captain.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(me.UserID, Is.Null);
            Assert.That(me.Roles, Is.Empty);
            Assert.That(named.UserID, Is.EqualTo(_captain.Id));
            Assert.That((await Assert.ThrowsAsync<ToolException>(() => tools.GetPlayer()))!.Message, Does.Contain("no site account"));
            await Assert.ThrowsAsync<ToolException>(() => tools.GetPlayerHistory());
            await Assert.ThrowsAsync<ToolException>(() => tools.GetPlayerRank());
        }
    }

    [Test]
    public void GetCurrentTime_IsTheClocksTime_InUtcAndUnixSeconds()
    {
        var now = new ClockTools(_time).GetCurrentTime();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(now.Utc, Is.EqualTo(_time.GetUtcNow()));
            Assert.That(now.UnixSeconds, Is.EqualTo(_time.GetUtcNow().ToUnixTimeSeconds()));
        }
    }

    /// <summary>The services a chat host's scope has: the Application's, on the test's database and clock.</summary>
    private ServiceProvider ChatServices()
        => new ServiceCollection()
            .AddApplication(new ConfigurationBuilder().Build())
            .AddLogging()
            .AddDistributedMemoryCache()
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<IApplicationDbContext>(_context)
            .AddSingleton(Substitute.For<IDataPullService>())
            .AddSingleton(Substitute.For<IGetsertXboxUserService>())
            .AddSingleton(new YouTubeClient(new HttpClient(new FakeSourcesHandler(_ => FakeSourcesHandler.Status(HttpStatusCode.NotFound)))))
            .BuildServiceProvider();

    private static AIFunction Function(IEnumerable<AITool> tools, string name) => tools.OfType<AIFunction>().Single(t => t.Name == name);

    [Test]
    public async Task AIFunctions_AreTheNamedTools_RunAsTheCaller()
    {
        await using var services = ChatServices();

        var tools = GrifTools.AIFunctions(services.GetRequiredService<IServiceScopeFactory>(), new ToolCaller(_captain.Id, PlayerRole), GrifTools.Chat);
        var me = await Function(tools, "who_am_i").InvokeAsync(new AIFunctionArguments());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tools.Select(t => t.Name), Is.EquivalentTo(GrifTools.Chat));
            Assert.That(Json(me).GetProperty("userID").GetInt32(), Is.EqualTo(_captain.Id));
        }
    }

    [Test]
    public async Task AIFunctions_GiveARefusal_AsTheToolsResult()
    {
        await using var services = ChatServices();
        var tools = GrifTools.AIFunctions(services.GetRequiredService<IServiceScopeFactory>(), ToolCaller.Anonymous, GrifTools.Chat);

        Assert.That(tools.Select(t => t.Name), Does.Not.Contain("pull_season_match_games").And.Not.Contain("pull_xbox_account_games"), "an anonymous caller has no role to pull with");
        var unknown = await Function(tools, "get_season").InvokeAsync(new AIFunctionArguments { ["seasonID"] = 987654 });
        var aboutMe = await Function(tools, "get_player").InvokeAsync(new AIFunctionArguments());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(unknown, Is.EqualTo("Refused: There is no season 987654: list_seasons has their IDs."));
            Assert.That(aboutMe?.ToString(), Does.StartWith("Refused: You have no site account"));
        }
    }

    /// <summary>A function's result as JSON, whether it gave it marshalled or as its own value.</summary>
    private static System.Text.Json.JsonElement Json(object? result)
        => result is System.Text.Json.JsonElement json ? json : System.Text.Json.JsonSerializer.SerializeToElement(result, AIJsonUtilities.DefaultOptions);

    /// <summary>A clock a scope makes, which says when its scope is done with it.</summary>
    private sealed class ScopedClock(DateTimeOffset now) : FakeTimeProvider(now), IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    [Test]
    public async Task AIFunctions_RunEachCallInAScopeOfItsOwn()
    {
        // The bot saves its streamed reply on its own scope's context meanwhile: a tool sharing it would race those saves
        var clocks = new List<ScopedClock>();
        await using var services = new ServiceCollection()
            .AddScoped<TimeProvider>(_ =>
            {
                var clock = new ScopedClock(_time.GetUtcNow());
                clocks.Add(clock);
                return clock;
            })
            .BuildServiceProvider();
        var now = Function(GrifTools.AIFunctions(services.GetRequiredService<IServiceScopeFactory>(), ToolCaller.Anonymous, GrifTools.Chat), "get_current_time");

        await now.InvokeAsync(new AIFunctionArguments());
        await now.InvokeAsync(new AIFunctionArguments());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clocks, Has.Count.EqualTo(2), "a scope per call");
            Assert.That(clocks.Select(c => c.Disposed), Has.All.True, "each scope ended with its call");
        }
    }

    [Test]
    public void ARoleTool_IsForItsRoles_AndTheRestForAnyone()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(GrifTools.RolesByName["pull_season_match_games"], Is.EquivalentTo(Pullers));
            Assert.That(GrifTools.RolesByName["pull_xbox_account_games"], Is.EquivalentTo(Pullers));
            Assert.That(GrifTools.RolesByName["get_commissioner_dashboard"], Is.EquivalentTo(Staff));
            Assert.That(GrifTools.RolesByName["decide_reschedule"], Is.EquivalentTo(Staff));
            Assert.That(GrifTools.RolesByName["forfeit_season_match"], Is.EquivalentTo(CommissionerOnly));
            Assert.That(GrifTools.RolesByName["decide_gamertag_claim"], Is.EquivalentTo(SysadminOnly));
            Assert.That(GrifTools.RolesByName["list_pending_gamertag_claims"], Is.EquivalentTo(SysadminOnly));
            Assert.That(GrifTools.Methods(typeof(SeasonTools)).Concat(GrifTools.Methods(typeof(TeamTools))).Select(m => GrifTools.RolesByName[GrifTools.Name(m)]), Has.All.Empty, "the site's public pages' tools are for anyone");
            Assert.That(GrifTools.Methods(typeof(StaffTools)).Concat(GrifTools.Methods(typeof(CommissionerTools))).Concat(GrifTools.Methods(typeof(SysadminTools)))
                .Select(m => GrifTools.RolesByName[GrifTools.Name(m)]), Has.None.Empty, "every staff tool names its roles");
        }
    }
}
