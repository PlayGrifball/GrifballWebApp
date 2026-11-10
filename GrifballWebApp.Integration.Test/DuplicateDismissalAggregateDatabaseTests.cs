using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GrifballWebApp.Integration.Test;

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
