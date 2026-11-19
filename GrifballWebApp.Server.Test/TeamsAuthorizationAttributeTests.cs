using GrifballWebApp.Server.Teams;
using Microsoft.AspNetCore.Authorization;

namespace GrifballWebApp.Server.Test;

[TestFixture]
public class TeamsAuthorizationAttributeTests
{
    private static string? Roles(Type type, string method) =>
        ControllerTestHelpers.ActionAuthorize(type, method)?.Roles;

    [TestCase(nameof(TeamsController.AddCaptain), "Commissioner")]
    [TestCase(nameof(TeamsController.ResortCaptain), "Commissioner")]
    [TestCase(nameof(TeamsController.RemoveCaptain), "Commissioner")]
    [TestCase(nameof(TeamsController.RemovePlayerFromTeam), "Commissioner")]
    [TestCase(nameof(TeamsController.MovePlayerToTeam), "Commissioner")]
    [TestCase(nameof(TeamsController.LockCaptains), "Commissioner")]
    [TestCase(nameof(TeamsController.UnlockCaptains), "Commissioner")]
    [TestCase(nameof(TeamsController.AddPlayerToTeam), "Commissioner,Player")]
    [TestCase(nameof(TeamsController.GetTeams), null)]
    [TestCase(nameof(TeamsController.GetPlayerPool), null)]
    [TestCase(nameof(TeamsController.AreCaptainsLocked), null)]
    public void TeamsController_Should_RequireRoles(string method, string? roles)
    {
        Assert.That(Roles(typeof(TeamsController), method), Is.EqualTo(roles));
    }
}
