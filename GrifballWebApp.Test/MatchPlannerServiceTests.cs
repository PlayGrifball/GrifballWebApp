using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.MatchPlanner;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
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

        var team1 = new Team { SeasonID = season.SeasonID, TeamName = "Team 1" };
        var team2 = new Team { SeasonID = season.SeasonID, TeamName = "Team 2" };

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

        var team1 = new Team { SeasonID = season.SeasonID, TeamName = "Team 1" };
        var team2 = new Team { SeasonID = season.SeasonID, TeamName = "Team 2" };

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

        var team1 = new Team { SeasonID = season.SeasonID, TeamName = "Team 1" };
        var team2 = new Team { SeasonID = season.SeasonID, TeamName = "Team 2" };

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
        await Assert.DoesNotThrowAsync(() => _service.UpdateMatchTime(dto, CancellationToken.None));
    }

    /// <summary>Captains named each way the app names people, and a team without a captain.</summary>
    private async Task<Season> SeasonWithMatchesOfCaptainsWithoutGamertags()
    {
        var season = await _context.AddSeason();
        await _context.AddTeam(season, "Xbox", await _context.AddUser("xbox-user", gamertag: "Gamer Tag"), 1);
        await _context.AddTeam(season, "Discord", await _context.AddUser("discord-user", discordName: "discord name"), 2);
        await _context.AddTeam(season, "Display", await _context.AddUser("display-user", displayName: "Display Name"), 3);
        await _context.AddTeam(season, "Plain", await _context.AddUser("plain-user"), 4);
        _context.Teams.Add(new Team { SeasonID = season.SeasonID, TeamName = "No captain" });
        await _context.SaveChangesAsync();
        await _service.CreateSeasonMatches(season.SeasonID, 1, 1, CancellationToken.None);
        _context.ChangeTracker.Clear();
        return season;
    }

    private static readonly string[] CaptainNames = ["", "discord name", "Display Name", "Gamer Tag", "plain-user"];

    [Test]
    public async Task GetUnscheduledMatches_Should_NameCaptainsWithoutAGamertag()
    {
        var season = await SeasonWithMatchesOfCaptainsWithoutGamertags();

        var result = await _service.GetUnscheduledMatches(season.SeasonID, CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(20));
        Assert.That(result.Select(m => m.HomeCaptain).Distinct(), Is.EquivalentTo(CaptainNames));
        Assert.That(result.Select(m => m.AwayCaptain).Distinct(), Is.EquivalentTo(CaptainNames));
        Assert.That(result.Select(m => m.HomeCaptain), Is.Ordered, "ordered by home captain");
    }

    [Test]
    public async Task GetScheduledMatches_Should_NameCaptainsWithoutAGamertag()
    {
        var season = await SeasonWithMatchesOfCaptainsWithoutGamertags();
        await _context.SeasonMatches.Where(m => m.SeasonID == season.SeasonID)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ScheduledTime, new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc)));

        var result = await _service.GetScheduledMatches(season.SeasonID, CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(20));
        Assert.That(result.Select(m => m.HomeCaptain).Distinct(), Is.EquivalentTo(CaptainNames));
        Assert.That(result.Select(m => m.AwayCaptain).Distinct(), Is.EquivalentTo(CaptainNames));
    }
}
