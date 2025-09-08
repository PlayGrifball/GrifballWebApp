using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TeamStandingsServiceTests
{
    private GrifballContext _context;
    private TeamStandingsService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new TeamStandingsService(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task GetTeamStandings_Should_ReturnEmptyArray_When_NoTeamsExist()
    {
        // Arrange
        const int seasonId = 1;

        // Act
        var result = await _service.GetTeamStandings(seasonId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetTeamStandings_Should_ReturnTeamsWithZeroWinsAndLosses_When_TeamsExistWithoutMatches()
    {
        // Arrange
        const int seasonId = 1;
        await EnsureSeasonExists(seasonId);
        
        var team1 = new Team { SeasonID = seasonId, TeamName = "Team Alpha" };
        var team2 = new Team { SeasonID = seasonId, TeamName = "Team Beta" };
        
        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTeamStandings(seasonId);

        // Assert
        Assert.That(result, Has.Length.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].TeamName, Is.EqualTo("Team Alpha"));
            Assert.That(result[0].Wins, Is.EqualTo(0));
            Assert.That(result[0].Losses, Is.EqualTo(0));
            Assert.That(result[1].TeamName, Is.EqualTo("Team Beta"));
            Assert.That(result[1].Wins, Is.EqualTo(0));
            Assert.That(result[1].Losses, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task GetTeamStandings_Should_CalculateWinsCorrectly_When_TeamsHaveWonMatches()
    {
        // Arrange
        const int seasonId = 1;
        await EnsureSeasonExists(seasonId);
        
        var team1 = new Team { SeasonID = seasonId, TeamName = "Team Alpha" };
        var team2 = new Team { SeasonID = seasonId, TeamName = "Team Beta" };
        
        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        // Add matches where Team Alpha wins
        var homeMatch = new SeasonMatch 
        { 
            SeasonID = seasonId, 
            HomeTeamID = team1.TeamID, 
            AwayTeamID = team2.TeamID,
            HomeTeamResult = SeasonMatchResult.Won,
            AwayTeamResult = SeasonMatchResult.Loss
        };
        
        var awayMatch = new SeasonMatch 
        { 
            SeasonID = seasonId, 
            HomeTeamID = team2.TeamID, 
            AwayTeamID = team1.TeamID,
            HomeTeamResult = SeasonMatchResult.Loss,
            AwayTeamResult = SeasonMatchResult.Won
        };

        await _context.SeasonMatches.AddRangeAsync(homeMatch, awayMatch);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTeamStandings(seasonId);

        // Assert
        Assert.That(result, Has.Length.EqualTo(2));
        
        // Results should be ordered by wins descending
        var teamAlpha = result.First(r => r.TeamName == "Team Alpha");
        var teamBeta = result.First(r => r.TeamName == "Team Beta");
        
        Assert.Multiple(() =>
        {
            Assert.That(teamAlpha.Wins, Is.EqualTo(2), "Team Alpha should have 2 wins");
            Assert.That(teamAlpha.Losses, Is.EqualTo(0), "Team Alpha should have 0 losses");
            Assert.That(teamBeta.Wins, Is.EqualTo(0), "Team Beta should have 0 wins");
            Assert.That(teamBeta.Losses, Is.EqualTo(2), "Team Beta should have 2 losses");
            Assert.That(result[0].TeamName, Is.EqualTo("Team Alpha"), "Team with most wins should be first");
        });
    }

    [Test]
    public async Task GetTeamStandings_Should_CalculateLossesCorrectly_When_TeamsHaveLostMatches()
    {
        // Arrange
        const int seasonId = 1;
        await EnsureSeasonExists(seasonId);
        
        var team1 = new Team { SeasonID = seasonId, TeamName = "Team Alpha" };
        var team2 = new Team { SeasonID = seasonId, TeamName = "Team Beta" };
        
        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        // Add matches where teams have different results
        var match1 = new SeasonMatch 
        { 
            SeasonID = seasonId, 
            HomeTeamID = team1.TeamID, 
            AwayTeamID = team2.TeamID,
            HomeTeamResult = SeasonMatchResult.Loss,
            AwayTeamResult = SeasonMatchResult.Won
        };
        
        var match2 = new SeasonMatch 
        { 
            SeasonID = seasonId, 
            HomeTeamID = team1.TeamID, 
            AwayTeamID = team2.TeamID,
            HomeTeamResult = SeasonMatchResult.Forfeit,
            AwayTeamResult = SeasonMatchResult.Won
        };

        await _context.SeasonMatches.AddRangeAsync(match1, match2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTeamStandings(seasonId);

        // Assert
        Assert.That(result, Has.Length.EqualTo(2));
        
        var teamAlpha = result.First(r => r.TeamName == "Team Alpha");
        var teamBeta = result.First(r => r.TeamName == "Team Beta");
        
        Assert.Multiple(() =>
        {
            Assert.That(teamAlpha.Wins, Is.EqualTo(0), "Team Alpha should have 0 wins");
            Assert.That(teamAlpha.Losses, Is.EqualTo(2), "Team Alpha should have 2 losses (including forfeit)");
            Assert.That(teamBeta.Wins, Is.EqualTo(2), "Team Beta should have 2 wins");
            Assert.That(teamBeta.Losses, Is.EqualTo(0), "Team Beta should have 0 losses");
        });
    }

    [Test]
    public async Task GetTeamStandings_Should_OrderByWinsDescendingThenLossesAscending()
    {
        // Arrange
        const int seasonId = 1;
        await EnsureSeasonExists(seasonId);
        
        var teamA = new Team { SeasonID = seasonId, TeamName = "Team A" }; // 3 wins, 1 loss
        var teamB = new Team { SeasonID = seasonId, TeamName = "Team B" }; // 2 wins, 0 losses  
        var teamC = new Team { SeasonID = seasonId, TeamName = "Team C" }; // 2 wins, 1 loss
        var teamD = new Team { SeasonID = seasonId, TeamName = "Team D" }; // 1 win, 2 losses
        
        await _context.Teams.AddRangeAsync(teamA, teamB, teamC, teamD);
        await _context.SaveChangesAsync();

        // Create matches to achieve desired win/loss records
        var matches = new[]
        {
            // Team A: 3 wins, 1 loss
            new SeasonMatch { SeasonID = seasonId, HomeTeamID = teamA.TeamID, AwayTeamID = teamD.TeamID, HomeTeamResult = SeasonMatchResult.Won, AwayTeamResult = SeasonMatchResult.Loss },
            new SeasonMatch { SeasonID = seasonId, HomeTeamID = teamA.TeamID, AwayTeamID = teamD.TeamID, HomeTeamResult = SeasonMatchResult.Won, AwayTeamResult = SeasonMatchResult.Loss },
            new SeasonMatch { SeasonID = seasonId, HomeTeamID = teamA.TeamID, AwayTeamID = teamD.TeamID, HomeTeamResult = SeasonMatchResult.Won, AwayTeamResult = SeasonMatchResult.Loss },
            new SeasonMatch { SeasonID = seasonId, HomeTeamID = teamA.TeamID, AwayTeamID = teamB.TeamID, HomeTeamResult = SeasonMatchResult.Loss, AwayTeamResult = SeasonMatchResult.Won },

            // Team B: 2 wins (already has 1 from above), 0 losses
            new SeasonMatch { SeasonID = seasonId, HomeTeamID = teamB.TeamID, AwayTeamID = teamC.TeamID, HomeTeamResult = SeasonMatchResult.Won, AwayTeamResult = SeasonMatchResult.Loss },

            // Team C: 2 wins, 1 loss (already has 1 loss from above)
            new SeasonMatch { SeasonID = seasonId, HomeTeamID = teamC.TeamID, AwayTeamID = teamD.TeamID, HomeTeamResult = SeasonMatchResult.Won, AwayTeamResult = SeasonMatchResult.Loss },
            new SeasonMatch { SeasonID = seasonId, HomeTeamID = teamC.TeamID, AwayTeamID = teamD.TeamID, HomeTeamResult = SeasonMatchResult.Won, AwayTeamResult = SeasonMatchResult.Loss },
        };

        await _context.SeasonMatches.AddRangeAsync(matches);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTeamStandings(seasonId);

        // Assert
        Assert.That(result, Has.Length.EqualTo(4));
        
        // Expected order: Team A (3 wins), Team B (2 wins, 0 losses), Team C (2 wins, 1 loss), Team D (1 win)
        Assert.Multiple(() =>
        {
            Assert.That(result[0].TeamName, Is.EqualTo("Team A"), "Team A should be first with 3 wins");
            Assert.That(result[1].TeamName, Is.EqualTo("Team B"), "Team B should be second with 2 wins, 0 losses");
            Assert.That(result[2].TeamName, Is.EqualTo("Team C"), "Team C should be third with 2 wins, 1 loss");
            Assert.That(result[3].TeamName, Is.EqualTo("Team D"), "Team D should be last with fewest wins");
            
            Assert.That(result[0].Wins, Is.EqualTo(3));
            Assert.That(result[1].Wins, Is.EqualTo(2));
            Assert.That(result[1].Losses, Is.EqualTo(0));
            Assert.That(result[2].Wins, Is.EqualTo(2));
            Assert.That(result[2].Losses, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetTeamStandings_Should_OnlyReturnTeamsFromSpecifiedSeason()
    {
        // Arrange
        const int seasonId1 = 1;
        const int seasonId2 = 2;
        await EnsureSeasonExists(seasonId1);
        await EnsureSeasonExists(seasonId2);
        
        var teamSeason1 = new Team { SeasonID = seasonId1, TeamName = "Season 1 Team" };
        var teamSeason2 = new Team { SeasonID = seasonId2, TeamName = "Season 2 Team" };
        
        await _context.Teams.AddRangeAsync(teamSeason1, teamSeason2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetTeamStandings(seasonId1);

        // Assert
        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].TeamName, Is.EqualTo("Season 1 Team"));
    }

    [Test]
    public async Task GetTeamStandings_Should_RespectCancellationToken()
    {
        // Arrange
        const int seasonId = 1;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        // TaskCanceledException inherits from OperationCanceledException
        Assert.ThrowsAsync<TaskCanceledException>(() => _service.GetTeamStandings(seasonId, cts.Token));
    }

    private async Task EnsureSeasonExists(int seasonId)
    {
        if (!await _context.Seasons.AnyAsync(s => s.SeasonID == seasonId))
        {
            var season = new Season { SeasonID = seasonId, SeasonName = $"Season {seasonId}" };
            await _context.Seasons.AddAsync(season);
            await _context.SaveChangesWithoutContraints();
        }
    }
}