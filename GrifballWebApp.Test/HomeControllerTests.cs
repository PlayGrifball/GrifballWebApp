using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.MatchPlanner;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class HomeControllerTests
{
    private GrifballContext _context;
    private HomeController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _controller = new HomeController(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task CurrentAndFutureEvents_Should_ReturnEmptyArray_When_NoEventsExist()
    {
        // Arrange - no seasons in database

        // Act
        var result = await _controller.CurrentAndFutureEvents(CancellationToken.None);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task CurrentAndFutureEvents_Should_ReturnSignupEvents_When_SignupsAreOpen()
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
        var result = await _controller.CurrentAndFutureEvents(CancellationToken.None);

        // Assert
        var signupEvents = result.Where(e => e.EventType == EventType.Signup).ToArray();
        Assert.That(signupEvents, Has.Length.EqualTo(1));
        Assert.That(signupEvents[0].Name, Is.EqualTo("Test Season"));
        Assert.That(signupEvents[0].Start, Is.EqualTo(season.SignupsOpen));
        Assert.That(signupEvents[0].End, Is.EqualTo(season.SignupsClose));
    }

    [Test]
    public async Task CurrentAndFutureEvents_Should_ReturnDraftEvents_When_DraftHasNotStarted()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = now.AddDays(10),
            SeasonEnd = now.AddDays(40),
            SignupsOpen = now.AddDays(-10),
            SignupsClose = now.AddDays(-5),
            DraftStart = now.AddDays(5)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.CurrentAndFutureEvents(CancellationToken.None);

        // Assert
        var draftEvents = result.Where(e => e.EventType == EventType.Draft).ToArray();
        Assert.That(draftEvents, Has.Length.EqualTo(1));
        Assert.That(draftEvents[0].Name, Is.EqualTo("Test Season"));
        Assert.That(draftEvents[0].Start, Is.EqualTo(season.DraftStart));
        Assert.That(draftEvents[0].End, Is.EqualTo(season.SeasonStart));
    }

    [Test]
    public async Task CurrentAndFutureEvents_Should_ReturnSeasonEvents_When_SeasonIsActive()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var season = new Season
        {
            SeasonName = "Active Season",
            SeasonStart = now.AddDays(-5),
            SeasonEnd = now.AddDays(25),
            SignupsOpen = now.AddDays(-20),
            SignupsClose = now.AddDays(-10),
            DraftStart = now.AddDays(-7)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.CurrentAndFutureEvents(CancellationToken.None);

        // Assert
        var seasonEvents = result.Where(e => e.EventType == EventType.Season).ToArray();
        Assert.That(seasonEvents, Has.Length.EqualTo(1));
        Assert.That(seasonEvents[0].Name, Is.EqualTo("Active Season"));
        Assert.That(seasonEvents[0].Start, Is.EqualTo(season.SeasonStart));
        Assert.That(seasonEvents[0].End, Is.EqualTo(season.SeasonEnd));
    }

    [Test]
    public async Task CurrentAndFutureEvents_Should_NotReturnPastEvents_When_EventsHaveEnded()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var pastSeason = new Season
        {
            SeasonName = "Past Season",
            SeasonStart = now.AddDays(-30),
            SeasonEnd = now.AddDays(-10),
            SignupsOpen = now.AddDays(-50),
            SignupsClose = now.AddDays(-35),
            DraftStart = now.AddDays(-32)
        };

        await _context.Seasons.AddAsync(pastSeason);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.CurrentAndFutureEvents(CancellationToken.None);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task CurrentAndFutureEvents_Should_ReturnEventsOrderedByStartTime()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var season1 = new Season
        {
            SeasonName = "Later Season",
            SeasonStart = now.AddDays(20),
            SeasonEnd = now.AddDays(50),
            SignupsOpen = now.AddDays(10),
            SignupsClose = now.AddDays(18),
            DraftStart = now.AddDays(19)
        };

        var season2 = new Season
        {
            SeasonName = "Earlier Season",
            SeasonStart = now.AddDays(5),
            SeasonEnd = now.AddDays(35),
            SignupsOpen = now.AddDays(-5),
            SignupsClose = now.AddDays(3),
            DraftStart = now.AddDays(4)
        };

        await _context.Seasons.AddRangeAsync(season1, season2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.CurrentAndFutureEvents(CancellationToken.None);

        // Assert
        Assert.That(result, Has.Length.GreaterThan(0));
        Assert.That(result[0].Start, Is.LessThanOrEqualTo(result[^1].Start));
    }

    [Test]
    public async Task PastSeasons_Should_ReturnEmptyList_When_NoPastSeasons()
    {
        // Arrange
        var filter = new PaginationFilter();

        // Act
        var result = await _controller.PastSeasons(filter, CancellationToken.None);

        // Assert
        Assert.That(result.Results, Is.Empty);
        Assert.That(result.TotalCount, Is.EqualTo(0));
    }

    [Test]
    public async Task PastSeasons_Should_ReturnPastSeasons_When_SeasonsHaveEnded()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var pastSeason = new Season
        {
            SeasonName = "Past Season",
            SeasonStart = now.AddDays(-30),
            SeasonEnd = now.AddDays(-10),
            SignupsOpen = now.AddDays(-50),
            SignupsClose = now.AddDays(-35),
            DraftStart = now.AddDays(-32)
        };

        await _context.Seasons.AddAsync(pastSeason);
        await _context.SaveChangesAsync();

        var filter = new PaginationFilter();

        // Act
        var result = await _controller.PastSeasons(filter, CancellationToken.None);

        // Assert
        Assert.That(result.Results, Has.Length.EqualTo(1));
        Assert.That(result.Results[0].Name, Is.EqualTo("Past Season"));
        Assert.That(result.Results[0].EventType, Is.EqualTo(EventType.Season));
    }

    [Test]
    public async Task PastSeasons_Should_NotReturnActiveSeasons_When_SeasonsAreActive()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var activeSeason = new Season
        {
            SeasonName = "Active Season",
            SeasonStart = now.AddDays(-5),
            SeasonEnd = now.AddDays(25),
            SignupsOpen = now.AddDays(-20),
            SignupsClose = now.AddDays(-10),
            DraftStart = now.AddDays(-7)
        };

        await _context.Seasons.AddAsync(activeSeason);
        await _context.SaveChangesAsync();

        var filter = new PaginationFilter();

        // Act
        var result = await _controller.PastSeasons(filter, CancellationToken.None);

        // Assert
        Assert.That(result.Results, Is.Empty);
    }

    [Test]
    public async Task PastSeasons_Should_OrderByEndDateDescending_When_NoSortColumnProvided()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var season1 = new Season
        {
            SeasonName = "Older Season",
            SeasonStart = now.AddDays(-60),
            SeasonEnd = now.AddDays(-40),
            SignupsOpen = now.AddDays(-80),
            SignupsClose = now.AddDays(-65),
            DraftStart = now.AddDays(-62)
        };

        var season2 = new Season
        {
            SeasonName = "Newer Season",
            SeasonStart = now.AddDays(-30),
            SeasonEnd = now.AddDays(-10),
            SignupsOpen = now.AddDays(-50),
            SignupsClose = now.AddDays(-35),
            DraftStart = now.AddDays(-32)
        };

        await _context.Seasons.AddRangeAsync(season1, season2);
        await _context.SaveChangesAsync();

        var filter = new PaginationFilter();

        // Act
        var result = await _controller.PastSeasons(filter, CancellationToken.None);

        // Assert
        Assert.That(result.Results, Has.Length.EqualTo(2));
        Assert.That(result.Results[0].Name, Is.EqualTo("Newer Season"));
        Assert.That(result.Results[1].Name, Is.EqualTo("Older Season"));
    }

    [Test]
    public async Task PastSeasons_Should_RespectPaginationFilter()
    {
        // Arrange
        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            var season = new Season
            {
                SeasonName = $"Season {i}",
                SeasonStart = now.AddDays(-30 - i),
                SeasonEnd = now.AddDays(-10 - i),
                SignupsOpen = now.AddDays(-50 - i),
                SignupsClose = now.AddDays(-35 - i),
                DraftStart = now.AddDays(-32 - i)
            };
            await _context.Seasons.AddAsync(season);
        }
        await _context.SaveChangesAsync();

        var filter = new PaginationFilter { PageNumber = 1, PageSize = 2 };

        // Act
        var result = await _controller.PastSeasons(filter, CancellationToken.None);

        // Assert
        Assert.That(result.Results, Has.Length.EqualTo(2));
        Assert.That(result.TotalCount, Is.EqualTo(5));
    }
}
