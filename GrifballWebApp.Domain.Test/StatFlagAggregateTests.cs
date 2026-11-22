using GrifballWebApp.Domain.Archive;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// A stat flag as an aggregate (#548): found against one game, a Halo Infinite match or a recorded game, never both or
/// neither (<see cref="StatFlag.Found"/>); dismissed and rewritten by <see cref="StatFlag.Dismiss"/> and
/// <see cref="StatFlag.Rewrite"/>.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class StatFlagAggregateTests
{
    private static readonly DateTime FoundAt = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid MatchID = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Test]
    public void Found_OfAMatch_SetsItsFieldsAndIsOpen()
    {
        var flag = StatFlag.Found(StatFlagRule.ImpossibleAccuracy, MatchID, null, 5, "105% accuracy", FoundAt);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flag.Rule, Is.EqualTo(StatFlagRule.ImpossibleAccuracy));
            Assert.That(flag.MatchID, Is.EqualTo(MatchID));
            // Set from the key alone (CheckGame); the navigations are EF's to load, nothing of Found's own
            Assert.That(flag.Match, Is.Null);
            Assert.That(flag.RecordedGameID, Is.Null);
            Assert.That(flag.RecordedGame, Is.Null);
            Assert.That(flag.PlayerUserID, Is.EqualTo(5));
            Assert.That(flag.Reason, Is.EqualTo("105% accuracy"));
            Assert.That(flag.FoundAt, Is.EqualTo(FoundAt));
            Assert.That(flag.IsOpen, Is.True);
            Assert.That(flag.DismissedAt, Is.Null);
        }
    }

    [Test]
    public void Found_OfBothAMatchAndARecordedGame_Refuses()
        => Assert.That(() => StatFlag.Found(StatFlagRule.NegativeStat, MatchID, 9, null, "reason", FoundAt), Throws.ArgumentException);

    [Test]
    public void Found_OfNeitherAMatchNorARecordedGame_Refuses()
        => Assert.That(() => StatFlag.Found(StatFlagRule.NegativeStat, null, null, null, "reason", FoundAt), Throws.ArgumentException);

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Found_WithNoReason_Refuses(string? reason)
        => Assert.That(() => StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, reason!, FoundAt), Throws.InstanceOf<ArgumentException>());

    [Test]
    public void Found_WithATooLongReason_Refuses()
        => Assert.That(() => StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, new string('x', StatFlag.ReasonMaxLength + 1), FoundAt), Throws.ArgumentException);

    [Test]
    public void Dismiss_ClosesIt_WithWhoAndWhen()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, "reason", FoundAt);
        var dismissedAt = FoundAt.AddDays(1);

        flag.Dismiss(3, dismissedAt);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flag.IsOpen, Is.False);
            Assert.That(flag.DismissedAt, Is.EqualTo(dismissedAt));
            Assert.That(flag.DismissedByUserID, Is.EqualTo(3));
        }
    }

    [Test]
    public void Dismiss_ByNobodySignedIn_KeepsNoDismisser()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, "reason", FoundAt);

        flag.Dismiss(null, FoundAt.AddDays(1));

        Assert.That(flag.DismissedByUserID, Is.Null);
    }

    [Test]
    public void Dismiss_AlreadyDismissed_Refuses()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, "reason", FoundAt);
        flag.Dismiss(3, FoundAt.AddDays(1));

        Assert.That(() => flag.Dismiss(3, FoundAt.AddDays(2)), Throws.InvalidOperationException);
    }

    [Test]
    public void Rewrite_WhileOpen_UpdatesReasonAndFoundAt()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, "first reason", FoundAt);
        var rewrittenAt = FoundAt.AddDays(1);

        flag.Rewrite("second reason", rewrittenAt);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flag.Reason, Is.EqualTo("second reason"));
            Assert.That(flag.FoundAt, Is.EqualTo(rewrittenAt));
            Assert.That(flag.IsOpen, Is.True);
        }
    }

    [Test]
    public void Rewrite_WhileDismissed_WithTheSameReason_StaysDismissed()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, "reason", FoundAt);
        flag.Dismiss(3, FoundAt.AddDays(1));

        flag.Rewrite("reason", FoundAt.AddDays(2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flag.IsOpen, Is.False);
            Assert.That(flag.DismissedByUserID, Is.EqualTo(3));
            // The re-run found it again, more recently, but the dismissal (of the same finding) still stands
            Assert.That(flag.FoundAt, Is.EqualTo(FoundAt));
        }
    }

    [Test]
    public void Rewrite_WhileDismissed_WithADifferentReason_Reopens()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, "reason", FoundAt);
        flag.Dismiss(3, FoundAt.AddDays(1));
        var rewrittenAt = FoundAt.AddDays(2);

        flag.Rewrite("a different reason", rewrittenAt);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flag.IsOpen, Is.True);
            Assert.That(flag.DismissedAt, Is.Null);
            Assert.That(flag.DismissedByUserID, Is.Null);
            Assert.That(flag.Reason, Is.EqualTo("a different reason"));
            Assert.That(flag.FoundAt, Is.EqualTo(rewrittenAt));
        }
    }
}
