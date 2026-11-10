using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.TeamStandings;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.TeamStandings;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
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
        _context.SeasonMatches.Add(SeasonMatch.Seed(season.SeasonID, a.TeamID, b.TeamID, bestOf: 3).WonBy(SeasonMatchSide.Away));
        await _context.SaveChangesAsync();

        var controller = new TeamStandingsController(new TeamStandingsService(_context));
        var result = await controller.GetTeamStandings(season.SeasonID, CancellationToken.None);

        Assert.That(result.Select(r => r.TeamName), Is.EqualTo(new[] { "Bravo", "Alpha" }));
        Assert.That(result.Select(r => r.Wins), Is.EqualTo(new[] { 1, 0 }));
    }
}
