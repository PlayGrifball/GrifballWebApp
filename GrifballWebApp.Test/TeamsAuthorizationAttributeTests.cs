using System.Reflection;
using System.Security.Claims;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.TeamPage;
using GrifballWebApp.Server.Teams;
using GrifballWebApp.Server.Teams.Handlers;
using GrifballWebApp.Server.TeamStandings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Test;

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

    [TestCase(nameof(TeamsHub.AddCaptain), "Commissioner")]
    [TestCase(nameof(TeamsHub.ResortCaptain), "Commissioner")]
    [TestCase(nameof(TeamsHub.RemoveCaptain), "Commissioner")]
    [TestCase(nameof(TeamsHub.RemovePlayerFromTeam), "Commissioner")]
    [TestCase(nameof(TeamsHub.MovePlayerToTeam), "Commissioner")]
    [TestCase(nameof(TeamsHub.LockCaptains), "Commissioner")]
    [TestCase(nameof(TeamsHub.UnlockCaptains), "Commissioner")]
    [TestCase(nameof(TeamsHub.AddPlayerToTeam), "Commissioner,Player")]
    [TestCase(nameof(TeamsHub.GetTeams), null)]
    [TestCase(nameof(TeamsHub.AreCaptainsLocked), null)]
    public void TeamsHub_Should_RequireRoles(string method, string? roles)
    {
        Assert.That(Roles(typeof(TeamsHub), method), Is.EqualTo(roles));
    }
}
