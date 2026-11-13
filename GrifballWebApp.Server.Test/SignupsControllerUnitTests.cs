using Microsoft.AspNetCore.Http;
using GrifballWebApp.Server.Signups;
using Microsoft.AspNetCore.Mvc;
using User = GrifballWebApp.Domain.Players.User;
using Team = GrifballWebApp.Domain.League.Team;
using DiscordUser = GrifballWebApp.Domain.Players.DiscordUser;
using SignupButtonInteractions = GrifballWebApp.Server.Signups.ButtonInteractions;
using GrifballWebApp.Application.Signups;

namespace GrifballWebApp.Server.Test;

/// <summary>SignupsControllerTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SignupsControllerUnitTests
{
    // Refused before the service is asked
    private readonly SignupsController _controller = new(null!);

    [Test]
    public void Authorization_SignupAndUpsertRequirePlayerOrSysadmin()
    {
        var t = typeof(SignupsController);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControllerTestHelpers.ClassAuthorize(t), Is.Null);
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(SignupsController.GetSignup))!.Roles, Is.EqualTo("Player,Sysadmin"));
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(SignupsController.UpsertSignup))!.Roles, Is.EqualTo("Player,Sysadmin"));
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(SignupsController.GetSignups)), Is.Null);
            // A signed-in player's own timeslots: signed in, any role
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(SignupsController.GetTimeslots))?.Roles, Is.Null);
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(SignupsController.GetTimeslots)), Is.Not.Null);
        }
    }

    [Test]
    public async Task UserScopedActions_WithoutUser_Unauthorized()
    {
        _controller.WithAnonymousUser();

        using (Assert.EnterMultipleScope())
        {
            (await _controller.GetTimeslots(1, 0, CancellationToken.None)).AssertProblem(StatusCodes.Status401Unauthorized);
            (await _controller.GetSignup(1, 0, CancellationToken.None)).AssertProblem(StatusCodes.Status401Unauthorized);
            (await _controller.UpsertSignup(1, new SignupRequestDto { SeasonID = 1 }, CancellationToken.None)).AssertProblem(StatusCodes.Status401Unauthorized);
        }
    }

    [Test]
    public async Task UserScopedActions_NonNumericId_Unauthorized()
    {
        _controller.WithUser("abc");

        (await _controller.GetTimeslots(1, 0, CancellationToken.None)).AssertProblem(StatusCodes.Status401Unauthorized);
    }
}

/// <summary>SignupsControllerTests's unit tests on a real GrifballContext (temp-file SQLite, #402), where
/// SignupsService needs nothing more.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SignupsControllerDatabaseUnitTests : SqliteGrifballContextFixture
{
    private SignupsController Controller() => new(new SignupsService(Db));

    [Test]
    public async Task GetSignups_ReturnsNone_ForASeasonWithNoSignups()
    {
        var result = await Controller().GetSignups(1, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetSignup_RepliesNoContent_ForAPlayerWithNoSignup()
    {
        var user = await AddUser(discordUserId: 1, gamertag: "Grifter1");
        var controller = Controller().WithUser(user.Id.ToString());

        var result = await controller.GetSignup(1, 0, CancellationToken.None);

        Assert.That(ControllerTestHelpers.StatusOf(result), Is.EqualTo(StatusCodes.Status204NoContent));
    }

    [Test]
    public async Task GetTimeslots_ReturnsNone_ForASeasonWithNoTimeslots()
    {
        var user = await AddUser(discordUserId: 1, gamertag: "Grifter1");
        var controller = Controller().WithUser(user.Id.ToString());

        var result = await controller.GetTimeslots(1, 0, CancellationToken.None);

        Assert.That(result.Value, Is.Empty);
    }
}
