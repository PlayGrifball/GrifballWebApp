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
public class TeamsHubTests
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
