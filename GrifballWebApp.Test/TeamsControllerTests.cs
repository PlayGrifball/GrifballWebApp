using System.Reflection;
using System.Security.Claims;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.TeamPage;
using GrifballWebApp.Server.Teams;
using GrifballWebApp.Server.Teams.Handlers;
using GrifballWebApp.Server.TeamStandings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TeamsControllerTests
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

    private void SetUser(string? nameIdentifier) => _controller.WithUser(nameIdentifier);

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
