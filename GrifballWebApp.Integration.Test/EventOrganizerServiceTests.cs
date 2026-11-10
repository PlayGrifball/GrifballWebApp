using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.EventOrganizer;

namespace GrifballWebApp.Integration.Test;

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
        _context = await TestDatabase.NewGrifballContext();
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
        var season1 = Season.Seed(seasonName: "Season 1").WithDates(seasonStart: now.AddDays(10), seasonEnd: now.AddDays(40), signupsOpen: now.AddDays(-5), signupsClose: now.AddDays(5), draftStart: now.AddDays(7));

        var season2 = Season.Seed(seasonName: "Season 2").WithDates(seasonStart: now.AddDays(50), seasonEnd: now.AddDays(80), signupsOpen: now.AddDays(35), signupsClose: now.AddDays(45), draftStart: now.AddDays(47));

        _context.Seasons.AddRange(season1, season2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeasons(CancellationToken.None);

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Any(s => s.SeasonName == "Season 1"), Is.True);
            Assert.That(result.Any(s => s.SeasonName == "Season 2"), Is.True);
        }
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
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: now.AddDays(10), seasonEnd: now.AddDays(40), signupsOpen: now.AddDays(-5), signupsClose: now.AddDays(5), draftStart: now.AddDays(7));

        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSeason(season.SeasonID, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.SeasonName, Is.EqualTo("Test Season"));
            Assert.That(result.SeasonID, Is.EqualTo(season.SeasonID));
        }
    }

    [Test]
    public async Task UpsertSeason_Should_CreateNewSeason_When_SeasonIDIsZero()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new SeasonDto
        {
            GameVersionID = 6,
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
        var season = Season.Seed(seasonName: "Old Name").WithDates(seasonStart: now.AddDays(10), seasonEnd: now.AddDays(40), signupsOpen: now.AddDays(-5), signupsClose: now.AddDays(5), draftStart: now.AddDays(7));

        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var dto = new SeasonDto
        {
            GameVersionID = 6,
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
            GameVersionID = 6,
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
        var user = User.Seed(userName: "testuser", displayName: "Test User");
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: now.AddDays(10), seasonEnd: now.AddDays(40), signupsOpen: now.AddDays(-5), signupsClose: now.AddDays(5), draftStart: now.AddDays(7));

        _context.Users.Add(user);
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();

        var signup1 = SeasonSignup.Seed(season.SeasonID, user.Id, timestamp: now);
        _context.SeasonSignups.Add(signup1);
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
        var dto = new SeasonDto { GameVersionID = 6, SeasonName = "Draft Season", SeasonStart = now, SeasonEnd = now.AddDays(30), SignupsOpen = now, SignupsClose = now, DraftStart = now };

        var seasonId = await _service.UpsertSeason(dto, CancellationToken.None);
        var created = await _service.GetSeason(seasonId, CancellationToken.None);

        var publicAt = new DateTime(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc);
        dto.SeasonID = seasonId;
        dto.PublicAt = publicAt;
        await _service.UpsertSeason(dto, CancellationToken.None);
        var updated = await _service.GetSeason(seasonId, CancellationToken.None);
        var listed = await _service.GetSeasons(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created!.PublicAt, Is.Null, "A new season is not announced");
            Assert.That(updated!.PublicAt, Is.EqualTo(publicAt));
            Assert.That(listed.Single().PublicAt, Is.EqualTo(publicAt));
        }
    }

    private async Task<Season> HistoricalSeason(string name)
    {
        var season = Season.Seed(seasonName: name, historicalSourceID: "wl19/" + name).WithDates(seasonStart: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), seasonEnd: new DateTime(2019, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        return season;
    }

    [Test]
    public async Task UpsertSeason_AnEditNamingNoGame_KeepsTheSeasonsGame()
    {
        var id = await _service.UpsertSeason(Dto("Kept Game"));
        var edit = Dto("Kept Game", id);
        edit.GameVersionID = null;

        await _service.UpsertSeason(edit);

        _context.ChangeTracker.Clear();
        Assert.That((await _context.Seasons.AsNoTracking().SingleAsync(s => s.SeasonID == id)).GameVersionID, Is.EqualTo(6));
    }

    private static SeasonDto Dto(string name, int seasonId = 0)
    {
        var now = DateTime.UtcNow;
        return new SeasonDto { GameVersionID = 6, SeasonID = seasonId, SeasonName = name, SeasonStart = now.AddDays(10), SeasonEnd = now.AddDays(40), SignupsOpen = now, SignupsClose = now.AddDays(5), DraftStart = now.AddDays(7) };
    }

    [Test]
    public async Task HistoricalSeasons_AreListedForTheOrganizer_LikeAnyOther()
    {
        var season = await HistoricalSeason("Winter League 2019");

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await _service.GetSeason(season.SeasonID))!.SeasonName, Is.EqualTo("Winter League 2019"));
            Assert.That((await _service.GetSeasons()).Select(s => s.SeasonID), Is.EqualTo(new[] { season.SeasonID }));
        }
    }

    [Test]
    public async Task HistoricalSeasons_AreNotTheOrganizers_ToEdit()
    {
        var season = await HistoricalSeason("Winter League 2019");

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpsertSeason(Dto("Renamed", season.SeasonID)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((ex!.Status, ex.Message), Is.EqualTo((400, "That season was imported from the historical archive and can't be edited here.")));
            Assert.That((season.SeasonName, season.SeasonStart), Is.EqualTo(("Winter League 2019", new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc))),
                "Refused by Season.Schedule before anything changed");
        }
    }

    [Test]
    public async Task ANewSeason_NamedLikeAHistoricalOne_SaysSo()
    {
        await HistoricalSeason("Winter League 2019");

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpsertSeason(Dto("winter league 2019")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Status, Is.EqualTo(409));
            Assert.That(ex.Message, Does.Contain("A historical season of this league is already named"));
        }
    }

    [Test]
    public async Task DatesInAnOrderThatCantHappen_AreRefused_AndTheSeasonKeepsItsOwn()
    {
        var id = await _service.UpsertSeason(Dto("Season 1"));
        var saved = (await _service.GetSeason(id))!;
        _context.ChangeTracker.Clear();

        async Task<string> Refused(Action<SeasonDto> change)
        {
            var dto = Dto("Season 1", id);
            change(dto);
            var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpsertSeason(dto));
            _context.ChangeTracker.Clear();
            return ex!.Message;
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Refused(d => d.SignupsClose = d.SignupsOpen.AddMinutes(-1)), Is.EqualTo("Signups can't close before they open."));
            Assert.That(await Refused(d => d.DraftStart = d.SeasonStart.AddMinutes(1)), Is.EqualTo("The draft can't start after the season does."));
            Assert.That(await Refused(d => d.SeasonEnd = d.SeasonStart.AddMinutes(-1)), Is.EqualTo("The season can't end before it starts."));
            Assert.That((await _service.GetSeason(id))!.SeasonEnd, Is.EqualTo(saved.SeasonEnd), "Unchanged");
        }
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

    [Test]
    public async Task ANewSeason_NeedsItsGame_AndKeepsItsGameLeagueAndDivision()
    {
        var noGame = Dto("No Game");
        noGame.GameVersionID = null;
        var refused = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpsertSeason(noGame));
        Assert.That(refused!.Message, Is.EqualTo("Choose the game the season is played on."));

        var gwl = await _context.Leagues.SingleAsync(l => l.Slug == "gwl");
        var dto = Dto("Pro Season");
        dto.GameVersionID = 1;
        dto.LeagueID = gwl.LeagueID;
        dto.Division = " Pro ";
        var id = await _service.UpsertSeason(dto);

        var read = (await _service.GetSeason(id))!;
        Assert.That((read.GameVersionID, read.LeagueID, read.Division), Is.EqualTo(((int?)1, gwl.LeagueID, "Pro")));

        var badGame = Dto("Bad", id);
        badGame.GameVersionID = 999;
        await Assert.ThatAsync(() => _service.UpsertSeason(badGame), Throws.TypeOf<SeasonRefusedException>());
        var badLeague = Dto("Bad", id);
        badLeague.LeagueID = 999;
        await Assert.ThatAsync(() => _service.UpsertSeason(badLeague), Throws.TypeOf<SeasonRefusedException>());
    }

    [Test]
    public async Task TheEditorsOptions_AreTheGamesAndLeagues()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That((await _service.GetGameVersions()).Select(g => g.Name), Does.Contain("Halo Infinite").And.Contain("Halo 3"));
            Assert.That((await _service.GetLeagues()).Select(l => l.Name), Does.Contain("AGLA"));
        }
    }

    [Test]
    public async Task ASeasonsFormat_IsTheOrganizers_ToSet_LeagueSeasonByDefault()
    {
        var id = await _service.UpsertSeason(Dto("League"));
        var tournament = Dto("Respawn");
        tournament.Format = SeasonFormat.Tournament;
        var tournamentID = await _service.UpsertSeason(tournament);
        var changed = Dto("League", id);
        changed.Format = SeasonFormat.Tournament;
        await _service.UpsertSeason(changed);

        var bad = Dto("Bad", id);
        bad.Format = (SeasonFormat)99;

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await _service.GetSeason(tournamentID))!.Format, Is.EqualTo(SeasonFormat.Tournament));
            Assert.That((await _service.GetSeasons()).Single(s => s.SeasonID == id).Format, Is.EqualTo(SeasonFormat.Tournament));
            await Assert.ThatAsync(() => _service.UpsertSeason(bad), Throws.TypeOf<SeasonRefusedException>().With.Message.EqualTo("That season format doesn't exist."));
        }
        Assert.That(new SeasonDto().Format, Is.EqualTo(SeasonFormat.LeagueSeason));
    }
}
