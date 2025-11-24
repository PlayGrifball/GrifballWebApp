using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.EventOrganizer;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class EventOrganizerServiceTests
{
    private GrifballContext _context;
    private EventOrganizerService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new EventOrganizerService(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task GetSeasons_Should_ReturnEmptyList_When_NoSeasonsExist()
    {
        // Arrange - no seasons in database

        // Act
        var result = await _service.GetSeasons(CancellationToken.None);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetSeasons_Should_ReturnSeasons_When_SeasonsExist()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var season1 = new Season
        {
            SeasonName = "Season 1",
            SeasonStart = now.AddDays(10),
            SeasonEnd = now.AddDays(40),
            SignupsOpen = now.AddDays(-5),
            SignupsClose = now.AddDays(5),
            DraftStart = now.AddDays(7)
        };

        var season2 = new Season
        {
            SeasonName = "Season 2",
            SeasonStart = now.AddDays(50),
            SeasonEnd = now.AddDays(80),
            SignupsOpen = now.AddDays(35),
            SignupsClose = now.AddDays(45),
            DraftStart = now.AddDays(47)
        };

        await _context.Seasons.AddRangeAsync(season1, season2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeasons(CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Any(s => s.SeasonName == "Season 1"), Is.True);
        Assert.That(result.Any(s => s.SeasonName == "Season 2"), Is.True);
    }

    [Test]
    public async Task GetSeason_Should_ReturnNull_When_SeasonDoesNotExist()
    {
        // Arrange
        const int nonExistentSeasonId = 999;

        // Act
        var result = await _service.GetSeason(nonExistentSeasonId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetSeason_Should_ReturnSeason_When_SeasonExists()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = now.AddDays(10),
            SeasonEnd = now.AddDays(40),
            SignupsOpen = now.AddDays(-5),
            SignupsClose = now.AddDays(5),
            DraftStart = now.AddDays(7)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeason(season.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.SeasonName, Is.EqualTo("Test Season"));
        Assert.That(result.SeasonID, Is.EqualTo(season.SeasonID));
    }

    [Test]
    public async Task UpsertSeason_Should_CreateNewSeason_When_SeasonIDIsZero()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new SeasonDto
        {
            SeasonID = 0,
            SeasonName = "New Season",
            SeasonStart = now.AddDays(10),
            SeasonEnd = now.AddDays(40),
            SignupsOpen = now.AddDays(-5),
            SignupsClose = now.AddDays(5),
            DraftStart = now.AddDays(7)
        };

        // Act
        var seasonId = await _service.UpsertSeason(dto, CancellationToken.None);

        // Assert
        Assert.That(seasonId, Is.GreaterThan(0));
        var createdSeason = await _context.Seasons.FindAsync(seasonId);
        Assert.That(createdSeason, Is.Not.Null);
        Assert.That(createdSeason!.SeasonName, Is.EqualTo("New Season"));
    }

    [Test]
    public async Task UpsertSeason_Should_UpdateExistingSeason_When_SeasonIDExists()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var season = new Season
        {
            SeasonName = "Old Name",
            SeasonStart = now.AddDays(10),
            SeasonEnd = now.AddDays(40),
            SignupsOpen = now.AddDays(-5),
            SignupsClose = now.AddDays(5),
            DraftStart = now.AddDays(7)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var dto = new SeasonDto
        {
            SeasonID = season.SeasonID,
            SeasonName = "Updated Name",
            SeasonStart = now.AddDays(10),
            SeasonEnd = now.AddDays(40),
            SignupsOpen = now.AddDays(-5),
            SignupsClose = now.AddDays(5),
            DraftStart = now.AddDays(7)
        };

        // Act
        var seasonId = await _service.UpsertSeason(dto, CancellationToken.None);

        // Assert
        Assert.That(seasonId, Is.EqualTo(season.SeasonID));
        var updatedSeason = await _context.Seasons.FindAsync(seasonId);
        Assert.That(updatedSeason!.SeasonName, Is.EqualTo("Updated Name"));
    }

    [Test]
    public void UpsertSeason_Should_ThrowException_When_SeasonDoesNotExist()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new SeasonDto
        {
            SeasonID = 999,
            SeasonName = "Non-existent Season",
            SeasonStart = now.AddDays(10),
            SeasonEnd = now.AddDays(40),
            SignupsOpen = now.AddDays(-5),
            SignupsClose = now.AddDays(5),
            DraftStart = now.AddDays(7)
        };

        // Act & Assert
        var ex = Assert.ThrowsAsync<Exception>(() => _service.UpsertSeason(dto, CancellationToken.None));
        Assert.That(ex!.Message, Is.EqualTo("Season does not exist"));
    }

    [Test]
    public async Task GetSeasons_Should_IncludeSignupsCount_When_SignupsExist()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var user = new User { UserName = "testuser", DisplayName = "Test User" };
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = now.AddDays(10),
            SeasonEnd = now.AddDays(40),
            SignupsOpen = now.AddDays(-5),
            SignupsClose = now.AddDays(5),
            DraftStart = now.AddDays(7)
        };

        await _context.Users.AddAsync(user);
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var signup1 = new SeasonSignup { UserID = user.Id, SeasonID = season.SeasonID, Timestamp = now };
        await _context.SeasonSignups.AddAsync(signup1);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeasons(CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].SignupsCount, Is.EqualTo(1));
    }
}
