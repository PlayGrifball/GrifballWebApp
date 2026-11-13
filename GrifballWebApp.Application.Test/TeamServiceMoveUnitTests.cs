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

/// <summary>
/// TeamService's draft moves beyond TeamServiceDraftUnitTests: removing and moving picks (within a team and traded
/// between teams, with each refusal), resorting captains, a taken team name, removing a captain with others left, and
/// the pick refusals for a captain (already on a team, before the lock, not their turn).
/// </summary>
[TestFixture]
internal sealed class TeamServiceMoveUnitTests : ApplicationDbContextTests
{
    private const int SeasonID = 1;
    private const int CaptainA = 1;
    private const int CaptainB = 2;
    private const int PickA1 = 3;
    private const int PickA2 = 4;
    private const int PickB1 = 5;
    private const int CommissionerID = 9;
    private const int CommissionerRoleID = 2; // RoleConfiguration's HasData
    private const string Connection = "conn-1";
    private static readonly int[] OnlyPickA2 = [PickA2];
    private static readonly int[] OnlyPickA1 = [PickA1];
    private static readonly int[] PickA2ThenA1 = [PickA2, PickA1];
    private static readonly int[] PickA2ThenB1 = [PickA2, PickB1];
    private static readonly int[] CaptainBThenA = [CaptainB, CaptainA];
    private static readonly int[] PickA1ThenA2 = [PickA1, PickA2];
    private static readonly int?[] RoundsOneTwo = [1, 2];

