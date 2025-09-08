using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Seasons;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SeasonServiceTests
{
    private GrifballContext _context;
    private SeasonService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new SeasonService(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task GetCurrentSeasonID_Should_ReturnZero_When_NoCurrentSeason()
    {
        // Arrange - no seasons in database

        // Act
        var result = await _service.GetCurrentSeasonID();

        // Assert
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public async Task GetCurrentSeasonID_Should_ReturnSeasonID_When_CurrentSeasonExists()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var currentSeason = new Season 
        { 
            SeasonName = "Current Season",
            SeasonStart = now.AddDays(-10),
            SeasonEnd = now.AddDays(10)
        };

        await _context.Seasons.AddAsync(currentSeason);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentSeasonID();

        // Assert
        Assert.That(result, Is.EqualTo(currentSeason.SeasonID));
    }

    [Test]
    public async Task GetCurrentSeasonID_Should_ReturnZero_When_SeasonsExistButNoneAreCurrent()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var pastSeason = new Season 
        { 
            SeasonName = "Past Season",
            SeasonStart = now.AddDays(-30),
            SeasonEnd = now.AddDays(-10)
        };
        var futureSeason = new Season 
        { 
            SeasonName = "Future Season",
            SeasonStart = now.AddDays(10),
            SeasonEnd = now.AddDays(30)
        };

        await _context.Seasons.AddRangeAsync(pastSeason, futureSeason);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentSeasonID();

        // Assert
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public async Task GetCurrentSeasonID_Should_ReturnFirstMatch_When_MultipleSeasonsOverlap()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var season1 = new Season 
        { 
            SeasonName = "Season 1",
            SeasonStart = now.AddDays(-20),
            SeasonEnd = now.AddDays(5)
        };
        var season2 = new Season 
        { 
            SeasonName = "Season 2",
            SeasonStart = now.AddDays(-5),
            SeasonEnd = now.AddDays(20)
        };

        await _context.Seasons.AddRangeAsync(season1, season2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentSeasonID();

        // Assert
        // Should return the first one found (FirstOrDefaultAsync behavior)
        Assert.That(result, Is.AnyOf(season1.SeasonID, season2.SeasonID));
        Assert.That(result, Is.Not.EqualTo(0));
    }

    [Test]
    public async Task GetCurrentSeasonID_Should_HandleExactBoundaryDates()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var exactStartSeason = new Season 
        { 
            SeasonName = "Exact Start Season",
            SeasonStart = now,
            SeasonEnd = now.AddDays(10)
        };
        var exactEndSeason = new Season 
        { 
            SeasonName = "Exact End Season",
            SeasonStart = now.AddDays(-10),
            SeasonEnd = now
        };

        await _context.Seasons.AddRangeAsync(exactStartSeason, exactEndSeason);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentSeasonID();

        // Assert
        // Either season should be valid since now is exactly on the boundary
        Assert.That(result, Is.AnyOf(exactStartSeason.SeasonID, exactEndSeason.SeasonID));
        Assert.That(result, Is.Not.EqualTo(0));
    }

    [Test]
    public async Task GetCurrentSeasonID_Should_RespectCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Assert.ThrowsAsync<TaskCanceledException>(() => _service.GetCurrentSeasonID(cts.Token));
    }

    [Test]
    public async Task GetSeasonName_Should_ReturnNull_When_SeasonDoesNotExist()
    {
        // Arrange
        const int nonExistentSeasonId = 999;

        // Act
        var result = await _service.GetSeasonName(nonExistentSeasonId);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetSeasonName_Should_ReturnSeasonName_When_SeasonExists()
    {
        // Arrange
        const string expectedSeasonName = "Test Season";
        var season = new Season 
        { 
            SeasonName = expectedSeasonName,
            SeasonStart = DateTime.UtcNow.AddDays(-10),
            SeasonEnd = DateTime.UtcNow.AddDays(10)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeasonName(season.SeasonID);

        // Assert
        Assert.That(result, Is.EqualTo(expectedSeasonName));
    }

    [Test]
    public async Task GetSeasonName_Should_ReturnCorrectName_When_MultipleSeasons()
    {
        // Arrange
        var season1 = new Season 
        { 
            SeasonName = "Season One",
            SeasonStart = DateTime.UtcNow.AddDays(-20),
            SeasonEnd = DateTime.UtcNow.AddDays(-10)
        };
        var season2 = new Season 
        { 
            SeasonName = "Season Two",
            SeasonStart = DateTime.UtcNow.AddDays(-5),
            SeasonEnd = DateTime.UtcNow.AddDays(5)
        };

        await _context.Seasons.AddRangeAsync(season1, season2);
        await _context.SaveChangesAsync();

        // Act & Assert
        Assert.Multiple(async () =>
        {
            var result1 = await _service.GetSeasonName(season1.SeasonID);
            var result2 = await _service.GetSeasonName(season2.SeasonID);
            
            Assert.That(result1, Is.EqualTo("Season One"));
            Assert.That(result2, Is.EqualTo("Season Two"));
        });
    }

    [Test]
    public async Task GetSeasonName_Should_HandleEmptySeasonName()
    {
        // Arrange
        var season = new Season 
        { 
            SeasonName = "",
            SeasonStart = DateTime.UtcNow.AddDays(-10),
            SeasonEnd = DateTime.UtcNow.AddDays(10)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeasonName(season.SeasonID);

        // Assert
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public async Task GetSeasonName_Should_RespectCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Assert.ThrowsAsync<TaskCanceledException>(() => _service.GetSeasonName(1, cts.Token));
    }
}