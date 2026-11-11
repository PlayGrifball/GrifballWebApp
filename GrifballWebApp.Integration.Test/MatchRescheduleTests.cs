using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using static GrifballWebApp.Domain.Test.MatchRescheduleTestData;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// A reschedule request's rules (<see cref="MatchReschedule"/>): made only for a match that can move and has no pending
/// request, decided once, and an approval only while the match is undecided (#297). Then what the database holds it to.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class MatchRescheduleTests
{
    // ---------- What the database holds it to ----------

    private static async Task<(GrifballContext Context, int MatchID, int UserID)> SeedAsync()
    {
        var context = await TestDatabase.NewGrifballContext();
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
