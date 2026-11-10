using Microsoft.Extensions.Time.Testing;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Signups;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SignupsServiceTests
{
    // Postgres keeps times to the microsecond, SQL Server's datetime2 to 100ns.
    private static readonly TimeSpan DatabasePrecision = TimeSpan.FromMicroseconds(1);

    private GrifballContext _context;
    private SignupsService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await TestDatabase.NewGrifballContext();
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
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: now.AddDays(10), seasonEnd: now.AddDays(40), signupsOpen: now.AddDays(-5), signupsClose: now.AddDays(5));

        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSignupDateInfo(personId, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Length.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result[0].SeasonID, Is.EqualTo(season.SeasonID));
            Assert.That(result[0].IsSignedUp, Is.False);
            Assert.That(result[0].SignupsOpen, Is.EqualTo(season.SignupsOpen).Within(DatabasePrecision));
            Assert.That(result[0].SignupsClose, Is.EqualTo(season.SignupsClose).Within(DatabasePrecision));
        }
    }

    [Test]
    public async Task GetSignupDateInfo_Should_ShowIsSignedUpTrue_When_UserIsSignedUp()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var user = User.Seed(userName: "testuser", displayName: "Test User");
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: now.AddDays(10), seasonEnd: now.AddDays(40), signupsOpen: now.AddDays(-5), signupsClose: now.AddDays(5));

        _context.Users.Add(user);
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var signup = SeasonSignup.Seed(season.SeasonID, user.Id, timestamp: now, teamName: "Test Team");

        _context.SeasonSignups.Add(signup);
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
    public async Task GetSignups_NeverShowsALoginName()
    {
        // Public: a player with no gamertag, Discord name or display name is listed without a name, never by their login
        var now = DateTime.UtcNow;
        var user = User.Seed(userName: "private-login");
        var season = Season.Seed(seasonName: "Login Season").WithDates(seasonStart: now.AddDays(10), seasonEnd: now.AddDays(40), signupsOpen: now.AddDays(-5), signupsClose: now.AddDays(5));
        _context.Users.Add(user);
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        _context.SeasonSignups.Add(SeasonSignup.Seed(season.SeasonID, user.Id, timestamp: now, teamName: "Team"));
        await _context.SaveChangesAsync();

        var result = await _service.GetSignups(season.SeasonID, CancellationToken.None);

        Assert.That(result.Single().PersonName, Is.Empty);
    }

    [Test]
    public async Task GetSignups_Should_ReturnSignups_When_SignupsExist()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var user = User.Seed(userName: "testuser", displayName: "Test User");
        var xboxUser = XboxUser.FromXbox(12345, "TestGamer");
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: now.AddDays(10), seasonEnd: now.AddDays(40), signupsOpen: now.AddDays(-5), signupsClose: now.AddDays(5));

        _context.Users.Add(user);
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        user.TieToXbox(xboxUser.XboxUserID);
        _context.XboxUsers.Add(xboxUser);
        await _context.SaveChangesAsync();

        var signup = SeasonSignup.Seed(season.SeasonID, user.Id, timestamp: now, teamName: "Test Team", willCaptain: true, requiresAssistanceDrafting: false);

        _context.SeasonSignups.Add(signup);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSignups(season.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result[0].UserID, Is.EqualTo(user.Id));
            Assert.That(result[0].SeasonID, Is.EqualTo(season.SeasonID));
            Assert.That(result[0].PersonName, Is.EqualTo("TestGamer"));
            Assert.That(result[0].TeamName, Is.EqualTo("Test Team"));
            Assert.That(result[0].WillCaptain, Is.True);
            Assert.That(result[0].RequiresAssistanceDrafting, Is.False);
        }
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
        var user = User.Seed(userName: "testuser", displayName: "Test User");
        var xboxUser = XboxUser.FromXbox(12345, "TestGamer");
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: now.AddDays(10), seasonEnd: now.AddDays(40), signupsOpen: now.AddDays(-5), signupsClose: now.AddDays(5));

        _context.Users.Add(user);
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        user.TieToXbox(xboxUser.XboxUserID);
        _context.XboxUsers.Add(xboxUser);
        await _context.SaveChangesAsync();

        var signup = SeasonSignup.Seed(season.SeasonID, user.Id, timestamp: now, teamName: "Test Team", willCaptain: true, requiresAssistanceDrafting: false);

        _context.SeasonSignups.Add(signup);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSignup(season.SeasonID, 0, user.Id, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.UserID, Is.EqualTo(user.Id));
            Assert.That(result.SeasonID, Is.EqualTo(season.SeasonID));
            Assert.That(result.PersonName, Is.EqualTo("TestGamer"));
            Assert.That(result.TeamName, Is.EqualTo("Test Team"));
        }
    }

    [Test]
    public async Task UpsertSignup_Should_CreateNewSignup_When_SignupDoesNotExist()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var user = User.Seed(userName: "testuser", displayName: "Test User", xboxUser: XboxUser.FromXbox(22243, "TestGamer"));
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: now.AddDays(10), seasonEnd: now.AddDays(40), signupsOpen: now.AddDays(-5), signupsClose: now.AddDays(5));

        _context.Users.Add(user);
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var dto = new SignupRequestDto
        {
            UserID = user.Id,
            SeasonID = season.SeasonID,
            // Stored trimmed
            TeamName = " New Team  ",
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
        var user = User.Seed(userName: "testuser", displayName: "Test User", xboxUser: XboxUser.FromXbox(22282, "TestGamer"));
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: now.AddDays(10), seasonEnd: now.AddDays(40), signupsOpen: now.AddDays(-5), signupsClose: now.AddDays(5));

        _context.Users.Add(user);
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var signup = SeasonSignup.Seed(season.SeasonID, user.Id, timestamp: now.AddDays(-1), teamName: "Old Team", willCaptain: false);

        _context.SeasonSignups.Add(signup);
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(signups[0].TeamName, Is.EqualTo("Updated Team"));
            Assert.That(signups[0].WillCaptain, Is.True);
        }
    }

    [Test]
    public async Task UpsertSignup_Should_ThrowException_When_SeasonDoesNotExist()
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
        var ex = await Assert.ThrowsAsync<Exception>(() => _service.UpsertSignup(dto, CancellationToken.None));
        Assert.That(ex!.Message, Is.EqualTo("Season does not exist"));
    }

    [Test]
    public async Task UpsertSignup_Should_ThrowSignupsClosedException_When_SignupsAreClosed()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var user = User.Seed(userName: "testuser", displayName: "Test User");
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: now.AddDays(10), seasonEnd: now.AddDays(40), signupsOpen: now.AddDays(-30), signupsClose: now.AddDays(-5));

        _context.Users.Add(user);
        _context.Seasons.Add(season);
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
        await Assert.ThrowsAsync<SignupsClosedException>(() => _service.UpsertSignup(dto, CancellationToken.None));
    }

    [Test]
    public async Task UpsertSignup_AtTheCloseExactly_IsClosed_AtTheOpenExactly_IsOpen()
    {
        // Signups are open from their open, included, to their close, not included (Season.SignupsOpenAt)
        var open = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var close = open.AddDays(7);
        var user = User.Seed(userName: "testuser", displayName: "Test User", xboxUser: XboxUser.FromXbox(2535400000000299, "Phase"));
        var season = Season.Seed(seasonName: "Boundary Season")
            .WithDates(signupsOpen: open, signupsClose: close, draftStart: close, seasonStart: close.AddDays(1), seasonEnd: close.AddDays(30));
        _context.Users.Add(user);
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        SignupRequestDto Dto() => new() { UserID = user.Id, SeasonID = season.SeasonID, TeamName = "Team", Timeslots = [] };

        await Assert.ThatAsync(() => new SignupsService(_context, new FakeTimeProvider(close)).UpsertSignup(Dto()), Throws.TypeOf<SignupsClosedException>());
        await new SignupsService(_context, new FakeTimeProvider(open)).UpsertSignup(Dto());

        Assert.That(await _context.SeasonSignups.CountAsync(s => s.SeasonID == season.SeasonID), Is.EqualTo(1));
    }

    [Test]
    public async Task UpsertSignup_Should_ThrowSignupsClosedException_When_SignupsNotYetOpen()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var user = User.Seed(userName: "testuser", displayName: "Test User");
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: now.AddDays(10), seasonEnd: now.AddDays(40), signupsOpen: now.AddDays(5), signupsClose: now.AddDays(10));

        _context.Users.Add(user);
        _context.Seasons.Add(season);
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
        await Assert.ThrowsAsync<SignupsClosedException>(() => _service.UpsertSignup(dto, CancellationToken.None));
    }

    [Test]
    public async Task UpsertSignup_RefusesAPlayerWithoutAGamertag_OnTheSiteAndDiscordAlike()
    {
        var now = DateTime.UtcNow;
        var user = User.Seed(userName: "no-gamertag", displayName: "No Gamertag");
        var season = Season.Seed(seasonName: "Gamertag Season").WithDates(seasonStart: now.AddDays(10), seasonEnd: now.AddDays(40), signupsOpen: now.AddDays(-5), signupsClose: now.AddDays(5));
        _context.Users.Add(user);
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SignupNeedsGamertagException>(() => _service.UpsertSignup(new SignupRequestDto { UserID = user.Id, SeasonID = season.SeasonID }, CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo(SignupNeedsGamertagException.Reason));
            Assert.That(await _context.SeasonSignups.AnyAsync(), Is.False);
        }
    }
}
