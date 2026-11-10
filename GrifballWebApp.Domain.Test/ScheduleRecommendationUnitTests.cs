using GrifballWebApp.Domain.League;
using static GrifballWebApp.Domain.Test.ScheduleRecommendationTestData;

namespace GrifballWebApp.Domain.Test;

/// <summary>ScheduleRecommendationTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ScheduleRecommendationUnitTests
{
    private static readonly int[] ExpectedMatches = [1, 2, 3];
    private static readonly bool[] ExpectedConflicts = [false, true, true];


    [Test]
    public void Propose_MakesItWithItsMatches_NotApplied()
    {
        var recommendation = Proposed();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((recommendation.SeasonID, recommendation.RequestedBy, recommendation.Fitness, recommendation.Generations), Is.EqualTo((1, "commish", 0.5, 12)));
            Assert.That((recommendation.IsApplied, recommendation.AppliedAt, recommendation.AppliedBy), Is.EqualTo((false, (DateTime?)null, (string?)null)));
            Assert.That(recommendation.Matches.Select(m => m.SeasonMatchID), Is.EqualTo(ExpectedMatches));
            Assert.That(recommendation.Matches.All(m => m.ScheduleRecommendation == recommendation), Is.True);
            Assert.That(recommendation.Matches.Select(m => m.HasConflict), Is.EqualTo(ExpectedConflicts));
            Assert.That(recommendation.ConflictCount, Is.EqualTo(2));
            Assert.That(recommendation.ProposedTimes(), Is.EqualTo(new Dictionary<int, DateTime> { [1] = At, [2] = At.AddHours(1), [3] = At.AddDays(1) }));
        }
    }

    [Test]
    public void Propose_RefusesWhatTheJobCantProduce()
    {
        var match = new ProposedMatchTime(1, At, false, true, true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => ScheduleRecommendation.Propose(1, null, -0.1, 1, []), Throws.InstanceOf<ArgumentOutOfRangeException>(), "Fitness below 0");
            Assert.That(() => ScheduleRecommendation.Propose(1, null, 1.1, 1, []), Throws.InstanceOf<ArgumentOutOfRangeException>(), "Fitness above 1");
            Assert.That(() => ScheduleRecommendation.Propose(1, null, double.NaN, 1, []), Throws.InstanceOf<ArgumentOutOfRangeException>(), "No fitness");
            Assert.That(() => ScheduleRecommendation.Propose(1, null, 1, -1, []), Throws.InstanceOf<ArgumentOutOfRangeException>(), "Negative generations");
            Assert.That(() => ScheduleRecommendation.Propose(1, null, 1, 1, [match, match with { ScheduledTime = At.AddHours(2) }]),
                Throws.ArgumentException.With.Message.Contains("proposed twice"), "One time per match");
            Assert.That(ScheduleRecommendation.Propose(1, null, 0, 0, []).Matches, Is.Empty, "A season with no matches yet");
        }
    }

    [Test]
    public void Apply_StampsIt_Once()
    {
        var recommendation = Proposed();

        var first = recommendation.Apply("commish", At);
        var again = recommendation.Apply("other", At.AddMinutes(1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((first, again), Is.EqualTo((ScheduleRecommendationApply.Applied, ScheduleRecommendationApply.AlreadyApplied)));
            Assert.That((recommendation.IsApplied, recommendation.AppliedAt, recommendation.AppliedBy), Is.EqualTo((true, (DateTime?)At, "commish")), "Kept the first");
            Assert.That(Proposed().Apply(null, At), Is.EqualTo(ScheduleRecommendationApply.Applied), "Nobody signed in");
        }
    }
}

/// <summary>ScheduleRecommendationTests' helpers, shared with its integration tests.</summary>
internal static class ScheduleRecommendationTestData
{
    internal static readonly DateTime At = new(2026, 3, 1, 20, 0, 0, DateTimeKind.Utc);

    internal static ScheduleRecommendation Proposed(int seasonID = 1) => ScheduleRecommendation.Propose(seasonID, "commish", 0.5, 12,
    [
        new ProposedMatchTime(1, At, Overlaps: false, HomeTeamAvailable: true, AwayTeamAvailable: true),
        new ProposedMatchTime(2, At.AddHours(1), Overlaps: true, HomeTeamAvailable: true, AwayTeamAvailable: true),
        new ProposedMatchTime(3, At.AddDays(1), Overlaps: false, HomeTeamAvailable: true, AwayTeamAvailable: false),
    ]);
}
