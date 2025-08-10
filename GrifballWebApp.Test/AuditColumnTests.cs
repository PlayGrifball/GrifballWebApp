using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Database.Services;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
public class AuditColumnTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task Setup()
    {
        var sub = Substitute.For<ICurrentUserService>();
        sub.GetCurrentUserId().Returns(9001);
        var auditInterceptor = new Database.Interceptors.AuditInterceptor(sub);
        _context = await SetUpFixture.NewGrifballContext([auditInterceptor]);
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
    }

    [Test]
    public async Task CreatingAuditableEntity_SetsAuditFields()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season Audit",
            PublicAt = DateTime.UtcNow,
            SignupsOpen = DateTime.UtcNow,
            SignupsClose = DateTime.UtcNow.AddDays(7),
            DraftStart = DateTime.UtcNow.AddDays(8),
            SeasonStart = DateTime.UtcNow.AddDays(14),
            SeasonEnd = DateTime.UtcNow.AddDays(60)
        };

        // Act
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        // Assert - Check that audit fields are populated
        Assert.That(season.CreatedAt, Is.Not.EqualTo(default(DateTime)));
        Assert.That(season.ModifiedAt, Is.Not.EqualTo(default(DateTime)));
        Assert.That(season.CreatedAt, Is.EqualTo(season.ModifiedAt).Within(TimeSpan.FromSeconds(1)));
        Assert.That(season.CreatedByID, Is.EqualTo(9001));
        Assert.That(season.ModifiedByID, Is.EqualTo(9001));
    }

    [Test]
    public async Task UpdatingAuditableEntity_UpdatesModifiedFields()
    {
        // Arrange - Create initial entity
        var season = new Season
        {
            SeasonName = "Test Season Update",
            PublicAt = DateTime.UtcNow,
            SignupsOpen = DateTime.UtcNow,
            SignupsClose = DateTime.UtcNow.AddDays(7),
            DraftStart = DateTime.UtcNow.AddDays(8),
            SeasonStart = DateTime.UtcNow.AddDays(14),
            SeasonEnd = DateTime.UtcNow.AddDays(60)
        };

        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var originalCreatedAt = season.CreatedAt;
        var originalModifiedAt = season.ModifiedAt;

        // Small delay to ensure different timestamps
        await Task.Delay(100);

        // Act - Update the entity
        season.SeasonName = "Updated Test Season";
        await _context.SaveChangesAsync();

        // Assert
        Assert.That(season.CreatedAt, Is.EqualTo(originalCreatedAt), "CreatedAt should not change on update");
        Assert.That(season.ModifiedAt, Is.GreaterThan(originalModifiedAt), "ModifiedAt should be updated");
        Assert.That(season.CreatedByID, Is.EqualTo(9001));
        Assert.That(season.ModifiedByID, Is.EqualTo(9001));
    }

    [Test]
    public async Task Team_InheritsAuditFunctionality()
    {
        // Arrange
        var season = new Season
        {
            SeasonName = "Test Season for Team",
            PublicAt = DateTime.UtcNow,
            SignupsOpen = DateTime.UtcNow,
            SignupsClose = DateTime.UtcNow.AddDays(7),
            DraftStart = DateTime.UtcNow.AddDays(8),
            SeasonStart = DateTime.UtcNow.AddDays(14),
            SeasonEnd = DateTime.UtcNow.AddDays(60)
        };
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var team = new Team
        {
            TeamName = "Test Team",
            SeasonID = season.SeasonID,
            Season = season
        };

        // Act
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        // Assert
        Assert.That(team.CreatedAt, Is.Not.EqualTo(default(DateTime)));
        Assert.That(team.ModifiedAt, Is.Not.EqualTo(default(DateTime)));
        Assert.That(team.CreatedAt, Is.EqualTo(team.ModifiedAt).Within(TimeSpan.FromSeconds(1)));
        Assert.That(team.CreatedByID, Is.EqualTo(9001));
        Assert.That(team.ModifiedByID, Is.EqualTo(9001));
    }

    [Test]
    public async Task Match_InheritsAuditFunctionality()
    {
        // Arrange
        var match = new Match
        {
            MatchID = Guid.NewGuid(),
            StartTime = DateTime.UtcNow,
            Duration = TimeSpan.FromMinutes(15)
        };

        // Act
        _context.Matches.Add(match);
        await _context.SaveChangesAsync();

        // Assert
        Assert.That(match.CreatedAt, Is.Not.EqualTo(default(DateTime)));
        Assert.That(match.ModifiedAt, Is.Not.EqualTo(default(DateTime)));
        Assert.That(match.CreatedAt, Is.EqualTo(match.ModifiedAt).Within(TimeSpan.FromSeconds(1)));
        Assert.That(match.CreatedByID, Is.EqualTo(9001));
        Assert.That(match.ModifiedByID, Is.EqualTo(9001));
    }

    [Test]
    public async Task CreatingUser_SetsAuditFields()
    {
        // Arrange
        var user = new User
        {
            UserName = "testuser",
            DisplayName = "Test User",
            Email = "testuser@example.com"
        };

        // Act
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Assert - Check that audit fields are populated
        Assert.That(user.CreatedAt, Is.Not.EqualTo(default(DateTime)), "CreatedAt should be set");
        Assert.That(user.ModifiedAt, Is.Not.EqualTo(default(DateTime)), "ModifiedAt should be set");
        Assert.That(user.CreatedAt, Is.EqualTo(user.ModifiedAt).Within(TimeSpan.FromSeconds(1)), "CreatedAt and ModifiedAt should be nearly equal on creation");
        Assert.That(user.CreatedByID, Is.EqualTo(9001), "CreatedByID should match current user id");
        Assert.That(user.ModifiedByID, Is.EqualTo(9001), "ModifiedByID should match current user id");
    }
}