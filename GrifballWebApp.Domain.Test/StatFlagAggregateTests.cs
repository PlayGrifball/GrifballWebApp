using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// A stat flag as an aggregate (#548): found against exactly one subject - a Halo Infinite match, a recorded game, or
/// a season match's series as a whole (<see cref="StatFlag.Found"/>); dismissed and rewritten by
/// <see cref="StatFlag.Dismiss"/> and <see cref="StatFlag.Rewrite"/>.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class StatFlagAggregateTests
{
    private static readonly DateTime FoundAt = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid MatchID = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static IReadOnlyList<IDomainEvent> Events(StatFlag flag) => ((IHasDomainEvents)flag).DomainEvents;
    private static void ClearEvents(StatFlag flag) => ((IHasDomainEvents)flag).ClearDomainEvents();

    [Test]
    public void Found_OfAMatch_SetsItsFieldsAndIsOpen()
    {
        var flag = StatFlag.Found(StatFlagRule.ImpossibleAccuracy, MatchID, null, null, 5, "105% accuracy", FoundAt);

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
        => Assert.That(() => StatFlag.Found(StatFlagRule.NegativeStat, MatchID, 9, null, null, "reason", FoundAt), Throws.ArgumentException);

    [Test]
    public void Found_OfAMatchAndASeasonMatch_Refuses()
        => Assert.That(() => StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, 9, null, "reason", FoundAt), Throws.ArgumentException);

    [Test]
    public void Found_OfARecordedGameAndASeasonMatch_Refuses()
        => Assert.That(() => StatFlag.Found(StatFlagRule.NegativeStat, null, 9, 9, null, "reason", FoundAt), Throws.ArgumentException);

    [Test]
    public void Found_OfNeitherAMatchNorARecordedGameNorASeasonMatch_Refuses()
        => Assert.That(() => StatFlag.Found(StatFlagRule.NegativeStat, null, null, null, null, "reason", FoundAt), Throws.ArgumentException);

    [Test]
    public void Found_OfASeasonMatch_SetsItsFieldsAndIsOpen()
    {
        var flag = StatFlag.Found(StatFlagRule.SeriesDoesNotMatchGames, null, null, 12, null, "the series shows 2-1, but its games add up to 1-1", FoundAt);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flag.SeasonMatchID, Is.EqualTo(12));
            Assert.That(flag.MatchID, Is.Null);
            Assert.That(flag.RecordedGameID, Is.Null);
            Assert.That(flag.SeasonMatch, Is.Null); // Set from the key alone (CheckGame); EF's to load
        }
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Found_WithNoReason_Refuses(string? reason)
        => Assert.That(() => StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, reason!, FoundAt), Throws.InstanceOf<ArgumentException>());

    [Test]
    public void Found_WithATooLongReason_Refuses()
        => Assert.That(() => StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, new string('x', StatFlag.ReasonMaxLength + 1), FoundAt), Throws.ArgumentException);

    [Test]
    public void Dismiss_ClosesIt_WithWhoAndWhen()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "reason", FoundAt);
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
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "reason", FoundAt);

        flag.Dismiss(null, FoundAt.AddDays(1));

        Assert.That(flag.DismissedByUserID, Is.Null);
    }

    [Test]
    public void Dismiss_AlreadyDismissed_IsANoOp_KeepingTheFirstDismissal()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "reason", FoundAt);
        var firstDismissedAt = FoundAt.AddDays(1);
        flag.Dismiss(3, firstDismissedAt);

        Assert.That(() => flag.Dismiss(7, FoundAt.AddDays(2)), Throws.Nothing);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flag.DismissedAt, Is.EqualTo(firstDismissedAt));
            Assert.That(flag.DismissedByUserID, Is.EqualTo(3));
        }
    }

    [Test]
    public void Dismiss_RaisesStatFlagDismissed()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "reason", FoundAt);
        ClearEvents(flag);

        flag.Dismiss(3, FoundAt.AddDays(1));

        Assert.That(Events(flag), Is.EqualTo(new IDomainEvent[] { new StatFlagDismissed(flag) }));
    }

    [Test]
    public void Dismiss_AlreadyDismissed_RaisesNothing()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "reason", FoundAt);
        flag.Dismiss(3, FoundAt.AddDays(1));
        ClearEvents(flag);

        flag.Dismiss(7, FoundAt.AddDays(2));

        Assert.That(Events(flag), Is.Empty);
    }

    [Test]
    public void Clear_RaisesStatFlagCleared()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "reason", FoundAt);
        ClearEvents(flag);

        flag.Clear();

        Assert.That(Events(flag), Is.EqualTo(new IDomainEvent[] { new StatFlagCleared(flag) }));
    }

    [Test]
    public void CatchUpPlayer_SetsThePlayer_WithoutReopeningOrChangingFoundAt()
    {
        // #548, third review: an account linking (or merging) must change who a Halo finding is attributed to
        // without acting like the finding itself changed
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "reason", FoundAt, discriminator: 555);
        flag.Dismiss(3, FoundAt.AddDays(1));
        ClearEvents(flag);

        flag.CatchUpPlayer(42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(flag.PlayerUserID, Is.EqualTo(42));
            Assert.That(flag.IsOpen, Is.False, "Still dismissed: only who it's attributed to changed, not the finding");
            Assert.That(flag.FoundAt, Is.EqualTo(FoundAt));
            Assert.That(Events(flag), Is.Empty);
        }
    }

    [Test]
    public void Found_WithADiscriminator_KeepsIt()
    {
        var flag = StatFlag.Found(StatFlagRule.TeamSmallerThanFormat, MatchID, null, null, null, "Side 1 fielded 1 player(s)", FoundAt, discriminator: 1);

        Assert.That(flag.Discriminator, Is.EqualTo(1));
    }

    [Test]
    public void Found_WithNoDiscriminator_IsNull()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, 5, "reason", FoundAt);

        Assert.That(flag.Discriminator, Is.Null);
    }

    [Test]
    public void Rewrite_WhileOpen_UpdatesReasonAndFoundAt()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "first reason", FoundAt);
        var rewrittenAt = FoundAt.AddDays(1);

        var changed = flag.Rewrite("second reason", rewrittenAt);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.True);
            Assert.That(flag.Reason, Is.EqualTo("second reason"));
            Assert.That(flag.FoundAt, Is.EqualTo(rewrittenAt));
            Assert.That(flag.IsOpen, Is.True);
        }
    }

    [Test]
    public void Rewrite_WhileOpen_WithTheSameReason_KeepsItsFoundAt_AndReturnsFalse()
    {
        // #548, third review: the same finding re-confirmed by a later re-run must not jump to the top of the
        // newest-first list, or look freshly found when it's not
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "reason", FoundAt);

        var changed = flag.Rewrite("reason", FoundAt.AddDays(1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.False);
            Assert.That(flag.Reason, Is.EqualTo("reason"));
            Assert.That(flag.FoundAt, Is.EqualTo(FoundAt));
        }
    }

    [Test]
    public void Rewrite_WhileOpen_WithTheSameReason_RaisesNothing()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "reason", FoundAt);
        ClearEvents(flag);

        flag.Rewrite("reason", FoundAt.AddDays(1));

        Assert.That(Events(flag), Is.Empty);
    }

    [Test]
    public void Rewrite_WhileDismissed_WithADifferentReason_ReturnsTrue()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "reason", FoundAt);
        flag.Dismiss(3, FoundAt.AddDays(1));

        var changed = flag.Rewrite("a different reason", FoundAt.AddDays(2));

        Assert.That(changed, Is.True);
    }

    [Test]
    public void Rewrite_WhileDismissed_WithTheSameReason_ReturnsFalse()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "reason", FoundAt);
        flag.Dismiss(3, FoundAt.AddDays(1));

        var changed = flag.Rewrite("reason", FoundAt.AddDays(2));

        Assert.That(changed, Is.False);
    }

    [Test]
    public void Rewrite_WhileDismissed_WithTheSameReason_StaysDismissed()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "reason", FoundAt);
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
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "reason", FoundAt);
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

    [Test]
    public void Rewrite_WhileDismissed_WithADifferentReason_RaisesStatFlagReopened()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "reason", FoundAt);
        flag.Dismiss(3, FoundAt.AddDays(1));
        ClearEvents(flag);

        flag.Rewrite("a different reason", FoundAt.AddDays(2));

        Assert.That(Events(flag), Is.EqualTo(new IDomainEvent[] { new StatFlagReopened(flag) }));
    }

    [Test]
    public void Rewrite_WhileOpen_RaisesNothing()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "first reason", FoundAt);
        ClearEvents(flag);

        flag.Rewrite("second reason", FoundAt.AddDays(1));

        Assert.That(Events(flag), Is.Empty);
    }

    [Test]
    public void Rewrite_WhileDismissed_WithTheSameReason_RaisesNothing()
    {
        var flag = StatFlag.Found(StatFlagRule.NegativeStat, MatchID, null, null, null, "reason", FoundAt);
        flag.Dismiss(3, FoundAt.AddDays(1));
        ClearEvents(flag);

        flag.Rewrite("reason", FoundAt.AddDays(2));

        Assert.That(Events(flag), Is.Empty);
    }
}
