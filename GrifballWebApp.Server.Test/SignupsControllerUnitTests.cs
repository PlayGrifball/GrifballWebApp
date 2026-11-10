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
            Assert.That(ControllerTestHelpers.ActionAuthorize(t, nameof(SignupsController.GetTimeslots)), Is.Null);
        }
    }

    [Test]
    public async Task UserScopedActions_WithoutUser_Unauthorized()
    {
        _controller.WithAnonymousUser();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _controller.GetTimeslots(1, 0, CancellationToken.None), Is.TypeOf<UnauthorizedObjectResult>());
            Assert.That(await _controller.GetSignup(1, 0, CancellationToken.None), Is.TypeOf<UnauthorizedObjectResult>());
            Assert.That(await _controller.UpsertSignup(new SignupRequestDto { SeasonID = 1 }, CancellationToken.None), Is.TypeOf<UnauthorizedObjectResult>());
        }
    }

    [Test]
    public async Task UserScopedActions_NonNumericId_Unauthorized()
    {
        _controller.WithUser("abc");

        Assert.That(await _controller.GetTimeslots(1, 0, CancellationToken.None), Is.TypeOf<UnauthorizedObjectResult>());
    }
}
