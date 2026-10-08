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
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TeamStandingsControllerTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task GetTeamStandings_Should_ReturnStandingsForSeason()
    {
        var season = await _context.AddSeason();
        var other = await _context.AddSeason("Other");
        var c1 = await _context.AddUser("c1");
        var c2 = await _context.AddUser("c2");
        var c3 = await _context.AddUser("c3");
        var a = await _context.AddTeam(season, "Alpha", c1, 1);
        var b = await _context.AddTeam(season, "Bravo", c2, 2);
        await _context.AddTeam(other, "Other", c3, 1);
        _context.SeasonMatches.Add(new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = a.TeamID, AwayTeamID = b.TeamID, HomeTeamResult = SeasonMatchResult.Loss, AwayTeamResult = SeasonMatchResult.Won, BestOf = 3 });
        await _context.SaveChangesAsync();

        var controller = new TeamStandingsController(new TeamStandingsService(_context));
        var result = await controller.GetTeamStandings(season.SeasonID, CancellationToken.None);

        Assert.That(result.Select(r => r.TeamName), Is.EqualTo(new[] { "Bravo", "Alpha" }));
        Assert.That(result.Select(r => r.Wins), Is.EqualTo(new[] { 1, 0 }));
    }
}
