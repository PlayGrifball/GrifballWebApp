using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

[TestFixture]
internal sealed class RescheduleServiceUnitTests : ApplicationDbContextTests
{
    private const int SeasonID = 1;
    private const int HomeTeamID = 10;
    private const int AwayTeamID = 20;
    private const int HomeCaptainUserID = 1;
    private const int AwayCaptainUserID = 2;
    private const int RequesterUserID = 3;
    private const int CommissionerUserID = 4;

    private IOutbox _outbox = null!;
    private RescheduleService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _outbox = Substitute.For<IOutbox>();
        _service = new RescheduleService(Context, _outbox);
    }

    private async Task<SeasonMatch> SeedUndecidedMatch(bool requesterOnRoster = true)
    {
        Context.Seasons.Add(Season.Seed(seasonID: SeasonID));
        Context.Users.Add(User.Seed(userName: "homecap", displayName: "HomeCap", id: HomeCaptainUserID));
        Context.Users.Add(User.Seed(userName: "awaycap", displayName: "AwayCap", id: AwayCaptainUserID));
        Context.Users.Add(User.Seed(userName: "requester", displayName: "Requester", id: RequesterUserID));
        var home = Team.Seed(teamName: "Home", seasonID: SeasonID, teamID: HomeTeamID);
        var away = Team.Seed(teamName: "Away", seasonID: SeasonID, teamID: AwayTeamID);
        Context.Teams.AddRange(home, away);
        var match = SeasonMatch.Between(SeasonID, HomeTeamID, AwayTeamID, bestOf: 3);
        Context.SeasonMatches.Add(match);
        await Context.SaveChangesAsync();

        Context.Add(TeamPlayer.Seed(teamID: HomeTeamID, userID: HomeCaptainUserID));
        Context.Add(TeamPlayer.Seed(teamID: AwayTeamID, userID: AwayCaptainUserID));
        if (requesterOnRoster)
            Context.Add(TeamPlayer.Seed(teamID: HomeTeamID, userID: RequesterUserID));
        await Context.SaveChangesAsync();

        home.SetCaptain(HomeCaptainUserID);
        away.SetCaptain(AwayCaptainUserID);
        await Context.SaveChangesAsync();

        return match;
    }

    [Test]
    public async Task RequestRescheduleAsync_MatchNotFound_Throws()
    {
        var dto = new RescheduleRequestDto { SeasonMatchID = 999, Reason = "conflict" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.RequestRescheduleAsync(dto, RequesterUserID));
        Assert.That(ex!.Message, Is.EqualTo("Season match not found"));
    }

    [Test]
    public async Task RequestRescheduleAsync_NotOnRosterNorCommissioner_ThrowsForbidden()
    {
        var match = await SeedUndecidedMatch(requesterOnRoster: false);
        var dto = new RescheduleRequestDto { SeasonMatchID = match.SeasonMatchID, Reason = "conflict" };

        Assert.That(async () => await _service.RequestRescheduleAsync(dto, RequesterUserID),
            Throws.TypeOf<RescheduleForbiddenException>());
    }

    [Test]
    public async Task RequestRescheduleAsync_NoBothTeams_ThrowsNotAllowed()
    {
        Context.Seasons.Add(Season.Seed(seasonID: SeasonID));
        var match = SeasonMatch.InBracket(SeasonID, Domain.League.MatchBracketInfo.Seed(1), bestOf: 3);
        Context.SeasonMatches.Add(match);
        await Context.SaveChangesAsync();
        var dto = new RescheduleRequestDto { SeasonMatchID = match.SeasonMatchID, Reason = "conflict" };

        Assert.That(async () => await _service.RequestRescheduleAsync(dto, RequesterUserID),
            Throws.TypeOf<RescheduleNotAllowedException>());
    }

    [Test]
    public async Task RequestRescheduleAsync_RosterPlayer_CreatesPendingRequest()
    {
        var match = await SeedUndecidedMatch();
        var newTime = new DateTime(2026, 6, 1, 18, 0, 0, DateTimeKind.Utc);
        var dto = new RescheduleRequestDto { SeasonMatchID = match.SeasonMatchID, NewScheduledTime = newTime, Reason = "conflict" };

        var reschedule = await _service.RequestRescheduleAsync(dto, RequesterUserID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reschedule.Status, Is.EqualTo(RescheduleStatus.Pending));
            Assert.That(reschedule.SeasonMatchID, Is.EqualTo(match.SeasonMatchID));
            Assert.That(reschedule.NewScheduledTime, Is.EqualTo(newTime));
        }
        var saved = await Context.MatchReschedules.ToListAsync();
        Assert.That(saved, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RequestRescheduleAsync_AlreadyPending_ThrowsAlreadyActive()
    {
        var match = await SeedUndecidedMatch();
        var dto = new RescheduleRequestDto { SeasonMatchID = match.SeasonMatchID, Reason = "first" };
        await _service.RequestRescheduleAsync(dto, RequesterUserID);

        var dto2 = new RescheduleRequestDto { SeasonMatchID = match.SeasonMatchID, Reason = "second" };
        Assert.That(async () => await _service.RequestRescheduleAsync(dto2, RequesterUserID),
            Throws.TypeOf<RescheduleAlreadyActiveException>());
    }

    [Test]
    public async Task ProcessRescheduleAsync_NotFound_Throws()
    {
        var dto = new ProcessRescheduleDto { Approved = true };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.ProcessRescheduleAsync(999, dto, CommissionerUserID));
        Assert.That(ex!.Message, Is.EqualTo("Reschedule request not found"));
    }

    [Test]
    public async Task ProcessRescheduleAsync_Approved_MovesMatchAndSendsDecision()
    {
        var match = await SeedUndecidedMatch();
        var newTime = new DateTime(2026, 6, 1, 18, 0, 0, DateTimeKind.Utc);
        var requested = await _service.RequestRescheduleAsync(
            new RescheduleRequestDto { SeasonMatchID = match.SeasonMatchID, NewScheduledTime = newTime, Reason = "conflict" }, RequesterUserID);

        var decided = await _service.ProcessRescheduleAsync(requested.MatchRescheduleID,
            new ProcessRescheduleDto { Approved = true, CommissionerNotes = "ok" }, CommissionerUserID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(decided.Status, Is.EqualTo(RescheduleStatus.Approved));
            Assert.That(decided.CommissionerNotes, Is.EqualTo("ok"));
        }
        var reloadedMatch = await Context.SeasonMatches.SingleAsync(m => m.SeasonMatchID == match.SeasonMatchID);
        Assert.That(reloadedMatch.ScheduledTime, Is.EqualTo(newTime));
    }

    [Test]
    public async Task ProcessRescheduleAsync_AlreadyDecided_Throws()
    {
        var match = await SeedUndecidedMatch();
        var requested = await _service.RequestRescheduleAsync(
            new RescheduleRequestDto { SeasonMatchID = match.SeasonMatchID, Reason = "conflict" }, RequesterUserID);
        await _service.ProcessRescheduleAsync(requested.MatchRescheduleID, new ProcessRescheduleDto { Approved = false }, CommissionerUserID);

        Assert.That(async () => await _service.ProcessRescheduleAsync(requested.MatchRescheduleID, new ProcessRescheduleDto { Approved = true }, CommissionerUserID),
            Throws.TypeOf<RescheduleAlreadyProcessedException>());
    }

    [Test]
    public async Task GetPendingReschedulesAsync_ReturnsPendingWithCaptains()
    {
        var match = await SeedUndecidedMatch();
        await _service.RequestRescheduleAsync(new RescheduleRequestDto { SeasonMatchID = match.SeasonMatchID, Reason = "conflict" }, RequesterUserID);

        var pending = await _service.GetPendingReschedulesAsync();

        Assert.That(pending, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(pending[0].HomeCaptain, Is.EqualTo("HomeCap"));
            Assert.That(pending[0].AwayCaptain, Is.EqualTo("AwayCap"));
            Assert.That(pending[0].RequestedByGamertag, Is.EqualTo("Requester"));
        }
    }

    [Test]
    public async Task GetPendingReschedulesAsync_NoneAreOpen_ReturnsEmpty()
    {
        var pending = await _service.GetPendingReschedulesAsync();

        Assert.That(pending, Is.Empty);
    }

    [Test]
    public async Task GetOverdueMatchesAsync_PastCutoff_ReturnsMatch()
    {
        await SeedUndecidedMatch();
        var match = await Context.SeasonMatches.SingleAsync();
        match.ScheduleAt(DateTime.UtcNow.AddHours(-48));
        await Context.SaveChangesAsync();

        var overdue = await _service.GetOverdueMatchesAsync();

        Assert.That(overdue, Has.Count.EqualTo(1));
        Assert.That(overdue[0].HoursOverdue, Is.GreaterThanOrEqualTo(47));
    }

    [Test]
    public async Task GetOverdueMatchesAsync_NotPastCutoff_ReturnsEmpty()
    {
        await SeedUndecidedMatch();
        var match = await Context.SeasonMatches.SingleAsync();
        match.ScheduleAt(DateTime.UtcNow.AddHours(-1));
        await Context.SaveChangesAsync();

        var overdue = await _service.GetOverdueMatchesAsync();

        Assert.That(overdue, Is.Empty);
    }

    [Test]
    public async Task CreateDiscordThreadAsync_NoExistingThread_Enqueues()
    {
        var match = await SeedUndecidedMatch();
        var requested = await _service.RequestRescheduleAsync(
            new RescheduleRequestDto { SeasonMatchID = match.SeasonMatchID, Reason = "conflict" }, RequesterUserID);

        await _service.CreateDiscordThreadAsync(requested.MatchRescheduleID);

        await _outbox.Received(3).EnqueueAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateDiscordThreadAsync_NotFound_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateDiscordThreadAsync(999));
        Assert.That(ex!.Message, Is.EqualTo("Reschedule request not found"));
    }
}
