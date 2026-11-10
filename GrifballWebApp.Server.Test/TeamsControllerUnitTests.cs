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
    public async Task AddPlayerToTeam_Should_ReturnBadRequest_When_NoValidUserId(string? claim)
    {
        _controller.WithUser(claim);

        var result = await _controller.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = 1, CaptainID = 2, PersonID = 3 }, null, CancellationToken.None);

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(((BadRequestObjectResult)result).Value, Is.EqualTo("You must be logged in to add a player to a team"));
            Assert.That(_publisher.ReceivedCalls(), Is.Empty);
        }
    }
}
