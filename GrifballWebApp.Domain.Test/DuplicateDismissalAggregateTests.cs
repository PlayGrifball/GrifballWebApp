using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// A dismissed "possible duplicate" pair as an aggregate (#350): made by <see cref="DuplicateDismissal.Dismiss"/>, which
/// stores the pair once, lower user ID first, whichever way round it was dismissed.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class DuplicateDismissalAggregateTests
{
    private static readonly DateTime DismissedAt = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    [TestCase(3, 8)]
    [TestCase(8, 3)]
    public void Dismiss_StoresThePairLowerUserFirst_EitherWayRound(int userId, int otherUserId)
    {
        var dismissal = DuplicateDismissal.Dismiss(userId, otherUserId, DismissedAt, 7);

        Assert.Multiple(() =>
        {
            Assert.That((dismissal.LowUserID, dismissal.HighUserID), Is.EqualTo((3, 8)));
            Assert.That(dismissal.DismissedAt, Is.EqualTo(DismissedAt));
            Assert.That(dismissal.DismissedByUserID, Is.EqualTo(7));
            Assert.That(dismissal.IsOf(3, 8), Is.True);
            Assert.That(dismissal.IsOf(8, 3), Is.True);
            Assert.That(dismissal.IsOf(3, 9), Is.False);
            Assert.That(dismissal.IsOf(3, 3), Is.False);
        });
    }

    [Test]
    public void Dismiss_ByNobodySignedIn_KeepsNoDismisser()
        => Assert.That(DuplicateDismissal.Dismiss(1, 2, DismissedAt, null).DismissedByUserID, Is.Null);

    [Test]
    public void Dismiss_RefusesAUserPairedWithItself()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DuplicateDismissal.CheckPair(4, 4), Is.EqualTo(DuplicatePair.SameUser));
            Assert.That(DuplicateDismissal.CheckPair(4, 5), Is.EqualTo(DuplicatePair.Ok));
            Assert.That(() => DuplicateDismissal.Dismiss(4, 4, DismissedAt, 7), Throws.ArgumentException);
        });
    }

    [TestCase(1, 2, 1, 2)]
    [TestCase(2, 1, 1, 2)]
    [TestCase(5, 5, 5, 5)]
    public void InOrder_PutsTheLowerUserFirst(int userId, int otherUserId, int low, int high)
        => Assert.That(DuplicateDismissal.InOrder(userId, otherUserId), Is.EqualTo((low, high)));
}
