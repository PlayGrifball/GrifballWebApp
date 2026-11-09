using GrifballWebApp.Database;
using GrifballWebApp.Server.TeamStandings;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
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
        
        var team1 = Team.Seed(seasonID: seasonId, teamName: "Team Alpha");
        var team2 = Team.Seed(seasonID: seasonId, teamName: "Team Beta");
        
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
        
        var team1 = Team.Seed(seasonID: seasonId, teamName: "Team Alpha");
        var team2 = Team.Seed(seasonID: seasonId, teamName: "Team Beta");
        
        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        // Add matches where Team Alpha wins
        var homeMatch = SeasonMatch.Seed(seasonId, team1.TeamID, team2.TeamID).WonBy(SeasonMatchSide.Home);
        
        var awayMatch = SeasonMatch.Seed(seasonId, team2.TeamID, team1.TeamID).WonBy(SeasonMatchSide.Away);

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
        
        var team1 = Team.Seed(seasonID: seasonId, teamName: "Team Alpha");
        var team2 = Team.Seed(seasonID: seasonId, teamName: "Team Beta");
        
        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        // Add matches where teams have different results
        var match1 = SeasonMatch.Seed(seasonId, team1.TeamID, team2.TeamID).WonBy(SeasonMatchSide.Away);
        
        var match2 = SeasonMatch.Seed(seasonId, team1.TeamID, team2.TeamID).Forfeited(Forfeiter.Home);

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
        
        var teamA = Team.Seed(seasonID: seasonId, teamName: "Team A"); // 3 wins, 1 loss
        var teamB = Team.Seed(seasonID: seasonId, teamName: "Team B"); // 2 wins, 0 losses  
        var teamC = Team.Seed(seasonID: seasonId, teamName: "Team C"); // 2 wins, 1 loss
        var teamD = Team.Seed(seasonID: seasonId, teamName: "Team D"); // 1 win, 2 losses
        
        await _context.Teams.AddRangeAsync(teamA, teamB, teamC, teamD);
        await _context.SaveChangesAsync();

        // Create matches to achieve desired win/loss records
        var matches = new[]
        {
            // Team A: 3 wins, 1 loss
            SeasonMatch.Seed(seasonId, teamA.TeamID, teamD.TeamID).WonBy(SeasonMatchSide.Home),
            SeasonMatch.Seed(seasonId, teamA.TeamID, teamD.TeamID).WonBy(SeasonMatchSide.Home),
            SeasonMatch.Seed(seasonId, teamA.TeamID, teamD.TeamID).WonBy(SeasonMatchSide.Home),
            SeasonMatch.Seed(seasonId, teamA.TeamID, teamB.TeamID).WonBy(SeasonMatchSide.Away),

            // Team B: 2 wins (already has 1 from above), 0 losses
            SeasonMatch.Seed(seasonId, teamB.TeamID, teamC.TeamID).WonBy(SeasonMatchSide.Home),

            // Team C: 2 wins, 1 loss (already has 1 loss from above)
            SeasonMatch.Seed(seasonId, teamC.TeamID, teamD.TeamID).WonBy(SeasonMatchSide.Home),
            SeasonMatch.Seed(seasonId, teamC.TeamID, teamD.TeamID).WonBy(SeasonMatchSide.Home),
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
        
        var teamSeason1 = Team.Seed(seasonID: seasonId1, teamName: "Season 1 Team");
        var teamSeason2 = Team.Seed(seasonID: seasonId2, teamName: "Season 2 Team");
        
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
        // SqlClient throws TaskCanceledException, Npgsql its base OperationCanceledException
        await Assert.CatchAsync<OperationCanceledException>(() => _service.GetTeamStandings(seasonId, cts.Token));
    }

    private async Task EnsureSeasonExists(int seasonId)
    {
        if (!await _context.Seasons.AnyAsync(s => s.SeasonID == seasonId))
        {
            var season = Season.Seed(seasonID: seasonId, seasonName: $"Season {seasonId}");
            await _context.Seasons.AddAsync(season);
            await _context.SaveChangesWithoutConstraints();
        }
    }

    [Test]
    public async Task GetTeamStandings_ATeamsRecordedRecord_IsItsRecord_WhenItsMatchesAreNotInTheApp()
    {
        var season = await _context.AddSeason("Summer League 2010");
        _context.Teams.AddRange(
            Team.Seed(seasonID: season.SeasonID, teamName: "Relentless", recordedWins: 9, recordedLosses: 1),
            Team.Seed(seasonID: season.SeasonID, teamName: "Benchwarmers", recordedWins: 1, recordedLosses: 9));
        await _context.SaveChangesAsync();

        var standings = await _service.GetTeamStandings(season.SeasonID);

        Assert.That(standings.Select(s => (s.TeamName, s.Wins, s.Losses, s.RecordFromSource)), Is.EqualTo(new[]
        {
            ("Relentless", 9, 1, true),
            ("Benchwarmers", 1, 9, true),
        }));
    }
}
