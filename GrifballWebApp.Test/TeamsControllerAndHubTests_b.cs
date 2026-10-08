using System.Reflection;
using System.Security.Claims;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.TeamPage;
using GrifballWebApp.Server.Teams;
using GrifballWebApp.Server.Teams.Handlers;
using GrifballWebApp.Server.TeamStandings;
using GrifballWebApp.Test.CovB;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
public class TeamsAuthorizationAttributeTests_b
{
    private static string? Roles(Type type, string method) =>
        type.GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>()?.Roles;

    [TestCase(nameof(TeamsController.AddCaptain), "Commissioner")]
    [TestCase(nameof(TeamsController.ResortCaptain), "Commissioner")]
    [TestCase(nameof(TeamsController.RemoveCaptain), "Commissioner")]
    [TestCase(nameof(TeamsController.RemovePlayerFromTeam), "Commissioner")]
    [TestCase(nameof(TeamsController.MovePlayerToTeam), "Commissioner")]
    [TestCase(nameof(TeamsController.LockCaptains), "Commissioner")]
    [TestCase(nameof(TeamsController.UnlockCaptains), "Commissioner")]
    [TestCase(nameof(TeamsController.AddPlayerToTeam), "Commissioner,Player")]
    [TestCase(nameof(TeamsController.GetTeams), null)]
    [TestCase(nameof(TeamsController.GetPlayerPool), null)]
    [TestCase(nameof(TeamsController.AreCaptainsLocked), null)]
    public void TeamsController_Should_RequireRoles(string method, string? roles)
    {
        Assert.That(Roles(typeof(TeamsController), method), Is.EqualTo(roles));
    }

