using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Signups;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SignupsServiceTests
{
    private GrifballContext _context;
    private SignupsService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new SignupsService(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task GetSignupDateInfo_Should_ReturnEmptyArray_When_NoSeasonsExist()
    {
        // Arrange
        const int personId = 1;

        // Act
        var result = await _service.GetSignupDateInfo(personId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetSignupDateInfo_Should_ReturnSeasonInfo_When_SeasonIsOpenForSignups()
    {
        // Arrange
        const int personId = 1;
        var now = DateTime.UtcNow;
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = now.AddDays(10),
            SeasonEnd = now.AddDays(40),
            SignupsOpen = now.AddDays(-5),
            SignupsClose = now.AddDays(5)
        };

        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSignupDateInfo(personId, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].SeasonID, Is.EqualTo(season.SeasonID));
        Assert.That(result[0].IsSignedUp, Is.False);
        Assert.That(result[0].SignupsOpen, Is.EqualTo(season.SignupsOpen));
        Assert.That(result[0].SignupsClose, Is.EqualTo(season.SignupsClose));
    }

    [Test]
    public async Task GetSignupDateInfo_Should_ShowIsSignedUpTrue_When_UserIsSignedUp()
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
            SignupsClose = now.AddDays(5)
        };

        await _context.Users.AddAsync(user);
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var signup = new SeasonSignup
        {
            UserID = user.Id,
            SeasonID = season.SeasonID,
            Timestamp = now,
            TeamName = "Test Team"
        };

        await _context.SeasonSignups.AddAsync(signup);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSignupDateInfo(user.Id, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].IsSignedUp, Is.True);
    }

    [Test]
    public async Task GetSignups_Should_ReturnEmptyList_When_NoSignupsExist()
    {
        // Arrange
        const int seasonId = 1;

        // Act
        var result = await _service.GetSignups(seasonId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetSignups_Should_ReturnSignups_When_SignupsExist()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var user = new User { UserName = "testuser", DisplayName = "Test User" };
        var xboxUser = new XboxUser { Gamertag = "TestGamer", XboxUserID = 12345 };
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = now.AddDays(10),
            SeasonEnd = now.AddDays(40),
            SignupsOpen = now.AddDays(-5),
            SignupsClose = now.AddDays(5)
        };

        await _context.Users.AddAsync(user);
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        user.XboxUserID = xboxUser.XboxUserID;
        await _context.XboxUsers.AddAsync(xboxUser);
        await _context.SaveChangesAsync();

        var signup = new SeasonSignup
        {
            UserID = user.Id,
            SeasonID = season.SeasonID,
            Timestamp = now,
            TeamName = "Test Team",
            WillCaptain = true,
            RequiresAssistanceDrafting = false
        };

        await _context.SeasonSignups.AddAsync(signup);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSignups(season.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].UserID, Is.EqualTo(user.Id));
        Assert.That(result[0].SeasonID, Is.EqualTo(season.SeasonID));
        Assert.That(result[0].PersonName, Is.EqualTo("TestGamer"));
        Assert.That(result[0].TeamName, Is.EqualTo("Test Team"));
        Assert.That(result[0].WillCaptain, Is.True);
        Assert.That(result[0].RequiresAssistanceDrafting, Is.False);
    }

    [Test]
    public async Task GetSignup_Should_ReturnNull_When_SignupDoesNotExist()
    {
        // Arrange
        const int seasonId = 1;
        const int userId = 1;
        const int offset = 0;

        // Act
        var result = await _service.GetSignup(seasonId, offset, userId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetSignup_Should_ReturnSignup_When_SignupExists()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var user = new User { UserName = "testuser", DisplayName = "Test User" };
        var xboxUser = new XboxUser { Gamertag = "TestGamer", XboxUserID = 12345 };
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = now.AddDays(10),
            SeasonEnd = now.AddDays(40),
            SignupsOpen = now.AddDays(-5),
            SignupsClose = now.AddDays(5)
        };

        await _context.Users.AddAsync(user);
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        user.XboxUserID = xboxUser.XboxUserID;
        await _context.XboxUsers.AddAsync(xboxUser);
        await _context.SaveChangesAsync();

        var signup = new SeasonSignup
        {
            UserID = user.Id,
            SeasonID = season.SeasonID,
            Timestamp = now,
            TeamName = "Test Team",
            WillCaptain = true,
            RequiresAssistanceDrafting = false
        };

        await _context.SeasonSignups.AddAsync(signup);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSignup(season.SeasonID, 0, user.Id, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.UserID, Is.EqualTo(user.Id));
        Assert.That(result.SeasonID, Is.EqualTo(season.SeasonID));
        Assert.That(result.PersonName, Is.EqualTo("TestGamer"));
        Assert.That(result.TeamName, Is.EqualTo("Test Team"));
    }

    [Test]
    public async Task UpsertSignup_Should_CreateNewSignup_When_SignupDoesNotExist()
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
            SignupsClose = now.AddDays(5)
        };

        await _context.Users.AddAsync(user);
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var dto = new SignupRequestDto
        {
            UserID = user.Id,
            SeasonID = season.SeasonID,
            TeamName = "New Team",
            WillCaptain = true,
            RequiresAssistanceDrafting = false,
            Timeslots = Array.Empty<TimeslotDto>()
        };

        // Act
        await _service.UpsertSignup(dto, CancellationToken.None);

        // Assert
        var signups = await _service.GetSignups(season.SeasonID, CancellationToken.None);
        Assert.That(signups, Has.Count.EqualTo(1));
        Assert.That(signups[0].TeamName, Is.EqualTo("New Team"));
    }

    [Test]
    public async Task UpsertSignup_Should_UpdateExistingSignup_When_SignupExists()
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
            SignupsClose = now.AddDays(5)
        };

        await _context.Users.AddAsync(user);
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var signup = new SeasonSignup
        {
            UserID = user.Id,
            SeasonID = season.SeasonID,
            Timestamp = now.AddDays(-1),
            TeamName = "Old Team",
            WillCaptain = false
        };

        await _context.SeasonSignups.AddAsync(signup);
        await _context.SaveChangesAsync();

        var dto = new SignupRequestDto
        {
            UserID = user.Id,
            SeasonID = season.SeasonID,
            TeamName = "Updated Team",
            WillCaptain = true,
            RequiresAssistanceDrafting = false,
            Timeslots = Array.Empty<TimeslotDto>()
        };

        // Act
        await _service.UpsertSignup(dto, CancellationToken.None);

        // Assert
        var signups = await _service.GetSignups(season.SeasonID, CancellationToken.None);
        Assert.That(signups, Has.Count.EqualTo(1));
        Assert.That(signups[0].TeamName, Is.EqualTo("Updated Team"));
        Assert.That(signups[0].WillCaptain, Is.True);
    }

    [Test]
    public void UpsertSignup_Should_ThrowException_When_SeasonDoesNotExist()
    {
        // Arrange
        var dto = new SignupRequestDto
        {
            UserID = 1,
            SeasonID = 999,
            TeamName = "Test Team",
            WillCaptain = false,
            RequiresAssistanceDrafting = false,
            Timeslots = Array.Empty<TimeslotDto>()
        };

        // Act & Assert
        var ex = Assert.ThrowsAsync<Exception>(() => _service.UpsertSignup(dto, CancellationToken.None));
        Assert.That(ex!.Message, Is.EqualTo("Season does not exist"));
    }

    [Test]
    public async Task UpsertSignup_Should_ThrowSignupsClosedException_When_SignupsAreClosed()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var user = new User { UserName = "testuser", DisplayName = "Test User" };
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = now.AddDays(10),
            SeasonEnd = now.AddDays(40),
            SignupsOpen = now.AddDays(-30),
            SignupsClose = now.AddDays(-5) // Signups closed 5 days ago
        };

        await _context.Users.AddAsync(user);
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var dto = new SignupRequestDto
        {
            UserID = user.Id,
            SeasonID = season.SeasonID,
            TeamName = "Test Team",
            WillCaptain = false,
            RequiresAssistanceDrafting = false,
            Timeslots = Array.Empty<TimeslotDto>()
        };

        // Act & Assert
        Assert.ThrowsAsync<SignupsClosedException>(() => _service.UpsertSignup(dto, CancellationToken.None));
    }

    [Test]
    public async Task UpsertSignup_Should_ThrowSignupsClosedException_When_SignupsNotYetOpen()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var user = new User { UserName = "testuser", DisplayName = "Test User" };
        var season = new Season
        {
            SeasonName = "Test Season",
            SeasonStart = now.AddDays(10),
            SeasonEnd = now.AddDays(40),
            SignupsOpen = now.AddDays(5), // Signups open in 5 days
            SignupsClose = now.AddDays(10)
        };

        await _context.Users.AddAsync(user);
        await _context.Seasons.AddAsync(season);
        await _context.SaveChangesAsync();

        var dto = new SignupRequestDto
        {
            UserID = user.Id,
            SeasonID = season.SeasonID,
            TeamName = "Test Team",
            WillCaptain = false,
            RequiresAssistanceDrafting = false,
            Timeslots = Array.Empty<TimeslotDto>()
        };

        // Act & Assert
        Assert.ThrowsAsync<SignupsClosedException>(() => _service.UpsertSignup(dto, CancellationToken.None));
    }
}
