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

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
        _seasons = new SeasonService(_context, new SeasonStatsService(_context), _time);
        _season = await _context.AddSeason("Spring");
        _captain = await _context.AddUser("cap", gamertag: "CaptainGT");
        _team = await _context.AddTeam(_season, "Alpha", _captain, 1);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private SeasonTools SeasonTools() => new(_seasons, new TeamStandingsService(_context), new MatchPlannerService(_context), new GradesService(_context),
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
        new MatchDetailsService(_context));

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
    public async Task ASeasonTool_RefusesAnUnknownSeason_AndNoSeasonWhenNoneIsRunning()
    {
        var unknown = await Assert.ThrowsAsync<ToolException>(() => SeasonTools().GetSeasonSchedule(_season.SeasonID + 1000));
        Assert.That(unknown!.Message, Does.Contain($"no season {_season.SeasonID + 1000}").And.Contain("list_seasons"));

        _time.SetUtcNow(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var none = await Assert.ThrowsAsync<ToolException>(() => SeasonTools().GetSeasonStandings());
        var byID = await SeasonTools().GetSeasonStandings(_season.SeasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(none!.Message, Does.Contain("No season is running"));
            Assert.That(byID.Select(s => s.TeamName), Is.EqualTo(Alpha), "a season given by ID still answers");
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
        var tools = new TeamTools(new TeamPageService(_context, _time), new TeamHistoryService(_context, _time), Vods());

        var team = await tools.GetTeam(_team.TeamID);
        var matches = await tools.GetTeamMatches(_team.TeamID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(team.TeamName, Is.EqualTo("Alpha"));
            Assert.That(matches, Is.Empty);
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
        Assert.That((await tools.GetPlayerRank()).UserID, Is.EqualTo(_captain.Id), "no player named: the caller");
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
        var tools = new BrowseTools(new QuickSearchService(_context, _time), Leaderboards(), new MatchmakingStatsService(_context));

        var hits = await tools.Search("Captain");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hits.Players.Select(p => p.UserID), Does.Contain(_captain.Id));
            Assert.That(await tools.ListLateLeagueSeasons(), Is.Empty);
            Assert.That((await Assert.ThrowsAsync<ToolException>(() => tools.GetLateLeagueSeason(4242)))!.Message, Does.Contain("list_late_league_seasons"));
        }
    }

    private static IEnumerable<System.Reflection.MethodInfo> AllTools() => GrifTools.ReadOnly.SelectMany(GrifTools.Methods);

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
    public void TheMcpServer_HasOneReadOnlyToolPerMethod()
    {
        var tools = GrifMcp.Tools(GrifTools.ReadOnly, readOnly: true).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tools.Select(t => t.ProtocolTool.Name), Is.EquivalentTo(AllTools().Select(GrifTools.Name)));
            Assert.That(tools.Select(t => t.ProtocolTool.Annotations?.ReadOnlyHint), Has.All.True);
            Assert.That(tools.Select(t => t.ProtocolTool.Annotations?.DestructiveHint), Has.All.False);
        }
    }
}
