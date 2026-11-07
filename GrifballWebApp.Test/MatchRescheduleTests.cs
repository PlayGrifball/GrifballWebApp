using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>
/// A reschedule request's rules (<see cref="MatchReschedule"/>): made only for a match that can move and has no pending
/// request, decided once, and an approval only while the match is undecided (#297). Then what the database holds it to.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class MatchRescheduleTests
{
    private static readonly DateTime Scheduled = new(2025, 6, 1, 20, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2025, 5, 30, 12, 0, 0, DateTimeKind.Utc);

    private static SeasonMatch Match(SeasonMatchResult? home = null, SeasonMatchResult? away = null, int? awayTeamID = 2)
        => new() { SeasonMatchID = 7, HomeTeamID = 1, AwayTeamID = awayTeamID, ScheduledTime = Scheduled, HomeTeamResult = home, AwayTeamResult = away };

    private static MatchReschedule Pending(SeasonMatch match, DateTime? newTime = null)
        => MatchReschedule.Request(match, newTime ?? Scheduled.AddDays(2), "busy", 3, Now).Reschedule!;

    [Test]
    public void Request_IsPending_FromTheMatchsTime_AndIsTheMatchsActiveRequest()
    {
        var match = Match();
        var result = MatchReschedule.Request(match, Scheduled.AddDays(2), "busy", 3, Now);

        var r = result.Reschedule!;
        Assert.Multiple(() =>
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
        });
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

    [TestCase(SeasonMatchResult.Won, null, 2, "This match already has a result.")]
    [TestCase(null, SeasonMatchResult.Bye, 2, "This match already has a result.")]
    [TestCase(null, null, null, "This match does not have both teams yet.")]
    public void Request_IsRefused_ForAMatchThatCantMove(SeasonMatchResult? home, SeasonMatchResult? away, int? awayTeamID, string why)
    {
        var match = Match(home, away, awayTeamID);

        Assert.That(MatchReschedule.Request(match, null, "busy", 3, Now), Is.EqualTo(new RescheduleRequestResult(null, why)));
        Assert.That(match.Reschedules, Is.Empty);
    }

    [Test]
    public void Approve_MovesTheMatch_AndRecordsWhoAndWhen()
    {
        var match = Match();
        var r = Pending(match, Scheduled.AddDays(3));

        Assert.That(r.Decide(true, 9, "ok", Now), Is.EqualTo(RescheduleDecision.Approved));
        Assert.Multiple(() =>
        {
            Assert.That(r.Status, Is.EqualTo(RescheduleStatus.Approved));
            Assert.That(r.ApprovedByUserID, Is.EqualTo(9));
            Assert.That(r.ProcessedAt, Is.EqualTo(Now));
            Assert.That(r.CommissionerNotes, Is.EqualTo("ok"));
            Assert.That(match.ScheduledTime, Is.EqualTo(Scheduled.AddDays(3)));
            Assert.That(match.PendingReschedule, Is.Null);
        });
    }

    [Test]
    public void Approve_WithoutANewTime_KeepsTheMatchsTime()
    {
        var match = Match();
        var r = MatchReschedule.Request(match, null, "busy", 3, Now).Reschedule!;

        Assert.That(r.Decide(true, 9, null, Now), Is.EqualTo(RescheduleDecision.Approved));
        Assert.That(match.ScheduledTime, Is.EqualTo(Scheduled));
    }

    [Test]
    public void Reject_KeepsTheMatchsTime()
    {
        var match = Match();
        var r = Pending(match);

        Assert.That(r.Decide(false, 9, "no", Now), Is.EqualTo(RescheduleDecision.Rejected));
        Assert.Multiple(() =>
        {
            Assert.That(r.Status, Is.EqualTo(RescheduleStatus.Rejected));
            Assert.That(r.ApprovedByUserID, Is.EqualTo(9));
            Assert.That(r.ProcessedAt, Is.EqualTo(Now));
            Assert.That(match.ScheduledTime, Is.EqualTo(Scheduled));
        });
    }

    [Test]
    public void Approve_IsRefused_When_TheMatchWasDecidedSinceTheRequest_ButRejectIsNot()
    {
        var match = Match();
        var r = Pending(match);
        match.HomeTeamResult = SeasonMatchResult.Forfeit;
        match.AwayTeamResult = SeasonMatchResult.Won;

        Assert.That(r.Decide(true, 9, null, Now), Is.EqualTo(RescheduleDecision.MatchNotReschedulable));
        Assert.Multiple(() =>
        {
            Assert.That(r.Status, Is.EqualTo(RescheduleStatus.Pending));
            Assert.That(r.ApprovedByUserID, Is.Null);
            Assert.That(match.ScheduledTime, Is.EqualTo(Scheduled));
        });
        Assert.That(r.Decide(false, 9, null, Now), Is.EqualTo(RescheduleDecision.Rejected));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ADecidedRequest_IsNotDecidedAgain(bool approvedFirst)
    {
        var match = Match();
        var r = Pending(match, Scheduled.AddDays(3));
        r.Decide(approvedFirst, 9, "first", Now);

        Assert.That(r.Decide(!approvedFirst, 10, "second", Now.AddHours(1)), Is.EqualTo(RescheduleDecision.AlreadyDecided));
        Assert.Multiple(() =>
        {
            Assert.That(r.ApprovedByUserID, Is.EqualTo(9));
            Assert.That(r.CommissionerNotes, Is.EqualTo("first"));
            Assert.That(r.ProcessedAt, Is.EqualTo(Now));
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(requested.RequestedByUserID, Is.EqualTo(8));
            Assert.That(requested.ApprovedByUserID, Is.Null);
            Assert.That(decided.RequestedByUserID, Is.EqualTo(8));
            Assert.That(decided.ApprovedByUserID, Is.EqualTo(8));
            Assert.That(others.RequestedByUserID, Is.EqualTo(4));
            Assert.That(others.ApprovedByUserID, Is.EqualTo(5));
        });
    }

    // ---------- What the database holds it to ----------

    private static async Task<(GrifballContext Context, int MatchID, int UserID)> SeedAsync()
    {
        var context = await SetUpFixture.NewGrifballContext();
        var s = await RescheduleTestData.SeedAsync(context);
        return (context, s.Match.SeasonMatchID, s.Requester.Id);
    }

    [Test]
    public async Task AMatch_HasOnePendingRequestAtMost_AndAnyNumberDecided()
    {
        var (context, matchID, userID) = await SeedAsync();
        try
        {
            var decided = MatchReschedule.Request(matchID, null, null, "old", userID, Now);
            context.MatchReschedules.AddRange(decided, MatchReschedule.Request(matchID, null, null, "one", userID, Now));
            RescheduleTestData.Decided(context, decided, RescheduleStatus.Rejected, userID);
            await context.SaveChangesAsync();

            context.MatchReschedules.Add(MatchReschedule.Request(matchID, null, null, "two", userID, Now));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }

    [TestCase(RescheduleStatus.Approved, false, true)]
    [TestCase(RescheduleStatus.Rejected, true, false)]
    [TestCase(RescheduleStatus.Pending, true, true)]
    public async Task ADecision_HasItsDeciderAndTime_AndAPendingRequestNeither(RescheduleStatus status, bool decider, bool processedAt)
    {
        var (context, matchID, userID) = await SeedAsync();
        try
        {
            var r = MatchReschedule.Request(matchID, null, null, "busy", userID, Now);
            context.MatchReschedules.Add(r);
            var entry = context.Entry(r);
            entry.Property(x => x.Status).CurrentValue = status;
            entry.Property(x => x.ApprovedByUserID).CurrentValue = decider ? userID : null;
            entry.Property(x => x.ProcessedAt).CurrentValue = processedAt ? Now : null;

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
        finally
        {
            await context.DropDatabaseAndDispose();
        }
    }
}
