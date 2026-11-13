using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Queries;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.Teams.Handlers;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>TeamService's draft moves (captains, picks): against the fake IApplicationDbContext. The DTO
/// (de)serialization edge cases stay in TeamServiceUnitTests.</summary>
[TestFixture]
internal sealed class TeamServiceDraftUnitTests : ApplicationDbContextTests
{
    private const int SeasonID = 1;

    private IPublisher _publisher = null!;
    private IDraftChannel _draftChannel = null!;
    private IDatabaseLocks _locks = null!;
    private TeamService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _publisher = Substitute.For<IPublisher>();
        _draftChannel = Substitute.For<IDraftChannel>();
        _locks = Substitute.For<IDatabaseLocks>();
        _service = new TeamService(Context, _publisher, _draftChannel, _locks);
    }

    private async Task SeedSeason() => await SeedSeasonUser(99);

    private async Task SeedSeasonUser(int userId, string? userName = null)
    {
        if (!await Context.Seasons.AnyAsync(s => s.SeasonID == SeasonID))
            Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: SeasonID));
        Context.Users.Add(User.Seed(userName: userName ?? $"user{userId}", displayName: $"User{userId}", id: userId));
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task AddCaptain_NotSignedUp_Throws()
    {
        await SeedSeasonUser(1);
        var dto = new CaptainPlacementDto { SeasonID = SeasonID, PersonID = 1, OrderNumber = 1 };

        var ex = await Assert.ThrowsAsync<TeamServiceException>(() => _service.AddCaptain(dto, resortOnly: false));
        Assert.That(ex!.Message, Is.EqualTo("Player is not signed up"));
    }

    [Test]
    public async Task AddCaptain_SignedUpNotCaptainYet_CreatesTeam()
    {
        await SeedSeasonUser(1, "alice");
        Context.Add(SeasonSignup.Seed(SeasonID, 1, teamName: "Alice's Crew"));
        await Context.SaveChangesAsync();
        var dto = new CaptainPlacementDto { SeasonID = SeasonID, PersonID = 1, OrderNumber = 1 };

        var teamName = await _service.AddCaptain(dto, resortOnly: false);

        Assert.That(teamName, Is.EqualTo("Alice's Crew"));
        Context.ChangeTracker.Clear();
        var team = await Context.Teams.Include(t => t.Captain).SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(team.TeamName, Is.EqualTo("Alice's Crew"));
            Assert.That(team.Captain!.UserID, Is.EqualTo(1));
            Assert.That(team.Captain.DraftCaptainOrder, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task AddCaptain_AlreadyACaptain_Throws()
    {
        await SeedSeasonUser(1, "alice");
        Context.Add(SeasonSignup.Seed(SeasonID, 1));
        await Context.SaveChangesAsync();
        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = SeasonID, PersonID = 1, OrderNumber = 1 }, resortOnly: false);

        var ex = await Assert.ThrowsAsync<TeamServiceException>(
            () => _service.AddCaptain(new CaptainPlacementDto { SeasonID = SeasonID, PersonID = 1, OrderNumber = 1 }, resortOnly: false));
        Assert.That(ex!.Message, Is.EqualTo("Player is already a captain"));
    }

    [Test]
    public async Task AddCaptain_AlreadyOnATeam_Throws()
    {
        await SeedSeasonUser(1, "alice");
        await SeedSeasonUser(2, "bob");
        Context.Add(SeasonSignup.Seed(SeasonID, 1));
        Context.Add(SeasonSignup.Seed(SeasonID, 2));
        await Context.SaveChangesAsync();
        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = SeasonID, PersonID = 1, OrderNumber = 1 }, resortOnly: false);
        Context.ChangeTracker.Clear();
        var team = await Context.Teams.SingleAsync();
        Context.Add(TeamPlayer.Seed(teamID: team.TeamID, userID: 2));
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<TeamServiceException>(
            () => _service.AddCaptain(new CaptainPlacementDto { SeasonID = SeasonID, PersonID = 2, OrderNumber = 2 }, resortOnly: false));
        Assert.That(ex!.Message, Is.EqualTo("Player is already on a team"));
    }

    [Test]
    public async Task AddCaptain_Resort_NotACaptain_Throws()
    {
        await SeedSeasonUser(1, "alice");
        var dto = new CaptainPlacementDto { SeasonID = SeasonID, PersonID = 1, OrderNumber = 1 };

        var ex = await Assert.ThrowsAsync<TeamServiceException>(() => _service.AddCaptain(dto, resortOnly: true));
        Assert.That(ex!.Message, Is.EqualTo("Player is not a captain"));
    }

    [Test]
    public async Task RemoveCaptain_NotACaptain_Throws()
    {
        await SeedSeasonUser(1, "alice");
        var dto = new RemoveCaptainDto { SeasonID = SeasonID, PersonID = 1 };

        var ex = await Assert.ThrowsAsync<TeamServiceException>(() => _service.RemoveCaptain(dto));
        Assert.That(ex!.Message, Is.EqualTo("Player is not a captain"));
    }

    [Test]
    public async Task RemoveCaptain_Captain_RemovesTeam()
    {
        await SeedSeasonUser(1, "alice");
        Context.Add(SeasonSignup.Seed(SeasonID, 1));
        await Context.SaveChangesAsync();
        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = SeasonID, PersonID = 1, OrderNumber = 1 }, resortOnly: false);

        await _service.RemoveCaptain(new RemoveCaptainDto { SeasonID = SeasonID, PersonID = 1 });

        Assert.That(await Context.Teams.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task AddPlayerToTeam_PlayerNotSignedUp_Throws()
    {
        await SeedSeasonUser(1, "alice");
        await SeedSeasonUser(2, "bob");
        Context.Add(SeasonSignup.Seed(SeasonID, 1));
        await Context.SaveChangesAsync();
        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = SeasonID, PersonID = 1, OrderNumber = 1 }, resortOnly: false);

        var dto = new AddPlayerToTeamRequestDto { SeasonID = SeasonID, PersonID = 2, CaptainID = 1 };
        var ex = await Assert.ThrowsAsync<TeamServiceException>(() => _service.AddPlayerToTeam(dto, requestorId: 1));
        Assert.That(ex!.Message, Is.EqualTo("This player has not signed up"));
    }

    [Test]
    public async Task AddPlayerToTeam_ByCaptain_AddsPlayer()
    {
        await SeedSeasonUser(1, "alice");
        await SeedSeasonUser(2, "bob");
        Context.Add(SeasonSignup.Seed(SeasonID, 1));
        Context.Add(SeasonSignup.Seed(SeasonID, 2));
        await Context.SaveChangesAsync();
        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = SeasonID, PersonID = 1, OrderNumber = 1 }, resortOnly: false);
        await _service.LockCaptains(SeasonID, true); // a captain picks once the draft order is set

        var dto = new AddPlayerToTeamRequestDto { SeasonID = SeasonID, PersonID = 2, CaptainID = 1 };
        await _service.AddPlayerToTeam(dto, requestorId: 1);

        var team = await Context.Teams.WithRoster().SingleAsync();
        Assert.That(team.TeamPlayers.Select(tp => tp.UserID), Does.Contain(2));
    }

    [Test]
    public async Task AddPlayerToTeam_ByNonCaptainNonCommissioner_Throws()
    {
        await SeedSeasonUser(1, "alice");
        await SeedSeasonUser(2, "bob");
        await SeedSeasonUser(3, "mallory");
        Context.Add(SeasonSignup.Seed(SeasonID, 1));
        Context.Add(SeasonSignup.Seed(SeasonID, 2));
        await Context.SaveChangesAsync();
        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = SeasonID, PersonID = 1, OrderNumber = 1 }, resortOnly: false);

        var dto = new AddPlayerToTeamRequestDto { SeasonID = SeasonID, PersonID = 2, CaptainID = 1 };
        var ex = await Assert.ThrowsAsync<TeamServiceException>(() => _service.AddPlayerToTeam(dto, requestorId: 3));
        Assert.That(ex!.Message, Does.Contain("commissioner"));
    }

    [Test]
    public async Task CaptainsTeamID_NoCaptain_ReturnsZero()
    {
        await SeedSeason();

        var teamID = await _service.CaptainsTeamID(SeasonID, 1);

        Assert.That(teamID, Is.Zero);
    }

    [Test]
    public async Task CaptainsTeamID_WithCaptain_ReturnsTeamID()
    {
        await SeedSeasonUser(1, "alice");
        Context.Add(SeasonSignup.Seed(SeasonID, 1));
        await Context.SaveChangesAsync();
        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = SeasonID, PersonID = 1, OrderNumber = 1 }, resortOnly: false);
        Context.ChangeTracker.Clear();
        var team = await Context.Teams.SingleAsync();

        var teamID = await _service.CaptainsTeamID(SeasonID, 1);

        Assert.That(teamID, Is.EqualTo(team.TeamID));
    }

    [Test]
    public async Task AreCaptainsLocked_NoSuchSeason_Throws()
    {
        var ex = await Assert.ThrowsAsync<TeamServiceException>(() => _service.AreCaptainsLocked(999));
        Assert.That(ex!.Message, Is.EqualTo("Season does not exist"));
    }

    [Test]
    public async Task LockCaptains_NoSuchSeason_Throws()
    {
        var ex = await Assert.ThrowsAsync<TeamServiceException>(() => _service.LockCaptains(999, true));
        Assert.That(ex!.Message, Is.EqualTo("Season does not exist"));
    }

    [Test]
    public async Task LockCaptains_TogglesLock()
    {
        await SeedSeason();

        await _service.LockCaptains(SeasonID, true);
        var lockedState = await _service.AreCaptainsLocked(SeasonID);

        await _service.LockCaptains(SeasonID, false);
        var unlockedState = await _service.AreCaptainsLocked(SeasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(lockedState, Is.True);
            Assert.That(unlockedState, Is.False);
        }
    }

    [Test]
    public async Task AddCaptain_CaptainsLocked_Throws()
    {
        await SeedSeasonUser(1, "alice");
        await _service.LockCaptains(SeasonID, true);
        var dto = new CaptainPlacementDto { SeasonID = SeasonID, PersonID = 1, OrderNumber = 1 };

        var ex = await Assert.ThrowsAsync<TeamServiceException>(() => _service.AddCaptain(dto, resortOnly: false));
        Assert.That(ex!.Message, Does.Contain("locked"));
    }

    [Test]
    public async Task GetPlayerPool_ExcludesPlayersAlreadyOnATeam()
    {
        await SeedSeasonUser(1, "alice");
        await SeedSeasonUser(2, "bob");
        Context.Add(SeasonSignup.Seed(SeasonID, 1));
        Context.Add(SeasonSignup.Seed(SeasonID, 2));
        await Context.SaveChangesAsync();
        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = SeasonID, PersonID = 1, OrderNumber = 1 }, resortOnly: false);

        var pool = await _service.GetPlayerPool(SeasonID);

        Assert.That(pool.Select(p => p.PersonID), Is.EqualTo(PoolOfBobOnly));
    }

    private static readonly int[] PoolOfBobOnly = [2];

    [Test]
    public async Task GetTeams_ReturnsCaptainAndRoster()
    {
        await SeedSeasonUser(1, "alice");
        await SeedSeasonUser(2, "bob");
        Context.Add(SeasonSignup.Seed(SeasonID, 1));
        Context.Add(SeasonSignup.Seed(SeasonID, 2));
        await Context.SaveChangesAsync();
        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = SeasonID, PersonID = 1, OrderNumber = 1 }, resortOnly: false);
        await _service.LockCaptains(SeasonID, true);
        await _service.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = SeasonID, PersonID = 2, CaptainID = 1 }, requestorId: 1);

        var teams = await _service.GetTeams(SeasonID);

        Assert.That(teams, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams[0].Captain.PersonID, Is.EqualTo(1));
            Assert.That(teams[0].Players.Select(p => p.PersonID), Does.Contain(2));
        }
    }
}
