using GrifballWebApp.Domain.League;
using static GrifballWebApp.Domain.Test.MatchRescheduleTestData;

namespace GrifballWebApp.Domain.Test;

/// <summary>MatchRescheduleTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class MatchRescheduleUnitTests
{
    [Test]
    public void Request_IsPending_FromTheMatchsTime_AndIsTheMatchsActiveRequest()
    {
        var match = Match();
        var result = MatchReschedule.Request(match, Scheduled.AddDays(2), "busy", 3, Now);

        var r = result.Reschedule!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Refused, Is.Null);
            Assert.That(r.Status, Is.EqualTo(RescheduleStatus.Pending));
            Assert.That(r.OriginalScheduledTime, Is.EqualTo(Scheduled));
            Assert.That(r.NewScheduledTime, Is.EqualTo(Scheduled.AddDays(2)));
            Assert.That(r.Reason, Is.EqualTo("busy"));
            Assert.That(r.RequestedByUserID, Is.EqualTo(3));
            Assert.That(r.RequestedAt, Is.EqualTo(Now));
            Assert.That(r.SeasonMatch, Is.SameAs(match));
            Assert.That(match.Reschedules, Is.EqualTo(new[] { r }));
            Assert.That(match.PendingReschedule, Is.SameAs(r));
        }
    }

    [Test]
    public void Request_IsRefused_While_AnotherIsPending_ButNotAfterItWasDecided()
    {
        var match = Match();
        var first = Pending(match);

        var second = MatchReschedule.Request(match, null, "again", 3, Now);
        Assert.That(second, Is.EqualTo(new RescheduleRequestResult(null, MatchReschedule.AlreadyActive)));

        first.Decide(false, 9, null, Now);
        Assert.That(MatchReschedule.Request(match, null, "again", 3, Now).Reschedule, Is.Not.Null);
    }

    [TestCase(SeasonMatchDecision.Games, 1, 2, "This match already has a result.")]
    [TestCase(SeasonMatchDecision.Forfeit, 1, 2, "This match already has a result.")]
    [TestCase(null, 1, null, "This match does not have both teams yet.")]
    [TestCase(null, null, 2, "This match does not have both teams yet.")]
    public void Request_IsRefused_ForAMatchThatCantMove(SeasonMatchDecision? decidedBy, int? homeTeamID, int? awayTeamID, string why)
    {
        var match = Match(decidedBy, awayTeamID, homeTeamID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(MatchReschedule.Request(match, null, "busy", 3, Now), Is.EqualTo(new RescheduleRequestResult(null, why)));
            Assert.That(match.Reschedules, Is.Empty);
        }
    }

    [Test]
    public void Approve_MovesTheMatch_AndRecordsWhoAndWhen()
    {
        var match = Match();
        var r = Pending(match, Scheduled.AddDays(3));

        Assert.That(r.Decide(true, 9, "ok", Now), Is.EqualTo(RescheduleDecision.Approved));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.Status, Is.EqualTo(RescheduleStatus.Approved));
            Assert.That(r.ApprovedByUserID, Is.EqualTo(9));
            Assert.That(r.ProcessedAt, Is.EqualTo(Now));
            Assert.That(r.CommissionerNotes, Is.EqualTo("ok"));
            Assert.That(match.ScheduledTime, Is.EqualTo(Scheduled.AddDays(3)));
            Assert.That(match.PendingReschedule, Is.Null);
        }
    }

    [Test]
    public void Approve_WithoutANewTime_KeepsTheMatchsTime()
    {
        var match = Match();
        var r = MatchReschedule.Request(match, null, "busy", 3, Now).Reschedule!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.Decide(true, 9, null, Now), Is.EqualTo(RescheduleDecision.Approved));
            Assert.That(match.ScheduledTime, Is.EqualTo(Scheduled));
        }
    }

    [Test]
    public void Reject_KeepsTheMatchsTime()
    {
        var match = Match();
        var r = Pending(match);

        Assert.That(r.Decide(false, 9, "no", Now), Is.EqualTo(RescheduleDecision.Rejected));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.Status, Is.EqualTo(RescheduleStatus.Rejected));
            Assert.That(r.ApprovedByUserID, Is.EqualTo(9));
            Assert.That(r.ProcessedAt, Is.EqualTo(Now));
            Assert.That(match.ScheduledTime, Is.EqualTo(Scheduled));
        }
    }

    // #297: a decided match keeps its time, so its decision closes its pending request. A decision racing the request's
    // approval (the match decided past its requests) is refused by Decide: RescheduleServiceWorkflowTests covers that.
    [TestCase(SeasonMatchDecision.Games)]
    [TestCase(SeasonMatchDecision.Forfeit)]
    public void DecidingTheMatch_ClosesItsPendingRequest_AsRejectedByItsRequester(SeasonMatchDecision decidedBy)
    {
        var match = Match();
        var r = Pending(match);

        _ = decidedBy is SeasonMatchDecision.Games ? match.WonBy(SeasonMatchSide.Away) : match.Forfeited(Forfeiter.Home);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.Status, Is.EqualTo(RescheduleStatus.Rejected));
            Assert.That(r.ApprovedByUserID, Is.EqualTo(3), "closed by its requester");
            Assert.That(r.ProcessedAt, Is.Not.Null);
            Assert.That(r.CommissionerNotes, Is.EqualTo(MatchReschedule.ClosedBecauseDecided));
            Assert.That(r.AwaitsDecision, Is.False);
            Assert.That(match.PendingReschedule, Is.Null);
            Assert.That(match.ScheduledTime, Is.EqualTo(Scheduled));
        }
        Assert.That(r.Decide(true, 9, null, Now), Is.EqualTo(RescheduleDecision.AlreadyDecided), "nothing left to approve");
    }

    [Test]
    public void ABye_ClosesThePendingRequestOfTheMatchItDecides()
    {
        // A request of a match that has since lost a team (its result taken back in the bracket); its slot gets a BYE
        var match = SeasonMatch.Seed(1, 1, null, scheduledTime: Scheduled).WithId(7);
        var r = MatchReschedule.Request(7, Scheduled, null, "busy", 3, Now);
        match.AddReschedule(r);

        match.WithBye(SeasonMatchSide.Away);

        Assert.That((r.Status, r.ApprovedByUserID, match.PendingReschedule), Is.EqualTo((RescheduleStatus.Rejected, (int?)3, (MatchReschedule?)null)));
    }

    [Test]
    public void ADecidedRequest_IsLeftAsItWas_And_AnUndecidedSeriesKeepsItsRequestPending()
    {
        var match = SeasonMatch.Seed(1, 1, 2, bestOf: 3, scheduledTime: Scheduled).WithId(7);
        var rejected = Pending(match);
        rejected.Decide(false, 9, "no", Now);
        var pending = Pending(match);

        match.Scored(1, 0);
        Assert.That(pending.AwaitsDecision, Is.True, "1-0 in a best of 3 decides nothing");

        match.WonBy(SeasonMatchSide.Home);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((rejected.Status, rejected.ApprovedByUserID, rejected.CommissionerNotes), Is.EqualTo((RescheduleStatus.Rejected, (int?)9, "no")));
            Assert.That((pending.Status, pending.ApprovedByUserID), Is.EqualTo((RescheduleStatus.Rejected, (int?)3)));
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AwaitsDecision_WhilePending_AndNotOnceDecided(bool approved)
    {
        var match = Match();
        var r = Pending(match);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.AwaitsDecision, Is.True);
            Assert.That(match.PendingReschedule, Is.SameAs(r));
        }

        r.Decide(approved, 9, null, Now);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.AwaitsDecision, Is.False);
            Assert.That(match.PendingReschedule, Is.Null);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ADecidedRequest_IsNotDecidedAgain(bool approvedFirst)
    {
        var match = Match();
        var r = Pending(match, Scheduled.AddDays(3));
        r.Decide(approvedFirst, 9, "first", Now);

        Assert.That(r.Decide(!approvedFirst, 10, "second", Now.AddHours(1)), Is.EqualTo(RescheduleDecision.AlreadyDecided));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(r.ApprovedByUserID, Is.EqualTo(9));
            Assert.That(r.CommissionerNotes, Is.EqualTo("first"));
            Assert.That(r.ProcessedAt, Is.EqualTo(Now));
        }
    }

    [Test]
    public void AttachThread_KeepsItsThread_AndRefusesAnother()
    {
        var r = Pending(Match());
        r.AttachThread(44);
        r.AttachThread(44);

        Assert.That(r.DiscordThreadID, Is.EqualTo(44UL));
        Assert.Throws<InvalidOperationException>(() => r.AttachThread(45));
    }

    [Test]
    public void ReassignUser_MovesTheRequestAndTheDecision_OnlyFromThatUser()
    {
        var requested = Pending(Match());
        var decided = Pending(Match());
        decided.Decide(false, 3, null, Now);
        var others = MatchReschedule.Request(Match(), null, "busy", 4, Now).Reschedule!;
        others.Decide(false, 5, null, Now);

        foreach (var r in new[] { requested, decided, others })
            r.ReassignUser(3, 8);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(requested.RequestedByUserID, Is.EqualTo(8));
            Assert.That(requested.ApprovedByUserID, Is.Null);
            Assert.That(decided.RequestedByUserID, Is.EqualTo(8));
            Assert.That(decided.ApprovedByUserID, Is.EqualTo(8));
            Assert.That(others.RequestedByUserID, Is.EqualTo(4));
            Assert.That(others.ApprovedByUserID, Is.EqualTo(5));
        }
    }
}

/// <summary>MatchRescheduleTests' helpers, shared with its integration tests.</summary>
internal static class MatchRescheduleTestData
{
    internal static readonly DateTime Scheduled = new(2025, 6, 1, 20, 0, 0, DateTimeKind.Utc);
    internal static readonly DateTime Now = new(2025, 5, 30, 12, 0, 0, DateTimeKind.Utc);

    internal static SeasonMatch Match(SeasonMatchDecision? decidedBy = null, int? awayTeamID = 2, int? homeTeamID = 1)
    {
        var match = SeasonMatch.Seed(1, homeTeamID, awayTeamID, scheduledTime: Scheduled).WithId(7);
        return decidedBy switch
        {
            null => match,
            SeasonMatchDecision.Games => match.WonBy(SeasonMatchSide.Home),
            SeasonMatchDecision.Forfeit => match.Forfeited(Forfeiter.Away),
            _ => throw new ArgumentOutOfRangeException(nameof(decidedBy)),
        };
    }

    internal static MatchReschedule Pending(SeasonMatch match, DateTime? newTime = null)
        => MatchReschedule.Request(match, newTime ?? Scheduled.AddDays(2), "busy", 3, Now).Reschedule!;
}
