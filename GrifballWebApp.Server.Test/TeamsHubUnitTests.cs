using System.Security.Claims;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Server.Teams;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// TeamsHub's unit tests, on a real GrifballContext (temp-file SQLite, #402): TeamService is real throughout
/// (IPublisher/IDraftChannel/IDatabaseLocks substituted, so its own real validation and writes run here too). What its
/// notifications cause (the SignalR broadcasts) stays TeamsSignalRHandlersTests' (the handlers) and
/// GrifballWebApp.Integration.Test's (a real hub connection).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamsHubUnitTests : SqliteGrifballContextFixture
{
    private TeamsHub Hub(ClaimsPrincipal? user = null)
    {
        var hub = new TeamsHub(new TeamService(Db, Substitute.For<IPublisher>(), Substitute.For<IDraftChannel>(), Substitute.For<IDatabaseLocks>()), new GrifballWebApp.Application.Seasons.SeasonVisibility(Db));
        var context = Substitute.For<HubCallerContext>();
        context.User.Returns(user ?? new ClaimsPrincipal(new ClaimsIdentity()));
        context.ConnectionId.Returns("test-connection");
        hub.Context = context;
        return hub;
    }

    [Test]
    public async Task GetTeams_ReturnsNone_ForASeasonWithNoTeams()
    {
        var season = await AddSeason("Empty");

        var result = await Hub().GetTeams(season.SeasonID);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetPlayerPool_ReturnsNone_ForASeasonWithNoSignups()
    {
        var season = await AddSeason("Empty");

        var result = await Hub().GetPlayerPool(season.SeasonID);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task TheDraftsReads_RefuseASeasonThatIsntThere_AsAHiddenOne()
    {
        var teams = await Assert.ThrowsAsync<HubException>(() => Hub().GetTeams(999));
        var pool = await Assert.ThrowsAsync<HubException>(() => Hub().GetPlayerPool(999));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams!.Message, Is.EqualTo(GrifballWebApp.Server.Seasons.SeasonVisibilityFilter.NoSuchSeason));
            Assert.That(pool!.Message, Is.EqualTo(GrifballWebApp.Server.Seasons.SeasonVisibilityFilter.NoSuchSeason));
        }
    }

    [Test]
    public async Task AreCaptainsLocked_ReflectsTheSeason()
    {
        var season = await AddSeason("Winter");

        Assert.That(await Hub().AreCaptainsLocked(season.SeasonID), Is.False);
    }

    [Test]
    public void AddPlayerToTeam_Throws_WhenNotSignedIn()
    {
        var dto = new AddPlayerToTeamRequestDto { SeasonID = 1, CaptainID = 1, PersonID = 1 };

        Assert.That(() => Hub().AddPlayerToTeam(dto), Throws.Exception.With.Message.EqualTo("You must be logged in to add a player to a team"));
    }

    [Test]
    public async Task LockCaptains_AndUnlockCaptains_RoundTrip_ThroughAreCaptainsLocked()
    {
        var season = await AddSeason("Winter");
        var hub = Hub();

        await hub.LockCaptains(season.SeasonID);
        var locked = await hub.AreCaptainsLocked(season.SeasonID);
        await hub.UnlockCaptains(season.SeasonID);
        var unlocked = await hub.AreCaptainsLocked(season.SeasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(locked, Is.True);
            Assert.That(unlocked, Is.False);
        }
    }

    [Test]
    public async Task AddCaptain_Throws_ForANonExistentSeason()
    {
        var dto = new CaptainPlacementDto { SeasonID = 999, PersonID = 1, OrderNumber = 1 };

        var e = await Assert.ThrowsAsync<TeamServiceException>(() => Hub().AddCaptain(dto));
        Assert.That(e!.Message, Is.EqualTo("Season does not exist"));
    }

    [Test]
    public async Task ResortCaptain_Throws_ForANonExistentSeason()
    {
        var dto = new CaptainPlacementDto { SeasonID = 999, PersonID = 1, OrderNumber = 1 };

        var e = await Assert.ThrowsAsync<TeamServiceException>(() => Hub().ResortCaptain(dto));
        Assert.That(e!.Message, Is.EqualTo("Season does not exist"));
    }

    [Test]
    public async Task RemoveCaptain_Throws_ForANonExistentSeason()
    {
        var dto = new RemoveCaptainDto { SeasonID = 999, PersonID = 1 };

        var e = await Assert.ThrowsAsync<TeamServiceException>(() => Hub().RemoveCaptain(dto));
        Assert.That(e!.Message, Is.EqualTo("Season does not exist"));
    }

    [Test]
    public async Task RemovePlayerFromTeam_Throws_WhenThePlayerIsNotOnThatTeam()
    {
        var dto = new RemovePlayerFromTeamRequestDto { SeasonID = 999, CaptainID = 1, PersonID = 1 };

        var e = await Assert.ThrowsAsync<TeamServiceException>(() => Hub().RemovePlayerFromTeam(dto));
        Assert.That(e!.Message, Is.EqualTo("Player is not on this team"));
    }

    [Test]
    public async Task MovePlayerToTeam_Throws_WhenThePlayerWasNotOnThatTeam()
    {
        var dto = new MovePlayerToTeamRequestDto { SeasonID = 999, PreviousCaptainID = 1, NewCaptainID = 2, PersonID = 1, RoundNumber = 1 };

        var e = await Assert.ThrowsAsync<TeamServiceException>(() => Hub().MovePlayerToTeam(dto));
        Assert.That(e!.Message, Is.EqualTo("Player was not on that team"));
    }
}
