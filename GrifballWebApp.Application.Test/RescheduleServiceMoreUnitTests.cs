using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// RescheduleService beyond RescheduleServiceUnitTests: the decision told in the request's thread (and how the approver
/// is named), approving a request whose match has a result since, a thread not made twice; and RescheduleRules.CanRequestAsync.
/// RequestRescheduleAsync's DbUpdateException path (two requests saved at once, the unique pending index refusing the
/// second) isn't here: it needs a concurrent save between the service's read and its write, which this single-context
/// fake can't stage; Integration.Test's concurrency tests cover it.
/// </summary>
[TestFixture]
internal sealed class RescheduleServiceMoreUnitTests : ApplicationDbContextTests
{
    private const int SeasonID = 1;
    private const int HomeTeamID = 10;
    private const int AwayTeamID = 20;
    private const int HomeCaptainUserID = 1;
    private const int AwayCaptainUserID = 2;
    private const int OutsiderUserID = 3;
    private const int CommissionerUserID = 4;
    private const int DiscordLinkedUserID = 5;
    private const int UnnamedUserID = 6;
    private const int CommissionerRoleID = 2; // RoleConfiguration's HasData
    private const long DiscordID = 555;
    private const ulong ThreadID = 9001;

    private IOutbox _outbox = null!;
    private RescheduleService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _outbox = Substitute.For<IOutbox>();
        _service = new RescheduleService(Context, _outbox);
    }

    private async Task<SeasonMatch> SeedMatch(int? awayTeamID = AwayTeamID)
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: SeasonID));
        Context.DiscordUsers.Add(DiscordUser.FromDiscord(DiscordID, "linked"));
        Context.Users.AddRange(
            User.Seed(userName: "homecap", displayName: "HomeCap", id: HomeCaptainUserID),
            User.Seed(userName: "awaycap", displayName: "AwayCap", id: AwayCaptainUserID),
            User.Seed(userName: "outsider", displayName: "Outsider", id: OutsiderUserID),
            User.Seed(userName: "commissioner", displayName: "Commissioner", id: CommissionerUserID),
            User.Seed(userName: "linked", displayName: "Linked", discordUserID: DiscordID, id: DiscordLinkedUserID),
            User.Seed(userName: "unnamed", id: UnnamedUserID));
        Context.Teams.AddRange(Team.Seed(teamName: "Home", seasonID: SeasonID, teamID: HomeTeamID),
            Team.Seed(teamName: "Away", seasonID: SeasonID, teamID: AwayTeamID));
        var match = SeasonMatch.Seed(SeasonID, HomeTeamID, awayTeamID, bestOf: 3);
        Context.SeasonMatches.Add(match);
        await Context.SaveChangesAsync();
        Context.AddRange(TeamPlayer.Seed(teamID: HomeTeamID, userID: HomeCaptainUserID), TeamPlayer.Seed(teamID: AwayTeamID, userID: AwayCaptainUserID));
        Context.UserRoles.Add(new UserRole { UserId = CommissionerUserID, RoleId = CommissionerRoleID });
        await Context.SaveChangesAsync();
        return match;
    }

    private Task<MatchReschedule> Request(SeasonMatch match) => _service.RequestRescheduleAsync(
        new RescheduleRequestDto { SeasonMatchID = match.SeasonMatchID, Reason = "conflict" }, HomeCaptainUserID);

    /// <summary>A pending request of the match, its Discord thread made.</summary>
    private async Task<int> RequestWithThread(SeasonMatch match)
    {
        var requested = await Request(match);
        requested.AttachThread(ThreadID);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return requested.MatchRescheduleID;
    }

    private Task AssertDecisionSent(int rescheduleID, string content) => _outbox.Received(1).EnqueueAsync(RescheduleOutbox.DecisionPost,
        Arg.Is<RescheduleDecisionPayload>(p => p.RescheduleId == rescheduleID && p.Content == content), RescheduleOutbox.Group(rescheduleID), Arg.Any<CancellationToken>());

    [Test]
    public async Task ProcessRescheduleAsync_WithThread_ApproverWithDiscord_IsMentioned()
    {
        var match = await SeedMatch();
        var id = await RequestWithThread(match);

        var decided = await _service.ProcessRescheduleAsync(id, new ProcessRescheduleDto { Approved = true }, DiscordLinkedUserID);

        Assert.That(decided.Status, Is.EqualTo(RescheduleStatus.Approved));
        await AssertDecisionSent(id, $"Your request has been Approved by <@{DiscordID}>");
    }

    [Test]
    public async Task ProcessRescheduleAsync_WithThread_ApproverWithoutDiscord_IsNamed()
    {
        var match = await SeedMatch();
        var id = await RequestWithThread(match);

        await _service.ProcessRescheduleAsync(id, new ProcessRescheduleDto { Approved = false }, CommissionerUserID);

        await AssertDecisionSent(id, "Your request has been Rejected by Commissioner");
    }

    [Test]
    public async Task ProcessRescheduleAsync_WithThread_ApproverWithNoName_IsACommissioner()
    {
        var match = await SeedMatch();
        var id = await RequestWithThread(match);

        await _service.ProcessRescheduleAsync(id, new ProcessRescheduleDto { Approved = false }, UnnamedUserID);

        await AssertDecisionSent(id, "Your request has been Rejected by a commissioner");
    }

    [Test]
    public async Task ProcessRescheduleAsync_WithThread_NoSuchApprover_IsACommissioner_AndTheDecisionIsRolledBack()
    {
        var match = await SeedMatch();
        var id = await RequestWithThread(match);

        // The decision names its approver (a foreign key to Users): an unknown one can't be saved
        await Assert.ThrowsAsync<DbUpdateException>(() => _service.ProcessRescheduleAsync(id, new ProcessRescheduleDto { Approved = false }, 999));

        await AssertDecisionSent(id, "Your request has been Rejected by a commissioner");
        Context.ChangeTracker.Clear();
        var request = await Context.MatchReschedules.AsNoTracking().SingleAsync(r => r.MatchRescheduleID == id);
        Assert.That(request.Status, Is.EqualTo(RescheduleStatus.Pending));
    }

    [Test]
    public async Task ProcessRescheduleAsync_NoThread_TellsNoOne()
    {
        var match = await SeedMatch();
        var requested = await Request(match);

        await _service.ProcessRescheduleAsync(requested.MatchRescheduleID, new ProcessRescheduleDto { Approved = false }, CommissionerUserID);

        await _outbox.DidNotReceive().EnqueueAsync(RescheduleOutbox.DecisionPost, Arg.Any<RescheduleDecisionPayload>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A pending request whose match got a result since (decided with the request not loaded, so it stayed pending).</summary>
    private async Task<int> RequestThenDecideTheMatch(SeasonMatch match)
    {
        var requested = await Request(match);
        Context.ChangeTracker.Clear();
        var loaded = await Context.SeasonMatches.SingleAsync(m => m.SeasonMatchID == match.SeasonMatchID);
        loaded.WonBy(SeasonMatchSide.Home);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return requested.MatchRescheduleID;
    }

    [Test]
    public async Task ProcessRescheduleAsync_ApproveAfterTheMatchHasAResult_Throws()
    {
        var match = await SeedMatch();
        var id = await RequestThenDecideTheMatch(match);

        var ex = await Assert.ThrowsAsync<RescheduleNotAllowedException>(
            () => _service.ProcessRescheduleAsync(id, new ProcessRescheduleDto { Approved = true }, CommissionerUserID));

        Context.ChangeTracker.Clear();
        var request = await Context.MatchReschedules.AsNoTracking().SingleAsync(r => r.MatchRescheduleID == id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo("This match already has a result."));
            Assert.That(request.Status, Is.EqualTo(RescheduleStatus.Pending));
        }
    }

    [Test]
    public async Task GetPendingReschedulesAsync_MatchDecidedSince_SaysItCanOnlyBeRejected()
    {
        var match = await SeedMatch();
        await RequestThenDecideTheMatch(match);

        var pending = (await _service.GetPendingReschedulesAsync()).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pending.WhyNotApprovable, Is.EqualTo("This match already has a result."));
            Assert.That(pending.CanApprove, Is.False);
        }
    }

    [Test]
    public async Task CreateDiscordThreadAsync_ThreadAlreadyMade_QueuesNothing()
    {
        var match = await SeedMatch();
        var id = await RequestWithThread(match);

        await _service.CreateDiscordThreadAsync(id);

        await _outbox.DidNotReceive().EnqueueAsync(Arg.Any<string>(), Arg.Any<RescheduleRequestPostPayload>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _outbox.DidNotReceive().EnqueueAsync(Arg.Any<string>(), Arg.Any<RescheduleThreadPayload>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CanRequestAsync_NoUser_False()
    {
        var match = await SeedMatch();

        Assert.That(await RescheduleRules.CanRequestAsync(Context, match, null), Is.False);
    }

    [Test]
    public async Task CanRequestAsync_MatchWithoutBothTeams_False()
    {
        var match = await SeedMatch(awayTeamID: null);

        Assert.That(await RescheduleRules.CanRequestAsync(Context, match, HomeCaptainUserID), Is.False);
    }

    [Test]
    public async Task CanRequestAsync_PendingRequestAlready_False()
    {
        var match = await SeedMatch();
        await Request(match);

        Assert.That(await RescheduleRules.CanRequestAsync(Context, match, AwayCaptainUserID), Is.False);
    }

    [TestCase(HomeCaptainUserID, true)]
    [TestCase(AwayCaptainUserID, true)]
    [TestCase(CommissionerUserID, true)]
    [TestCase(OutsiderUserID, false)]
    public async Task CanRequestAsync_OpenMatch_RosterOrCommissionerOnly(int userID, bool expected)
    {
        var match = await SeedMatch();

        Assert.That(await RescheduleRules.CanRequestAsync(Context, match, userID), Is.EqualTo(expected));
    }
}
