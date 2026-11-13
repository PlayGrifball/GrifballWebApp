using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Server.Profile;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// ProfileController's unit tests, on a real GrifballContext (temp-file SQLite, #402): ProfileStatsService,
/// ProfileCommendationsService and the services they go through are all IApplicationDbContext-backed. Only
/// ISetGamertagService is substituted.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ProfileControllerUnitTests : SqliteGrifballContextFixture
{
    private readonly ISetGamertagService _setGamertagService = Substitute.For<ISetGamertagService>();

    private ProfileController Controller() => new(Db, _setGamertagService,
        new ProfileStatsService(Db, new SeasonStatsService(Db), new MatchmakingStatsService(Db)),
        new ProfileCommendationsService(Db));

    [Test]
    public async Task GetGamertag_RepliesNotFound_ForAnUnknownUser()
    {
        var result = await Controller().GetGamertag(999, CancellationToken.None);

        result.Result.AssertProblem(StatusCodes.Status404NotFound, "There is no such user");
    }

    [Test]
    public async Task GetGamertag_ReturnsIt_ForAKnownUser()
    {
        var user = await AddUser(123, "Someone");

        var result = await Controller().GetGamertag(user.Id, CancellationToken.None);

        Assert.That(result.Value!.Gamertag, Is.EqualTo("Someone"));
    }

    [Test]
    public async Task GetProfileStats_RepliesNotFound_ForAnUnknownUser()
    {
        var result = await Controller().GetProfileStats(999, CancellationToken.None);

        result.Result.AssertProblem(StatusCodes.Status404NotFound, "There is no such user");
    }

    [Test]
    public async Task GetProfileStats_ReturnsThem_ForAKnownUser()
    {
        var user = await AddUser(123, "Someone");

        var result = await Controller().GetProfileStats(user.Id, CancellationToken.None);

        Assert.That(result.Value!.Gamertag, Is.EqualTo("Someone"));
    }

    [Test]
    public async Task GetCommendations_ReturnsNone_ForAnUnknownUser()
    {
        var result = await Controller().GetCommendations(999, ProfileCommendationsService.DefaultTop, CancellationToken.None);

        Assert.That(result.TotalMedals, Is.Zero);
    }

    [Test]
    public async Task SetGamertag_RepliesForbidden_WhenSettingSomeoneElses()
    {
        var controller = Controller().WithUser("1");

        var result = await controller.SetGamertag(2, new SetGamertagRequest("NewTag"), CancellationToken.None);

        result.AssertProblem(StatusCodes.Status403Forbidden, "You can only set your own gamertag");
    }

    [Test]
    public async Task SetGamertag_RepliesBadRequest_WhenTheServiceRefuses()
    {
        _setGamertagService.SetGamertag(1, "Taken", Arg.Any<CancellationToken>()).Returns("That gamertag is taken");
        var controller = Controller().WithUser("1");

        var result = await controller.SetGamertag(1, new SetGamertagRequest("Taken"), CancellationToken.None);

        result.AssertProblem(StatusCodes.Status400BadRequest, "That gamertag is taken");
    }

    [Test]
    public async Task SetGamertag_Succeeds_ForOnesOwnProfile()
    {
        _setGamertagService.SetGamertag(1, "NewTag", Arg.Any<CancellationToken>()).Returns((string?)null);
        var controller = Controller().WithUser("1");

        var result = await controller.SetGamertag(1, new SetGamertagRequest("NewTag"), CancellationToken.None);

        Assert.That(ControllerTestHelpers.StatusOf(result), Is.EqualTo(StatusCodes.Status204NoContent));
    }
}
