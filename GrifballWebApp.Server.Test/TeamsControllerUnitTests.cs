using GrifballWebApp.Application.Teams;
using GrifballWebApp.Server.Teams;
using GrifballWebApp.Application.Notifications;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>TeamsControllerTests' unit tests: requests refused before the team service is asked. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamsControllerUnitTests
{
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly TeamsController _controller;

    public TeamsControllerUnitTests()
    {
        // Refused before the service reaches the database, the draft channel or the locks
        _controller = new TeamsController(new TeamService(null!, _publisher, null!, null!));
    }

    [TestCase(null)]
    [TestCase("not-a-number")]
    public async Task AddPlayerToTeam_Should_BeUnauthorized_When_NoValidUserId(string? claim)
    {
        _controller.WithUser(claim);

        var result = await _controller.AddPlayerToTeam(1, new AddPlayerToTeamRequestDto { SeasonID = 1, CaptainID = 2, PersonID = 3 }, null, CancellationToken.None);

        result.AssertProblem(Microsoft.AspNetCore.Http.StatusCodes.Status401Unauthorized, "You must be logged in to add a player to a team");
        Assert.That(_publisher.ReceivedCalls(), Is.Empty);
    }

}

/// <summary>TeamsControllerTests' unit tests on a real GrifballContext (temp-file SQLite, #402), where TeamService needs
/// nothing more than IPublisher/IDraftChannel substituted.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamsControllerDatabaseUnitTests : SqliteGrifballContextFixture
{
    private TeamsController Controller() => new(new TeamService(Db, Substitute.For<IPublisher>(), Substitute.For<IDraftChannel>(), null!));

    [Test]
    public async Task GetTeams_ReturnsNone_ForASeasonWithNoTeams()
    {
        var result = await Controller().GetTeams(1, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetPlayerPool_ReturnsNone_ForASeasonWithNoSignups()
    {
        var result = await Controller().GetPlayerPool(1, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task LockCaptains_ThenAreCaptainsLocked_ReflectsIt()
    {
        var season = await AddSeason("Winter");
        var controller = Controller();

        await controller.LockCaptains(season.SeasonID, null, CancellationToken.None);

        Assert.That(await controller.AreCaptainsLocked(season.SeasonID, CancellationToken.None), Is.True);
    }

    [Test]
    public async Task UnlockCaptains_ThenAreCaptainsLocked_ReflectsIt()
    {
        var season = await AddSeason("Winter");
        var controller = Controller();
        await controller.LockCaptains(season.SeasonID, null, CancellationToken.None);

        await controller.UnlockCaptains(season.SeasonID, null, CancellationToken.None);

        Assert.That(await controller.AreCaptainsLocked(season.SeasonID, CancellationToken.None), Is.False);
    }
}
