using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.MatchPlanner;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class MatchPlannerServiceTests
{
    private GrifballContext _context;
    private MatchPlannerService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new MatchPlannerService(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task CreateSeasonMatches_Should_CreateMatches_When_TeamsExist()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var user1 = new User { UserName = "user1", DisplayName = "User 1" };
        var user2 = new User { UserName = "user2", DisplayName = "User 2" };
        await _context.Users.AddRangeAsync(user1, user2);
        await _context.SaveChangesAsync();

        var captain1 = new TeamPlayer { UserID = user1.Id, DraftCaptainOrder = 1 };
        var captain2 = new TeamPlayer { UserID = user2.Id, DraftCaptainOrder = 2 };

        var team1 = new Team
        {
            SeasonID = season.SeasonID,
            TeamName = "Team 1",
            Captain = captain1
        };

        var team2 = new Team
        {
            SeasonID = season.SeasonID,
            TeamName = "Team 2",
            Captain = captain2
        };

        team1.TeamPlayers.Add(captain1);
        team2.TeamPlayers.Add(captain2);

        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        // Act
        await _service.CreateSeasonMatches(season.SeasonID, 1, 3, CancellationToken.None);

        // Assert
        var matches = await _context.SeasonMatches.Where(m => m.SeasonID == season.SeasonID).ToListAsync();
        Assert.That(matches, Has.Count.EqualTo(2)); // Each team has 1 home match
        Assert.That(matches.All(m => m.BestOf == 3), Is.True);
    }

    [Test]
    public async Task CreateSeasonMatches_Should_DeleteExistingMatches_When_MatchesAlreadyExist()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var user1 = new User { UserName = "user1", DisplayName = "User 1" };
        var user2 = new User { UserName = "user2", DisplayName = "User 2" };
        await _context.Users.AddRangeAsync(user1, user2);
        await _context.SaveChangesAsync();

        var captain1 = new TeamPlayer { UserID = user1.Id, DraftCaptainOrder = 1 };
        var captain2 = new TeamPlayer { UserID = user2.Id, DraftCaptainOrder = 2 };

        var team1 = new Team
        {
            SeasonID = season.SeasonID,
            TeamName = "Team 1",
            Captain = captain1
        };

        var team2 = new Team
        {
            SeasonID = season.SeasonID,
            TeamName = "Team 2",
            Captain = captain2
        };

        team1.TeamPlayers.Add(captain1);
        team2.TeamPlayers.Add(captain2);

        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        // Create initial matches
        await _service.CreateSeasonMatches(season.SeasonID, 1, 3, CancellationToken.None);
        var initialCount = await _context.SeasonMatches.CountAsync();

        // Act - recreate with different parameters
        await _service.CreateSeasonMatches(season.SeasonID, 2, 5, CancellationToken.None);

        // Assert
        var matches = await _context.SeasonMatches.Where(m => m.SeasonID == season.SeasonID).ToListAsync();
        Assert.That(matches, Has.Count.EqualTo(4)); // Each team has 2 home matches now
        Assert.That(matches.All(m => m.BestOf == 5), Is.True);
    }

    [Test]
    public async Task GetUnscheduledMatches_Should_ReturnEmptyList_When_NoMatchesExist()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetUnscheduledMatches(season.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetScheduledMatches_Should_ReturnEmptyList_When_NoScheduledMatchesExist()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetScheduledMatches(season.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task UpdateMatchTime_Should_UpdateScheduledTime_When_MatchExists()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = DateTime.UtcNow,
            SeasonEnd = DateTime.UtcNow.AddDays(30)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var user1 = new User { UserName = "user1", DisplayName = "User 1" };
        var user2 = new User { UserName = "user2", DisplayName = "User 2" };
        await _context.Users.AddRangeAsync(user1, user2);
        await _context.SaveChangesAsync();

        var captain1 = new TeamPlayer { UserID = user1.Id, DraftCaptainOrder = 1 };
        var captain2 = new TeamPlayer { UserID = user2.Id, DraftCaptainOrder = 2 };

        var team1 = new Team
        {
            SeasonID = season.SeasonID,
            TeamName = "Team 1",
            Captain = captain1
        };

        var team2 = new Team
        {
            SeasonID = season.SeasonID,
            TeamName = "Team 2",
            Captain = captain2
        };

        team1.TeamPlayers.Add(captain1);
        team2.TeamPlayers.Add(captain2);

        await _context.Teams.AddRangeAsync(team1, team2);
        await _context.SaveChangesAsync();

        var match = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = team1.TeamID,
            AwayTeamID = team2.TeamID,
            BestOf = 3
        };

        await _context.SeasonMatches.AddAsync(match);
        await _context.SaveChangesAsync();

        var newTime = DateTime.UtcNow.AddDays(5);
        var dto = new UpdateMatchTimeDto
        {
            SeasonMatchID = match.SeasonMatchID,
            Time = newTime
        };

        // Act
        await _service.UpdateMatchTime(dto, CancellationToken.None);

        // Assert
        var updatedMatch = await _context.SeasonMatches.FindAsync(match.SeasonMatchID);
        Assert.That(updatedMatch!.ScheduledTime, Is.EqualTo(newTime));
    }

    [Test]
    public async Task UpdateMatchTime_Should_DoNothing_When_MatchDoesNotExist()
    {
        // Arrange
        var dto = new UpdateMatchTimeDto
        {
            SeasonMatchID = 999,
            Time = DateTime.UtcNow
        };

        // Act & Assert - Should not throw
        Assert.DoesNotThrowAsync(() => _service.UpdateMatchTime(dto, CancellationToken.None));
    }
}
