using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// A user's spend on the chat's paid models in a month (<see cref="ChatMonth"/>): begun for the month a day is in, each reply's cost and
/// tokens added (<see cref="ChatMonth.Record"/>), and its total measured against a limit (<see cref="ChatMonth.Reached"/>).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ChatMonthAggregateTests
{
    [Test]
    public void Begin_IsTheMonthTheDayIsIn_WithNothingSpent()
    {
        var month = ChatMonth.Begin(7, new DateOnly(2026, 11, 15));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(month.UserID, Is.EqualTo(7));
            Assert.That(month.Month, Is.EqualTo(new DateOnly(2026, 11, 1)));
            Assert.That((month.CostMicros, month.InputTokens, month.OutputTokens, month.Replies), Is.EqualTo((0L, 0L, 0L, 0)));
            Assert.That(month.Spent, Is.Zero);
        }
    }

    [Test]
    public void MonthOf_IsTheFirstOfTheUtcMonth()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatMonth.MonthOf(new DateTime(2026, 11, 30, 23, 59, 59, DateTimeKind.Utc)), Is.EqualTo(new DateOnly(2026, 11, 1)));
            Assert.That(ChatMonth.MonthOf(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc)), Is.EqualTo(new DateOnly(2026, 12, 1)));
        }
    }

    [Test]
    public void Record_AddsEachReply_AndChangesItsVersion()
    {
        var month = ChatMonth.Begin(7, new DateOnly(2026, 11, 15));
        var version = month.Version;

        month.Record(costMicros: 1_250_000, inputTokens: 9_000, outputTokens: 400);
        month.Record(costMicros: 750_000, inputTokens: 1_000, outputTokens: 100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((month.CostMicros, month.InputTokens, month.OutputTokens, month.Replies), Is.EqualTo((2_000_000L, 10_000L, 500L, 2)));
            Assert.That(month.Spent, Is.EqualTo(2m));
            Assert.That(month.Version, Is.Not.EqualTo(version), "Two replies saved at once can't both add to the same total");
        }
    }

    [Test]
    public void Record_RefusesANegativeCostOrTokens()
    {
        var month = ChatMonth.Begin(7, new DateOnly(2026, 11, 15));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => month.Record(-1, 0, 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => month.Record(0, -1, 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => month.Record(0, 0, -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(month.Replies, Is.Zero, "Refused: unchanged");
        }
    }

    [Test]
    public void Reached_OnceTheSpendIsAtTheLimit()
    {
        var month = ChatMonth.Begin(7, new DateOnly(2026, 11, 15));
        month.Record(9_999_999, 0, 0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(month.Reached(10m), Is.False);
            month.Record(1, 0, 0);
            Assert.That(month.Reached(10m), Is.True, "At the limit: no more");
        }
    }

    [Test]
    public void TakeFrom_AddsAnotherAccountsSameMonth_AsAMergeDoes()
    {
        var target = ChatMonth.Begin(7, new DateOnly(2026, 11, 15));
        target.Record(1_000_000, 100, 10);
        var source = ChatMonth.Begin(8, new DateOnly(2026, 11, 2));
        source.Record(2_500_000, 300, 30);
        source.Record(500_000, 50, 5);
        var version = target.Version;

        target.TakeFrom(source);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((target.UserID, target.CostMicros, target.InputTokens, target.OutputTokens, target.Replies),
                Is.EqualTo((7, 4_000_000L, 450L, 45L, 3)));
            Assert.That(target.Version, Is.Not.EqualTo(version));
            Assert.That(() => target.TakeFrom(ChatMonth.Begin(8, new DateOnly(2026, 12, 1))), Throws.ArgumentException, "Another month");
        }
    }
}