    private IPublisher _publisher = null!;
    private IDraftChannel _draftChannel = null!;
    private TeamService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _publisher = Substitute.For<IPublisher>();
        _draftChannel = Substitute.For<IDraftChannel>();
        _service = new TeamService(Context, _publisher, _draftChannel, Substitute.For<IDatabaseLocks>());
    }

    /// <summary>A season, users 1-5 signed up (team names as given), and commissioner 9.</summary>
    private async Task SeedSignups(string? teamNameA = null, string? teamNameB = null)
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: SeasonID));
        foreach (var id in new[] { CaptainA, CaptainB, PickA1, PickA2, PickB1, CommissionerID })
            Context.Users.Add(User.Seed(userName: $"user{id}", displayName: $"User{id}", id: id));
        await Context.SaveChangesAsync();
        Context.UserRoles.Add(new UserRole { UserId = CommissionerID, RoleId = CommissionerRoleID });
        Context.SeasonSignups.AddRange(
            SeasonSignup.Seed(SeasonID, CaptainA, teamName: teamNameA, willCaptain: teamNameA is not null),
            SeasonSignup.Seed(SeasonID, CaptainB, teamName: teamNameB, willCaptain: teamNameB is not null),
            SeasonSignup.Seed(SeasonID, PickA1),
            SeasonSignup.Seed(SeasonID, PickA2),
            SeasonSignup.Seed(SeasonID, PickB1));
        await Context.SaveChangesAsync();
    }

    private async Task AddCaptains()
    {
        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = SeasonID, PersonID = CaptainA, OrderNumber = 1 }, resortOnly: false);
        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = SeasonID, PersonID = CaptainB, OrderNumber = 2 }, resortOnly: false);
    }

    /// <summary>Captains 1 and 2 (in that order); team A picked 3 then 4, team B picked 5, all by the commissioner.</summary>
    private async Task SeedDraft()
    {
        await SeedSignups();
        await AddCaptains();
        await _service.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = SeasonID, CaptainID = CaptainA, PersonID = PickA1 }, CommissionerID);
        await _service.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = SeasonID, CaptainID = CaptainA, PersonID = PickA2 }, CommissionerID);
        await _service.AddPlayerToTeam(new AddPlayerToTeamRequestDto { SeasonID = SeasonID, CaptainID = CaptainB, PersonID = PickB1 }, CommissionerID);
        _draftChannel.ClearReceivedCalls();
        _publisher.ClearReceivedCalls();
    }

    /// <summary>The picks (not the captain) of the team <paramref name="captainID"/> captains, by draft round, read fresh.</summary>
    private async Task<List<TeamPlayer>> PicksOf(int captainID)
    {
        Context.ChangeTracker.Clear();
        var teamID = await _service.CaptainsTeamID(SeasonID, captainID);
        return await Context.Teams.Rosters().Where(tp => tp.TeamID == teamID && tp.UserID != captainID)
            .OrderBy(tp => tp.DraftRound).ToListAsync();
    }

    [Test]
    public async Task RemovePlayerFromTeam_Pick_RemovesItAndMovesTheLaterPicksUp()
    {
        await SeedDraft();
        var dto = new RemovePlayerFromTeamRequestDto { SeasonID = SeasonID, CaptainID = CaptainA, PersonID = PickA1 };

        await _service.RemovePlayerFromTeam(dto, Connection);

        var picks = await PicksOf(CaptainA);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(picks.Select(p => p.UserID), Is.EqualTo(OnlyPickA2));
            Assert.That(picks.Single().DraftRound, Is.EqualTo(1));
        }
        await _draftChannel.Received(1).PlayerRemovedAsync(_service, dto, Arg.Any<CancellationToken>());
        await _publisher.Received(1).Publish(Arg.Is<Notification<RemovePlayerFromTeamRequestDto>>(n => n.ConnectionId == Connection && n.Value == dto), CancellationToken.None);
    }

    [TestCase(CaptainA, PickB1)]
    [TestCase(CaptainA, CaptainA)]
    [TestCase(PickA1, PickA2)]
    public async Task RemovePlayerFromTeam_NotAPickOfThatCaptainsTeam_Throws(int captainID, int personID)
    {
        await SeedDraft();
        var dto = new RemovePlayerFromTeamRequestDto { SeasonID = SeasonID, CaptainID = captainID, PersonID = personID };

        var ex = await Assert.ThrowsAsync<TeamServiceException>(() => _service.RemovePlayerFromTeam(dto));

        Assert.That(ex!.Message, Is.EqualTo("Player is not on this team"));
        await _draftChannel.DidNotReceive().PlayerRemovedAsync(Arg.Any<TeamService>(), Arg.Any<RemovePlayerFromTeamRequestDto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MovePlayerToTeam_SameTeam_MovesThePickToTheRound()
    {
        await SeedDraft();
        var dto = new MovePlayerToTeamRequestDto { SeasonID = SeasonID, PreviousCaptainID = CaptainA, NewCaptainID = CaptainA, PersonID = PickA2, RoundNumber = 1 };

        await _service.MovePlayerToTeam(dto, Connection);

        var picks = await PicksOf(CaptainA);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(picks.Select(p => p.UserID), Is.EqualTo(PickA2ThenA1));
            Assert.That(picks.Select(p => p.DraftRound), Is.EqualTo(RoundsOneTwo));
        }
        await _draftChannel.Received(1).PlayerMovedAsync(_service, dto, Arg.Any<CancellationToken>());
        await _publisher.Received(1).Publish(Arg.Is<Notification<MovePlayerToTeamRequestDto>>(n => n.ConnectionId == Connection && n.Value == dto), CancellationToken.None);
    }

    [Test]
    public async Task MovePlayerToTeam_ToAnotherTeam_TradesThePick()
    {
        await SeedDraft();
        var dto = new MovePlayerToTeamRequestDto { SeasonID = SeasonID, PreviousCaptainID = CaptainA, NewCaptainID = CaptainB, PersonID = PickA2, RoundNumber = 1 };

        await _service.MovePlayerToTeam(dto);

        var picksA = await PicksOf(CaptainA);
        var picksB = await PicksOf(CaptainB);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(picksA.Select(p => p.UserID), Is.EqualTo(OnlyPickA1));
            Assert.That(picksB.Select(p => p.UserID), Is.EqualTo(PickA2ThenB1));
            Assert.That(picksB.Select(p => p.DraftRound), Is.EqualTo(RoundsOneTwo));
        }
        await _draftChannel.Received(1).PlayerMovedAsync(_service, dto, Arg.Any<CancellationToken>());
    }

    [TestCase(CaptainA, CaptainB, PickB1, 1, "Player was not on that team")]
    [TestCase(PickA1, CaptainB, PickA2, 1, "Player was not on that team")]
    [TestCase(CaptainA, PickB1, PickA2, 1, "New team does not exist")]
    [TestCase(CaptainA, CaptainB, PickA2, 5, "Round number is out of range")]
    [TestCase(CaptainA, CaptainA, PickA2, 3, "Round number is out of range")]
    [TestCase(CaptainA, CaptainA, PickB1, 1, "Player is not on that team")]
    [TestCase(PickA1, PickA1, PickA2, 1, "Player is not on that team")]
    public async Task MovePlayerToTeam_Refused_ThrowsAndChangesNothing(int previousCaptainID, int newCaptainID, int personID, int round, string message)
    {
        await SeedDraft();
        var dto = new MovePlayerToTeamRequestDto { SeasonID = SeasonID, PreviousCaptainID = previousCaptainID, NewCaptainID = newCaptainID, PersonID = personID, RoundNumber = round };

        var ex = await Assert.ThrowsAsync<TeamServiceException>(() => _service.MovePlayerToTeam(dto));

        var picksA = await PicksOf(CaptainA);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo(message));
            Assert.That(picksA.Select(p => p.UserID), Is.EqualTo(PickA1ThenA2));
        }
        await _draftChannel.DidNotReceive().PlayerMovedAsync(Arg.Any<TeamService>(), Arg.Any<MovePlayerToTeamRequestDto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddCaptain_Resort_ReordersTheCaptainsAndReturnsNull()
    {
        await SeedSignups();
        await AddCaptains();
        _publisher.ClearReceivedCalls();
        var dto = new CaptainPlacementDto { SeasonID = SeasonID, PersonID = CaptainB, OrderNumber = 1 };

        var name = await _service.AddCaptain(dto, resortOnly: true, signalRConnectionID: Connection);

        Context.ChangeTracker.Clear();
        var captains = await Context.Teams.Rosters().Where(tp => tp.DraftCaptainOrder != null).OrderBy(tp => tp.DraftCaptainOrder).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(name, Is.Null);
            Assert.That(captains.Select(c => c.UserID), Is.EqualTo(CaptainBThenA));
            Assert.That(captains.Select(c => c.DraftCaptainOrder), Is.EqualTo(RoundsOneTwo));
        }
        await _publisher.Received(1).Publish(Arg.Is<Notification<CaptainPlacementDto>>(n => n.ConnectionId == Connection && n.Value == dto), CancellationToken.None);
    }

    [Test]
    public async Task AddCaptain_TeamNameTaken_GivesTheNewTeamASuffix()
    {
        await SeedSignups(teamNameA: "Bombers", teamNameB: "Bombers");
        await _service.AddCaptain(new CaptainPlacementDto { SeasonID = SeasonID, PersonID = CaptainA, OrderNumber = 1 }, resortOnly: false);

        var name = await _service.AddCaptain(new CaptainPlacementDto { SeasonID = SeasonID, PersonID = CaptainB, OrderNumber = 2 }, resortOnly: false);

        Context.ChangeTracker.Clear();
        var names = await Context.Teams.AsNoTracking().Select(t => t.TeamName).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(name, Does.Match("^Bombers [0-9a-f]{4}$"));
            Assert.That(names, Is.EquivalentTo(new[] { "Bombers", name }));
        }
    }

    [Test]
    public async Task RemoveCaptain_WithAnotherCaptainLeft_MovesThemUpTheOrder()
    {
        await SeedSignups();
        await AddCaptains();

        await _service.RemoveCaptain(new RemoveCaptainDto { SeasonID = SeasonID, PersonID = CaptainA });

        Context.ChangeTracker.Clear();
        var captains = await Context.Teams.Rosters().Where(tp => tp.DraftCaptainOrder != null).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.Teams.CountAsync(), Is.EqualTo(1));
            Assert.That(captains.Single().UserID, Is.EqualTo(CaptainB));
            Assert.That(captains.Single().DraftCaptainOrder, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task AddPlayerToTeam_AlreadyOnATeam_Throws()
    {
        await SeedDraft();

        var ex = await Assert.ThrowsAsync<TeamServiceException>(() => _service.AddPlayerToTeam(
            new AddPlayerToTeamRequestDto { SeasonID = SeasonID, CaptainID = CaptainB, PersonID = PickA1 }, CommissionerID));

        Assert.That(ex!.Message, Is.EqualTo("This player is already on a team"));
    }

    [Test]
    public async Task AddPlayerToTeam_CaptainBeforeTheLock_Throws()
    {
        await SeedSignups();
        await AddCaptains();

        var ex = await Assert.ThrowsAsync<TeamServiceException>(() => _service.AddPlayerToTeam(
            new AddPlayerToTeamRequestDto { SeasonID = SeasonID, CaptainID = CaptainA, PersonID = PickA1 }, CaptainA));

        Assert.That(ex!.Message, Is.EqualTo("The draft has not started: the captains are not locked yet"));
    }

    [Test]
    public async Task AddPlayerToTeam_CaptainOutOfTurn_ThrowsNamingWhoseTurnItIs()
    {
        await SeedSignups();
        await AddCaptains();
        await _service.LockCaptains(SeasonID, true);

        var ex = await Assert.ThrowsAsync<TeamServiceException>(() => _service.AddPlayerToTeam(
            new AddPlayerToTeamRequestDto { SeasonID = SeasonID, CaptainID = CaptainB, PersonID = PickA1 }, CaptainB));

        Assert.That(ex!.Message, Is.EqualTo($"It is not your turn to be making a draft pick. It is {CaptainA}'s turn"));
    }

    [Test]
    public void TeamServiceException_KeepsItsInnerException()
    {
        var inner = new InvalidOperationException("inner");

        var ex = new TeamServiceException("outer", inner);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex.Message, Is.EqualTo("outer"));
            Assert.That(ex.InnerException, Is.SameAs(inner));
        }
    }

    [Test]
    public void Dtos_HoldTheirValues()
    {
        var move = new MovePlayerToTeamRequestDto { SeasonID = 1, PreviousCaptainID = 2, NewCaptainID = 3, PersonID = 4, RoundNumber = 5 };
        var remove = new RemovePlayerFromTeamRequestDto { SeasonID = 1, CaptainID = 2, PersonID = 3 };
        var added = new CaptainAddedResponseDto { TeamName = "Bombers", TeamID = 7 };

        using (Assert.EnterMultipleScope())
        {
            Assert.That((move.SeasonID, move.PreviousCaptainID, move.NewCaptainID, move.PersonID, move.RoundNumber), Is.EqualTo((1, 2, 3, 4, 5)));
            Assert.That(move with { RoundNumber = 5 }, Is.EqualTo(move));
            Assert.That((remove.SeasonID, remove.CaptainID, remove.PersonID), Is.EqualTo((1, 2, 3)));
            Assert.That((added.TeamName, added.TeamID), Is.EqualTo(("Bombers", 7)));
        }
    }
}