    [TestCase(nameof(TeamsHub.AddCaptain), "Commissioner")]
    [TestCase(nameof(TeamsHub.ResortCaptain), "Commissioner")]
    [TestCase(nameof(TeamsHub.RemoveCaptain), "Commissioner")]
    [TestCase(nameof(TeamsHub.RemovePlayerFromTeam), "Commissioner")]
    [TestCase(nameof(TeamsHub.MovePlayerToTeam), "Commissioner")]
    [TestCase(nameof(TeamsHub.LockCaptains), "Commissioner")]
    [TestCase(nameof(TeamsHub.UnlockCaptains), "Commissioner")]
    [TestCase(nameof(TeamsHub.AddPlayerToTeam), "Commissioner,Player")]
    [TestCase(nameof(TeamsHub.GetTeams), null)]
    [TestCase(nameof(TeamsHub.AreCaptainsLocked), null)]
    public void TeamsHub_Should_RequireRoles(string method, string? roles)
    {
        Assert.That(Roles(typeof(TeamsHub), method), Is.EqualTo(roles));
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TeamsControllerTests_b
{
    private GrifballContext _context = null!;
    private IPublisher _publisher = null!;
    private TeamsController _controller = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _publisher = Substitute.For<IPublisher>();
        _controller = new TeamsController(new TeamService(_context, _publisher));
        SetUser(null);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private void SetUser(string? nameIdentifier)
    {
        var identity = nameIdentifier is null ? new ClaimsIdentity() : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, nameIdentifier)], "test");
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
    }

    private T SinglePublished<T>() =>
        _publisher.ReceivedCalls().Select(c => c.GetArguments()[0]).OfType<T>().Single();

    [Test]
    public async Task GetTeams_And_GetPlayerPool_Should_ReturnServiceData()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("cap", "Cap");
        var p = await _context.AddUser("p", gamertag: "PoolGuy");
        await _context.AddSignup(season, cap);
        await _context.AddSignup(season, p);
        await _context.AddTeam(season, "Team", cap, 1);

        var teams = await _controller.GetTeams(season.SeasonID, CancellationToken.None);
        var pool = await _controller.GetPlayerPool(season.SeasonID, CancellationToken.None);

        Assert.That(teams.Select(t => t.TeamName), Is.EqualTo(new[] { "Team" }));
        Assert.That(pool.Select(x => x.Name), Is.EqualTo(new[] { "PoolGuy" }));
    }

    [Test]
    public async Task CaptainEndpoints_Should_Add_Resort_And_Remove_WithConnectionId()
    {
        var season = await _context.AddSeason();
        var c1 = await _context.AddUser("c1", "C1");
        var c2 = await _context.AddUser("c2", "C2");
        await _context.AddSignup(season, c1, "One");
        await _context.AddSignup(season, c2, "Two");

        await _controller.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = c1.Id, OrderNumber = 1 }, "h1", CancellationToken.None);
        await _controller.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = c2.Id, OrderNumber = 2 }, "h1", CancellationToken.None);
        await _controller.ResortCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = c2.Id, OrderNumber = 1 }, "h2", CancellationToken.None);

        var teams = await _controller.GetTeams(season.SeasonID, CancellationToken.None);
        Assert.That(teams.Select(t => t.TeamName), Is.EqualTo(new[] { "Two", "One" }));
        Assert.That(SinglePublished<Notification<CaptainPlacementDto>>().ConnectionId, Is.EqualTo("h2"));

        await _controller.RemoveCaptain(new RemoveCaptainDto { SeasonID = season.SeasonID, PersonID = c2.Id }, "h3", CancellationToken.None);
        Assert.That(await _context.NewContextLike().Teams.Select(t => t.TeamName).ToListAsync(), Is.EqualTo(new[] { "One" }));
        Assert.That(SinglePublished<Notification<RemoveCaptainDto>>().ConnectionId, Is.EqualTo("h3"));
    }

    [Test]
    public async Task PlayerEndpoints_Should_Add_Move_And_Remove()
    {
        var season = await _context.AddSeason();
        var commish = await _context.AddUser("commish", commissioner: true);
        var capA = await _context.AddUser("capA");
        var capB = await _context.AddUser("capB");
        var p = await _context.AddUser("p", "P");
        await _context.AddSignup(season, p);
        var teamA = await _context.AddTeam(season, "A", capA, 1);
        var teamB = await _context.AddTeam(season, "B", capB, 2);
        SetUser(commish.Id.ToString());

        var result = await _controller.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = season.SeasonID, CaptainID = capA.Id, PersonID = p.Id }, "h", CancellationToken.None);
        Assert.That(result, Is.TypeOf<OkResult>());
        Assert.That(await _context.NewContextLike().TeamPlayers.AnyAsync(tp => tp.TeamID == teamA.TeamID && tp.UserID == p.Id), Is.True);

        await _controller.MovePlayerToTeam(new MovePlayerToTeamRequestDto { SeasonID = season.SeasonID, PreviousCaptainID = capA.Id, NewCaptainID = capB.Id, PersonID = p.Id, RoundNumber = 1 }, "h", CancellationToken.None);
        Assert.That(await _context.NewContextLike().TeamPlayers.AnyAsync(tp => tp.TeamID == teamB.TeamID && tp.UserID == p.Id), Is.True);

        await _controller.RemovePlayerFromTeam(new RemovePlayerFromTeamRequestDto { SeasonID = season.SeasonID, CaptainID = capB.Id, PersonID = p.Id }, "h", CancellationToken.None);
        Assert.That(await _context.NewContextLike().TeamPlayers.AnyAsync(tp => tp.UserID == p.Id), Is.False);
    }

    [TestCase(null)]
    [TestCase("not-a-number")]
    public async Task AddPlayerToTeam_Should_ReturnBadRequest_When_NoValidUserId(string? claim)
    {
        SetUser(claim);

        var result = await _controller.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = 1, CaptainID = 2, PersonID = 3 }, null, CancellationToken.None);

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("You must be logged in to add a player to a team"));
        Assert.That(_publisher.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task LockEndpoints_Should_ToggleCaptainsLocked()
    {
        var season = await _context.AddSeason();

        await _controller.LockCaptains(season.SeasonID, "h", CancellationToken.None);
        Assert.That(await new TeamsController(new TeamService(_context.NewContextLike(), _publisher)).AreCaptainsLocked(season.SeasonID, CancellationToken.None), Is.True);

        await _controller.UnlockCaptains(season.SeasonID, "h", CancellationToken.None);
        Assert.That(await new TeamsController(new TeamService(_context.NewContextLike(), _publisher)).AreCaptainsLocked(season.SeasonID, CancellationToken.None), Is.False);

        Assert.That(_publisher.ReceivedCalls().Select(c => c.GetArguments()[0]).OfType<LockChanged>().Select(l => l.Value), Is.EqualTo(new[] { true, false }));
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TeamsHubTests_b
{
    private GrifballContext _context = null!;
    private IPublisher _publisher = null!;
    private TeamsHub _hub = null!;
    private HubCallerContext _callerContext = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _publisher = Substitute.For<IPublisher>();
        _hub = new TeamsHub(new TeamService(_context, _publisher));
        _callerContext = Substitute.For<HubCallerContext>();
        _callerContext.ConnectionId.Returns("hub-conn");
        _hub.Context = _callerContext;
    }

    [TearDown]
    public async Task TearDown()
    {
        _hub.Dispose();
        await _context.DropDatabaseAndDispose();
    }

    private void SetUser(string? nameIdentifier)
    {
        var identity = nameIdentifier is null ? new ClaimsIdentity() : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, nameIdentifier)], "test");
        _callerContext.User.Returns(new ClaimsPrincipal(identity));
    }

    private List<object> Published() => _publisher.ReceivedCalls().Select(c => c.GetArguments()[0]!).ToList();

    [Test]
    public async Task ConnectAndDisconnect_Should_Complete()
    {
        await _hub.OnConnectedAsync();
        await _hub.OnDisconnectedAsync(null);
        Assert.Pass();
    }

    [Test]
    public async Task Hub_Should_DelegateDraftOperations_UsingCallerConnectionId()
    {
        var season = await _context.AddSeason();
        var c1 = await _context.AddUser("c1", "C1");
        var c2 = await _context.AddUser("c2", "C2");
        var p = await _context.AddUser("p", gamertag: "PGT");
        var commish = await _context.AddUser("commish", commissioner: true);
        await _context.AddSignup(season, c1, "One");
        await _context.AddSignup(season, c2, "Two");
        await _context.AddSignup(season, p);
        SetUser(commish.Id.ToString());

        await _hub.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = c1.Id, OrderNumber = 1 });
        await _hub.AddCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = c2.Id, OrderNumber = 2 });
        await _hub.ResortCaptain(new CaptainPlacementDto { SeasonID = season.SeasonID, PersonID = c2.Id, OrderNumber = 1 });
        Assert.That(await _hub.GetPlayerPool(season.SeasonID), Has.Count.EqualTo(1));

        await _hub.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = season.SeasonID, CaptainID = c1.Id, PersonID = p.Id });
        await _hub.MovePlayerToTeam(new MovePlayerToTeamRequestDto { SeasonID = season.SeasonID, PreviousCaptainID = c1.Id, NewCaptainID = c2.Id, PersonID = p.Id, RoundNumber = 1 });

        var teams = await new TeamsHub(new TeamService(_context.NewContextLike(), _publisher)).GetTeams(season.SeasonID);
        Assert.That(teams.Select(t => t.TeamName), Is.EqualTo(new[] { "Two", "One" }));
        Assert.That(teams[0].Players.Select(x => x.PersonID), Is.EqualTo(new[] { p.Id }));
        Assert.That(await _hub.GetPlayerPool(season.SeasonID), Is.Empty);

        await _hub.RemovePlayerFromTeam(new RemovePlayerFromTeamRequestDto { SeasonID = season.SeasonID, CaptainID = c2.Id, PersonID = p.Id });
        await _hub.RemoveCaptain(new RemoveCaptainDto { SeasonID = season.SeasonID, PersonID = c1.Id });
        await _hub.LockCaptains(season.SeasonID);
        Assert.That(await new TeamsHub(new TeamService(_context.NewContextLike(), _publisher)).AreCaptainsLocked(season.SeasonID), Is.True);
        await _hub.UnlockCaptains(season.SeasonID);
        Assert.That(await new TeamsHub(new TeamService(_context.NewContextLike(), _publisher)).AreCaptainsLocked(season.SeasonID), Is.False);

        var published = Published();
        Assert.That(published, Has.Count.EqualTo(9));
        Assert.That(published.OfType<LockChanged>().Select(l => l.connectionID), Is.All.EqualTo("hub-conn"));
        Assert.That(published.OfType<Notification<CaptainAddedDto>>().Select(n => n.ConnectionId), Is.All.EqualTo("hub-conn"));
        Assert.That(published.OfType<Notification<AddPlayerToTeamRequestDto>>().Single().ConnectionId, Is.EqualTo("hub-conn"));
        Assert.That(published.OfType<Notification<RemoveCaptainDto>>().Single().ConnectionId, Is.EqualTo("hub-conn"));
    }

    [TestCase(null)]
    [TestCase("abc")]
    public void AddPlayerToTeam_Should_Throw_When_NotLoggedIn(string? claim)
    {
        SetUser(claim);
        var ex = Assert.Throws<Exception>(() => _hub.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = 1, CaptainID = 2, PersonID = 3 }));
        Assert.That(ex!.Message, Is.EqualTo("You must be logged in to add a player to a team"));
    }

    [Test]
    public void AddPlayerToTeam_Should_Throw_When_NoUserOnContext()
    {
        _callerContext.User.Returns((ClaimsPrincipal?)null);
        Assert.Throws<Exception>(() => _hub.AddPlayerToTeam(new AddPlayerToTeamRequestDto()));
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TeamPageControllerTests_b
{
    private GrifballContext _context = null!;
    private TeamController _controller = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _controller = new TeamController(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private async Task<(Season season, Team a, Team b, Team c, User capA, User p1, User p2)> Seed()
    {
        var season = await _context.AddSeason();
        var capA = await _context.AddUser("capA", gamertag: "CapA");
        var p1 = await _context.AddUser("p1", gamertag: "P1");
        var p2 = await _context.AddUser("p2", gamertag: "P2");
        var capB = await _context.AddUser("capB", gamertag: "CapB");
        var capC = await _context.AddUser("capC", gamertag: "CapC");
        // players added in reverse round order to prove sorting
        var a = await _context.AddTeam(season, "Alpha", capA, 1, p1, p2);
        var b = await _context.AddTeam(season, "Bravo", capB, 2);
        var c = await _context.AddTeam(season, "Charlie", capC, 3);
        var tps = await _context.TeamPlayers.Where(tp => tp.TeamID == a.TeamID && tp.DraftRound != null).ToListAsync();
        tps.Single(tp => tp.UserID == p1.Id).DraftRound = 2;
        tps.Single(tp => tp.UserID == p2.Id).DraftRound = 1;

        _context.SeasonMatches.AddRange(
            // A home win vs B (played)
            new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = a.TeamID, AwayTeamID = b.TeamID, HomeTeamScore = 2, AwayTeamScore = 0, HomeTeamResult = SeasonMatchResult.Won, AwayTeamResult = SeasonMatchResult.Loss, BestOf = 3, ScheduledTime = new DateTime(2025, 1, 1) },
            // A away loss vs C (played)
            new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = c.TeamID, AwayTeamID = a.TeamID, HomeTeamScore = 2, AwayTeamScore = 1, HomeTeamResult = SeasonMatchResult.Won, AwayTeamResult = SeasonMatchResult.Loss, BestOf = 3, ScheduledTime = new DateTime(2025, 1, 2) },
            // A away forfeit vs B (counts as a loss, no score)
            new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = b.TeamID, AwayTeamID = a.TeamID, HomeTeamResult = SeasonMatchResult.Won, AwayTeamResult = SeasonMatchResult.Forfeit, BestOf = 3, ScheduledTime = new DateTime(2025, 1, 3) },
            // Unplayed, later
            new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = a.TeamID, AwayTeamID = c.TeamID, BestOf = 5, ScheduledTime = new DateTime(2025, 2, 10) },
            // Unplayed, earlier (away)
            new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = b.TeamID, AwayTeamID = a.TeamID, BestOf = 3, ScheduledTime = new DateTime(2025, 2, 1) },
            // Unrelated match
            new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = b.TeamID, AwayTeamID = c.TeamID, BestOf = 3, ScheduledTime = new DateTime(2025, 1, 5) });
        await _context.SaveChangesAsync();
        return (season, a, b, c, capA, p1, p2);
    }

    [Test]
    public async Task Team_Should_ReturnNull_When_NotFound()
    {
        Assert.That(await _controller.Team(12345, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task Team_Should_ComputeRecord_And_OrderPlayers_CaptainFirst()
    {
        var (_, a, _, _, capA, p1, p2) = await Seed();

        var dto = await _controller.Team(a.TeamID, CancellationToken.None);

        Assert.That(dto, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(dto!.TeamName, Is.EqualTo("Alpha"));
            Assert.That(dto.Wins, Is.EqualTo(1));
            Assert.That(dto.Losses, Is.EqualTo(2));
            Assert.That(dto.TBD, Is.EqualTo(2));
            Assert.That(dto.Players.Select(p => p.UserID), Is.EqualTo(new[] { capA.Id, p2.Id, p1.Id }));
            Assert.That(dto.Players.Select(p => p.Gamertag), Is.EqualTo(new[] { "CapA", "P2", "P1" }));
            Assert.That(dto.Players[0].DraftCaptainOrder, Is.EqualTo(1));
            Assert.That(dto.Players[1].DraftRound, Is.EqualTo(1));
            Assert.That(dto.Players.Select(p => p.TeamPlayerID), Is.All.GreaterThan(0));
        });
    }

    [Test]
    public async Task Matches_Should_ListHomeAndAway_FromTeamPerspective_UnplayedFirst()
    {
        var (_, a, b, c, _, _, _) = await Seed();

        var matches = await _controller.Matches(a.TeamID, CancellationToken.None);

        Assert.That(matches, Has.Length.EqualTo(5));
        Assert.That(matches.Select(m => m.ScheduledTime), Is.EqualTo(new DateTime?[]
        {
            new DateTime(2025, 2, 1), new DateTime(2025, 2, 10), // unplayed, by date
            new DateTime(2025, 1, 1), new DateTime(2025, 1, 2), new DateTime(2025, 1, 3),
        }));
        Assert.Multiple(() =>
        {
            Assert.That(matches[0].OtherTeamID, Is.EqualTo(b.TeamID));
            Assert.That(matches[0].Result, Is.Null);
            Assert.That(matches[1].OtherTeamName, Is.EqualTo("Charlie"));
            Assert.That(matches[1].BestOf, Is.EqualTo(5));

            // home win: perspective of A
            Assert.That((matches[2].Score, matches[2].OtherScore, matches[2].Result, matches[2].OtherResult, matches[2].OtherTeamName),
                Is.EqualTo(((int?)2, (int?)0, (SeasonMatchResult?)SeasonMatchResult.Won, (SeasonMatchResult?)SeasonMatchResult.Loss, "Bravo")));
            // away loss: scores are swapped to A's perspective
            Assert.That((matches[3].Score, matches[3].OtherScore, matches[3].Result, matches[3].OtherTeamID),
                Is.EqualTo(((int?)1, (int?)2, (SeasonMatchResult?)SeasonMatchResult.Loss, (int?)c.TeamID)));
            Assert.That(matches[4].Result, Is.EqualTo(SeasonMatchResult.Forfeit));
            Assert.That(matches[4].Score, Is.Null);
        });
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TeamStandingsControllerTests_b
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task GetTeamStandings_Should_ReturnStandingsForSeason()
    {
        var season = await _context.AddSeason();
        var other = await _context.AddSeason("Other");
        var c1 = await _context.AddUser("c1");
        var c2 = await _context.AddUser("c2");
        var c3 = await _context.AddUser("c3");
        var a = await _context.AddTeam(season, "Alpha", c1, 1);
        var b = await _context.AddTeam(season, "Bravo", c2, 2);
        await _context.AddTeam(other, "Other", c3, 1);
        _context.SeasonMatches.Add(new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = a.TeamID, AwayTeamID = b.TeamID, HomeTeamResult = SeasonMatchResult.Loss, AwayTeamResult = SeasonMatchResult.Won, BestOf = 3 });
        await _context.SaveChangesAsync();

        var controller = new TeamStandingsController(new TeamStandingsService(_context));
        var result = await controller.GetTeamStandings(season.SeasonID, CancellationToken.None);

        Assert.That(result.Select(r => r.TeamName), Is.EqualTo(new[] { "Bravo", "Alpha" }));
        Assert.That(result.Select(r => r.Wins), Is.EqualTo(new[] { 1, 0 }));
    }
}
