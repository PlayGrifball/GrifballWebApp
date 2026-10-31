using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.EventOrganizer;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
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
    public async Task UpsertSeason_Should_ThrowException_When_SeasonDoesNotExist()
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
        var ex = await Assert.ThrowsAsync<Exception>(() => _service.UpsertSeason(dto, CancellationToken.None));
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

    [Test]
    public async Task UpsertSeason_NewSeason_IsNotPublic_UntilPublicAtIsSet()
    {
        var now = DateTime.UtcNow;
        var dto = new SeasonDto { SeasonName = "Draft Season", SeasonStart = now, SeasonEnd = now.AddDays(30), SignupsOpen = now, SignupsClose = now, DraftStart = now };

        var seasonId = await _service.UpsertSeason(dto, CancellationToken.None);
        var created = await _service.GetSeason(seasonId, CancellationToken.None);

        var publicAt = new DateTime(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc);
        dto.SeasonID = seasonId;
        dto.PublicAt = publicAt;
        await _service.UpsertSeason(dto, CancellationToken.None);
        var updated = await _service.GetSeason(seasonId, CancellationToken.None);
        var listed = await _service.GetSeasons(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(created!.PublicAt, Is.Null, "A new season is not announced");
            Assert.That(updated!.PublicAt, Is.EqualTo(publicAt));
            Assert.That(listed.Single().PublicAt, Is.EqualTo(publicAt));
        });
    }

    private async Task<Season> HistoricalSeason(string name)
    {
        var season = new Season
        {
            SeasonName = name,
            IsHistorical = true,
            HistoricalSourceID = "wl19/" + name,
            SeasonStart = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonEnd = new DateTime(2019, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        };
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        return season;
    }

    private static SeasonDto Dto(string name, int seasonId = 0)
    {
        var now = DateTime.UtcNow;
        return new SeasonDto { SeasonID = seasonId, SeasonName = name, SeasonStart = now.AddDays(10), SeasonEnd = now.AddDays(40), SignupsOpen = now, SignupsClose = now.AddDays(5), DraftStart = now.AddDays(7) };
    }

    [Test]
    public async Task HistoricalSeasons_AreListedForTheOrganizer_LikeAnyOther()
    {
        var season = await HistoricalSeason("Winter League 2019");

        Assert.That((await _service.GetSeason(season.SeasonID))!.SeasonName, Is.EqualTo("Winter League 2019"));
        Assert.That((await _service.GetSeasons()).Select(s => s.SeasonID), Is.EqualTo(new[] { season.SeasonID }));
    }

    [Test]
    public async Task HistoricalSeasons_AreNotTheOrganizers_ToEdit()
    {
        var season = await HistoricalSeason("Winter League 2019");

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpsertSeason(Dto("Renamed", season.SeasonID)));

        Assert.That((ex!.Status, ex.Message), Is.EqualTo((400, "That season was imported from the historical archive and can't be edited here.")));
    }

    [Test]
    public async Task ANewSeason_NamedLikeAHistoricalOne_SaysSo()
    {
        await HistoricalSeason("Winter League 2019");

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpsertSeason(Dto("winter league 2019")));

        Assert.That(ex!.Status, Is.EqualTo(409));
        Assert.That(ex.Message, Does.Contain("A historical season of this league is already named"));
    }

    [Test]
    public async Task RenamingASeason_ToAnotherSeasonsName_SaysSo_ButKeepingItsOwnNameIsFine()
    {
        var first = await _service.UpsertSeason(Dto("Season 1"));
        await _service.UpsertSeason(Dto("Season 2"));

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpsertSeason(Dto("Season 2", first)));
        Assert.That(ex!.Message, Is.EqualTo("There is already a season named Season 2. Choose another name."));

        _context.ChangeTracker.Clear();
        Assert.That(await _service.UpsertSeason(Dto("Season 1", first)), Is.EqualTo(first));
    }
}
