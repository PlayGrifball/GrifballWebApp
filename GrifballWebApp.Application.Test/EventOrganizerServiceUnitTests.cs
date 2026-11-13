using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Test;

[TestFixture]
internal sealed class EventOrganizerServiceUnitTests : ApplicationDbContextTests
{
    private const int HaloInfiniteGameVersionID = 6;
    private EventOrganizerService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new EventOrganizerService(Context);
    }

    private static SeasonDto NewSeasonDto(string name = "New Season") => new()
    {
        SeasonName = name,
        GameVersionID = HaloInfiniteGameVersionID,
        SignupsOpen = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        SignupsClose = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
        DraftStart = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        SeasonStart = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
        SeasonEnd = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    [Test]
    public async Task GetSeasons_ReturnsSignupCount()
    {
        var season = Season.Seed(seasonName: "S1", seasonID: 1);
        Context.Seasons.Add(season);
        Context.Users.Add(Domain.Players.User.Seed(userName: "signer", id: 1));
        Context.Add(SeasonSignup.Seed(1, 1));
        await Context.SaveChangesAsync();

        var seasons = await _service.GetSeasons();

        Assert.That(seasons, Has.Count.EqualTo(1));
        Assert.That(seasons[0].SignupsCount, Is.EqualTo(1));
    }

    [Test]
    public async Task GetSeason_NotFound_ReturnsNull()
    {
        var season = await _service.GetSeason(999);

        Assert.That(season, Is.Null);
    }

    [Test]
    public async Task GetSeason_Found_ReturnsDto()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: 1, leagueID: League.DefaultLeagueID));
        await Context.SaveChangesAsync();

        var season = await _service.GetSeason(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(season!.SeasonID, Is.EqualTo(1));
            Assert.That(season.SeasonName, Is.EqualTo("S1"));
            Assert.That(season.LeagueID, Is.EqualTo(League.DefaultLeagueID));
        }
    }

    [Test]
    public async Task UpsertSeason_NewWithoutGameVersion_Throws()
    {
        var dto = NewSeasonDto();
        dto.GameVersionID = null;

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpsertSeason(dto));
        Assert.That(ex!.Message, Is.EqualTo("Choose the game the season is played on."));
    }

    [Test]
    public async Task UpsertSeason_BadSchedule_Throws()
    {
        var dto = NewSeasonDto();
        dto.SeasonEnd = dto.SeasonStart.AddDays(-1);

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpsertSeason(dto));
        Assert.That(ex!.Message, Is.EqualTo("The season can't end before it starts."));
    }

    [Test]
    public async Task UpsertSeason_UnknownGameVersion_Throws()
    {
        var dto = NewSeasonDto();
        dto.GameVersionID = 999;

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpsertSeason(dto));
        Assert.That(ex!.Message, Is.EqualTo("That game doesn't exist."));
    }

    [Test]
    public async Task UpsertSeason_UnknownLeague_Throws()
    {
        var dto = NewSeasonDto();
        dto.LeagueID = 999;

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpsertSeason(dto));
        Assert.That(ex!.Message, Is.EqualTo("That league doesn't exist."));
    }

    [Test]
    public async Task UpsertSeason_NameTaken_Throws()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "Taken", seasonID: 1));
        await Context.SaveChangesAsync();
        var dto = NewSeasonDto("Taken");

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpsertSeason(dto));
        Assert.That(ex!.Message, Does.Contain("already a season named"));
    }

    [Test]
    public async Task UpsertSeason_New_CreatesSeason()
    {
        var dto = NewSeasonDto();

        var seasonId = await _service.UpsertSeason(dto);

        var saved = await Context.Seasons.SingleAsync(s => s.SeasonID == seasonId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.SeasonName, Is.EqualTo("New Season"));
            Assert.That(saved.GameVersionID, Is.EqualTo(HaloInfiniteGameVersionID));
            Assert.That(saved.SeasonStart, Is.EqualTo(dto.SeasonStart));
        }
    }

    [Test]
    public async Task UpsertSeason_NoSuchSeasonToEdit_Throws()
    {
        var dto = NewSeasonDto();
        dto.SeasonID = 999;

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpsertSeason(dto));
        Assert.That(ex!.Message, Is.EqualTo("There is no such season"));
    }

    [Test]
    public async Task UpsertSeason_EditExisting_UpdatesFields()
    {
        var created = await _service.UpsertSeason(NewSeasonDto("Original"));
        var edit = NewSeasonDto("Renamed");
        edit.SeasonID = created;

        await _service.UpsertSeason(edit);

        var saved = await Context.Seasons.SingleAsync(s => s.SeasonID == created);
        Assert.That(saved.SeasonName, Is.EqualTo("Renamed"));
    }

    [Test]
    public async Task UpsertSeason_ImportedSeason_RefusesEdit()
    {
        var imported = Season.Imported("src/1", "Archive Season", League.DefaultLeagueID, HaloInfiniteGameVersionID, "", "https://x", SeasonFormat.LeagueSeason);
        Context.Seasons.Add(imported);
        await Context.SaveChangesAsync();
        var dto = NewSeasonDto("Archive Season Renamed");
        dto.SeasonID = imported.SeasonID;

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.UpsertSeason(dto));
        Assert.That(ex!.Message, Is.EqualTo(SeasonRefusedException.ImportedSeason));
    }

    [Test]
    public async Task UpsertSeason_CopyTeamsFromAnotherSeason_CopiesCaptain()
    {
        var sourceId = await _service.UpsertSeason(NewSeasonDto("Source"));
        Context.Users.Add(Domain.Players.User.Seed(userName: "captain", id: 1));
        var sourceTeam = Team.Drafted(sourceId, "Reclaimers", captainUserID: 1, captainOrder: 1);
        Context.Teams.Add(sourceTeam);
        await Context.SaveChangesAsync();
        sourceTeam.SetCaptain(1);
        await Context.SaveChangesAsync();

        var newDto = NewSeasonDto("Target");
        newDto.CopyFromSeasonID = sourceId;
        newDto.CopyTeams = true;
        var targetId = await _service.UpsertSeason(newDto);

        var copiedTeam = await Context.Teams.Include(t => t.Captain).SingleAsync(t => t.SeasonID == targetId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(copiedTeam.TeamName, Is.EqualTo("Reclaimers"));
            Assert.That(copiedTeam.Captain!.UserID, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task GetGameVersions_ReturnsSeededVersions()
    {
        var versions = await _service.GetGameVersions();

        Assert.That(versions.Select(v => v.GameVersionID), Does.Contain(HaloInfiniteGameVersionID));
    }

    [Test]
    public async Task GetLeagues_ReturnsSeededLeagues()
    {
        var leagues = await _service.GetLeagues();

        Assert.That(leagues.Select(l => l.LeagueID), Does.Contain(League.DefaultLeagueID));
    }
}
