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
public class TeamPageControllerTests
{
    private GrifballContext _context = null!;
    private TeamController _controller = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _controller = new TeamController(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private async Task<(Season season, Team a, Team b, Team c, User capA, User p1, User p2)> Seed()
    {
        var season = await _context.AddSeason();
        var capA = await _context.AddUser("capA", gamertag: "CapA");
        var p1 = await _context.AddUser("p1", gamertag: "P1");
        var p2 = await _context.AddUser("p2", gamertag: "P2");
        var capB = await _context.AddUser("capB", gamertag: "CapB");
        var capC = await _context.AddUser("capC", gamertag: "CapC");
        // players added in reverse round order to prove sorting
        var a = await _context.AddTeam(season, "Alpha", capA, 1, p1, p2);
        var b = await _context.AddTeam(season, "Bravo", capB, 2);
        var c = await _context.AddTeam(season, "Charlie", capC, 3);
        var tps = await _context.TeamPlayers.Where(tp => tp.TeamID == a.TeamID && tp.DraftRound != null).ToListAsync();
        tps.Single(tp => tp.UserID == p1.Id).DraftRound = 2;
        tps.Single(tp => tp.UserID == p2.Id).DraftRound = 1;

        _context.SeasonMatches.AddRange(
            // A home win vs B (played)
            new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = a.TeamID, AwayTeamID = b.TeamID, HomeTeamScore = 2, AwayTeamScore = 0, HomeTeamResult = SeasonMatchResult.Won, AwayTeamResult = SeasonMatchResult.Loss, BestOf = 3, ScheduledTime = new DateTime(2025, 1, 1) },
            // A away loss vs C (played)
            new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = c.TeamID, AwayTeamID = a.TeamID, HomeTeamScore = 2, AwayTeamScore = 1, HomeTeamResult = SeasonMatchResult.Won, AwayTeamResult = SeasonMatchResult.Loss, BestOf = 3, ScheduledTime = new DateTime(2025, 1, 2) },
            // A away forfeit vs B (counts as a loss, no score)
            new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = b.TeamID, AwayTeamID = a.TeamID, HomeTeamResult = SeasonMatchResult.Won, AwayTeamResult = SeasonMatchResult.Forfeit, BestOf = 3, ScheduledTime = new DateTime(2025, 1, 3) },
            // Unplayed, later
            new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = a.TeamID, AwayTeamID = c.TeamID, BestOf = 5, ScheduledTime = new DateTime(2025, 2, 10) },
            // Unplayed, earlier (away)
            new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = b.TeamID, AwayTeamID = a.TeamID, BestOf = 3, ScheduledTime = new DateTime(2025, 2, 1) },
            // Unrelated match
            new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = b.TeamID, AwayTeamID = c.TeamID, BestOf = 3, ScheduledTime = new DateTime(2025, 1, 5) });
        await _context.SaveChangesAsync();
        return (season, a, b, c, capA, p1, p2);
    }

    [Test]
    public async Task Team_Should_ReturnNull_When_NotFound()
    {
        Assert.That(await _controller.Team(12345, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task Team_Should_ComputeRecord_And_OrderPlayers_CaptainFirst()
    {
        var (_, a, _, _, capA, p1, p2) = await Seed();

        var dto = await _controller.Team(a.TeamID, CancellationToken.None);

        Assert.That(dto, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(dto!.TeamName, Is.EqualTo("Alpha"));
            Assert.That(dto.Wins, Is.EqualTo(1));
            Assert.That(dto.Losses, Is.EqualTo(2));
            Assert.That(dto.TBD, Is.EqualTo(2));
            Assert.That(dto.Players.Select(p => p.UserID), Is.EqualTo(new[] { capA.Id, p2.Id, p1.Id }));
            Assert.That(dto.Players.Select(p => p.Gamertag), Is.EqualTo(new[] { "CapA", "P2", "P1" }));
            Assert.That(dto.Players[0].DraftCaptainOrder, Is.EqualTo(1));
            Assert.That(dto.Players[1].DraftRound, Is.EqualTo(1));
            Assert.That(dto.Players.Select(p => p.TeamPlayerID), Is.All.GreaterThan(0));
        });
    }

    [Test]
    public async Task Matches_Should_ListHomeAndAway_FromTeamPerspective_UnplayedFirst()
    {
        var (_, a, b, c, _, _, _) = await Seed();

        var matches = await _controller.Matches(a.TeamID, CancellationToken.None);

        Assert.That(matches, Has.Length.EqualTo(5));
        Assert.That(matches.Select(m => m.ScheduledTime), Is.EqualTo(new DateTime?[]
        {
            new DateTime(2025, 2, 1), new DateTime(2025, 2, 10), // unplayed, by date
            new DateTime(2025, 1, 1), new DateTime(2025, 1, 2), new DateTime(2025, 1, 3),
        }));
        Assert.Multiple(() =>
        {
            Assert.That(matches[0].OtherTeamID, Is.EqualTo(b.TeamID));
            Assert.That(matches[0].Result, Is.Null);
            Assert.That(matches[1].OtherTeamName, Is.EqualTo("Charlie"));
            Assert.That(matches[1].BestOf, Is.EqualTo(5));

            // home win: perspective of A
            Assert.That((matches[2].Score, matches[2].OtherScore, matches[2].Result, matches[2].OtherResult, matches[2].OtherTeamName),
                Is.EqualTo(((int?)2, (int?)0, (SeasonMatchResult?)SeasonMatchResult.Won, (SeasonMatchResult?)SeasonMatchResult.Loss, "Bravo")));
            // away loss: scores are swapped to A's perspective
            Assert.That((matches[3].Score, matches[3].OtherScore, matches[3].Result, matches[3].OtherTeamID),
                Is.EqualTo(((int?)1, (int?)2, (SeasonMatchResult?)SeasonMatchResult.Loss, (int?)c.TeamID)));
            Assert.That(matches[4].Result, Is.EqualTo(SeasonMatchResult.Forfeit));
            Assert.That(matches[4].Score, Is.Null);
        });
    }
}
