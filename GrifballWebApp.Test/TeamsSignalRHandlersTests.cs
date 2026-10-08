using System.Security.Claims;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Teams;
using GrifballWebApp.Server.Teams.Handlers;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
public class TeamsSignalRHandlersTests
{
    private IHubContext<TeamsHub, ITeamsHubClient> _hub = null!;
    private ITeamsHubClient _others = null!;

    [SetUp]
    public void SetUp()
    {
        _hub = Substitute.For<IHubContext<TeamsHub, ITeamsHubClient>>();
        _others = Substitute.For<ITeamsHubClient>();
        _hub.Clients.AllExcept(Arg.Is<IReadOnlyList<string>>(l => l.Count == 1 && l[0] == "conn-1")).Returns(_others);
    }

    [Test]
    public void Notification_Create_Should_CarryConnectionAndValue()
    {
        var dto = new RemoveCaptainDto { SeasonID = 1, PersonID = 2 };
        var n = Notification.Create("c", dto);
        Assert.That(n, Is.TypeOf<Notification<RemoveCaptainDto>>());
        Assert.That(n.ConnectionId, Is.EqualTo("c"));
        Assert.That(n.Value, Is.SameAs(dto));
        Assert.That(Notification.Create<RemoveCaptainDto>(null, dto).ConnectionId, Is.Null);
    }

    [Test]
    public async Task AddCaptainHandler_Should_BroadcastToOthers()
    {
        var dto = new CaptainAddedDto { SeasonID = 1, PersonID = 2, TeamName = "T", CaptainName = "C", OrderNumber = 1 };
        await new AddCaptainHandler(_hub).Handle(Notification.Create("conn-1", dto), CancellationToken.None);
        await _others.Received(1).AddCaptain(dto);
    }

    [Test]
    public async Task ResortCaptainHandler_Should_BroadcastToOthers()
    {
        var dto = new CaptainPlacementDto { SeasonID = 1, PersonID = 2, OrderNumber = 3 };
        await new ResortCaptainHandler(_hub).Handle(Notification.Create("conn-1", dto), CancellationToken.None);
        await _others.Received(1).ResortCaptain(dto);
    }

    [Test]
    public async Task RemoveCaptainHandler_Should_BroadcastToOthers()
    {
        var dto = new RemoveCaptainDto { SeasonID = 1, PersonID = 2 };
        await new RemoveCaptainHandler(_hub).Handle(Notification.Create("conn-1", dto), CancellationToken.None);
        await _others.Received(1).RemoveCaptain(dto);
    }

    [Test]
    public async Task AddPlayerHandler_Should_BroadcastToOthers()
    {
        var dto = new AddPlayerToTeamRequestDto { SeasonID = 1, CaptainID = 2, PersonID = 3 };
        await new AddPlayerHandler(_hub).Handle(Notification.Create("conn-1", dto), CancellationToken.None);
        await _others.Received(1).AddPlayerToTeam(dto);
    }

    [Test]
    public async Task RemovePlayerHandler_Should_BroadcastToOthers()
    {
        var dto = new RemovePlayerFromTeamRequestDto { SeasonID = 1, CaptainID = 2, PersonID = 3 };
        await new RemovePlayerHandler(_hub).Handle(Notification.Create("conn-1", dto), CancellationToken.None);
        await _others.Received(1).RemovePlayerFromTeam(dto);
    }

    [Test]
    public async Task MovePlayerHandler_Should_BroadcastToOthers()
    {
        var dto = new MovePlayerToTeamRequestDto { SeasonID = 1, PreviousCaptainID = 2, NewCaptainID = 3, PersonID = 4, RoundNumber = 1 };
        await new MovePlayerHandler(_hub).Handle(Notification.Create("conn-1", dto), CancellationToken.None);
        await _others.Received(1).MovePlayerToTeam(dto);
    }

    [Test]
    public async Task LockChangeHandler_Should_BroadcastLockOrUnlock()
    {
        var handler = new LockChangeHandler(_hub);
        await handler.Handle(new LockChanged(true, 7, "conn-1"), CancellationToken.None);
        await _others.Received(1).LockCaptains(7);
        await _others.DidNotReceive().UnlockCaptains(Arg.Any<int>());

        await handler.Handle(new LockChanged(false, 8, "conn-1"), CancellationToken.None);
        await _others.Received(1).UnlockCaptains(8);
    }
}
