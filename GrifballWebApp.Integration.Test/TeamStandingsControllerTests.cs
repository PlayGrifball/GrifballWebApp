using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.TeamStandings;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.TeamStandings;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamStandingsControllerTests
{
    private GrifballContext _context = null!;
    private static readonly string[] expected = new[] { "Bravo", "Alpha" };
    private static readonly int[] expectedArray = new[] { 1, 0 };

    [SetUp]
    public async Task SetUp() => _context = await TestDatabase.NewGrifballContext();

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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Select(r => r.TeamName), Is.EqualTo(expected));
            Assert.That(result.Select(r => r.Wins), Is.EqualTo(expectedArray));
        }
    }
}
