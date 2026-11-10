using GrifballWebApp.Server.Signups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
}
