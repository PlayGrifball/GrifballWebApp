using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GrifballWebApp.Integration.Test;

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

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DuplicateDismissalAggregateDatabaseTests
{
    private static readonly DateTime DismissedAt = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task The_database_refuses_a_pair_out_of_order_with_itself_or_twice()
    {
        // Past the factory, as only something outside the class could (raw SQL, a bad migration)
        async Task<bool> Refused(int userId, int otherUserId, Action<EntityEntry<DuplicateDismissal>> change)
        {
            await using var ctx = _context.NewContextLike();
            var entry = ctx.DuplicateDismissals.Add(DuplicateDismissal.Dismiss(userId, otherUserId, DismissedAt, 7));
            change(entry);
            try
            {
                await ctx.SaveChangesAsync();
                return false;
            }
            catch (DbUpdateException)
            {
                return true;
            }
        }

        static void Reverse(EntityEntry<DuplicateDismissal> e)
        {
            var (low, high) = (e.Entity.LowUserID, e.Entity.HighUserID);
            e.Property(d => d.LowUserID).CurrentValue = high;
            e.Property(d => d.HighUserID).CurrentValue = low;
        }

        Assert.Multiple(async () =>
        {
            Assert.That(await Refused(1, 2, Reverse), Is.True, "Higher user first");
            Assert.That(await Refused(3, 4, e => e.Property(d => d.HighUserID).CurrentValue = e.Entity.LowUserID), Is.True, "A user with itself");

            Assert.That(await Refused(5, 6, _ => { }), Is.False, "A pair in order");
            Assert.That(await Refused(6, 5, _ => { }), Is.True, "The same pair again, the other way round");
        });
    }

    [Test]
    public async Task Of_in_a_query_agrees_with_IsOf()
    {
        _context.DuplicateDismissals.AddRange(
            DuplicateDismissal.Dismiss(1, 2, DismissedAt, 7),
            DuplicateDismissal.Dismiss(3, 1, DismissedAt, null),
            DuplicateDismissal.Dismiss(2, 3, DismissedAt, 7));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var loaded = await _context.DuplicateDismissals.AsNoTracking().ToListAsync();

        Assert.Multiple(async () =>
        {
            foreach (var (userId, otherUserId) in new[] { (1, 2), (2, 1), (1, 3), (3, 1), (1, 4), (2, 2) })
            {
                var found = await _context.DuplicateDismissals.Where(DuplicateDismissal.Of(userId, otherUserId))
                    .Select(d => new { d.LowUserID, d.HighUserID }).ToListAsync();
                Assert.That(found.Select(d => (d.LowUserID, d.HighUserID)),
                    Is.EquivalentTo(loaded.Where(d => d.IsOf(userId, otherUserId)).Select(d => (d.LowUserID, d.HighUserID))),
                    $"({userId}, {otherUserId})");
            }
            Assert.That(loaded.Count(d => d.IsOf(1, 3)), Is.EqualTo(1), "Dismissed 3 then 1: found either way round");
        });
    }
}
