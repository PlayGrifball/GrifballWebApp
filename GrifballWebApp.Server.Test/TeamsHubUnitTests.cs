using System.Security.Claims;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Server.Teams;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// TeamsHub's unit tests, on a real GrifballContext (temp-file SQLite, #402) for the reads TeamService needs nothing
/// else for. Its SignalR broadcasts stay TeamsSignalRHandlersTests' (the handlers) and
/// GrifballWebApp.Integration.Test's (the writes, with a database and a real hub connection).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamsHubUnitTests : SqliteGrifballContextFixture
{
    private TeamsHub Hub(ClaimsPrincipal? user = null)
    {
        var hub = new TeamsHub(new TeamService(Db, null!, null!, null!));
        var context = Substitute.For<HubCallerContext>();
        context.User.Returns(user ?? new ClaimsPrincipal(new ClaimsIdentity()));
        hub.Context = context;
        return hub;
    }

    [Test]
    public async Task GetTeams_ReturnsNone_ForASeasonWithNoTeams()
    {
        var result = await Hub().GetTeams(1);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetPlayerPool_ReturnsNone_ForASeasonWithNoSignups()
    {
        var result = await Hub().GetPlayerPool(1);

        Assert.That(result, Is.Empty);
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
}
