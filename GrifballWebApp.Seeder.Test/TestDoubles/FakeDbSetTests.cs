using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Seeder.Test;

/// <summary>The fake set answers EF's async operators the way a real one would, composed queries included.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class FakeDbSetTests
{
    private sealed record Item(int Value);

    private static readonly int[] OneAndTwo = [1, 2];
    private static readonly int[] Four = [4];

    private readonly FakeDbSet<Item> _set = new();

    // As the seeder sees a set: an IQueryable (the fake itself is also an IAsyncEnumerable, for which System.Linq has
    // its own async operators)
    private IQueryable<Item> Query => _set;

    [SetUp]
    public void AddItems() => _set.AddRange(new Item(1), new Item(2), new Item(3));

    [Test]
    public async Task ComposedQueries_RunAsynchronously()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Query.Where(i => i.Value > 1).AnyAsync(), Is.True);
            Assert.That(await Query.Where(i => i.Value > 3).AnyAsync(), Is.False);
            Assert.That(await Query.OrderByDescending(i => i.Value).FirstOrDefaultAsync(), Is.EqualTo(new Item(3)));
            Assert.That(await Query.CountAsync(i => i.Value != 2), Is.EqualTo(2));
            Assert.That(await Query.Where(i => i.Value < 3).Select(i => i.Value).ToListAsync(), Is.EqualTo(OneAndTwo));
            Assert.That(await Query.ToListAsync(), Has.Count.EqualTo(3));
        }
    }

    [Test]
    public void Queries_SeeItemsAddedAfterThem()
    {
        var query = Query.Where(i => i.Value > 3);
        _set.Add(new Item(4));

        Assert.That(query.Select(i => i.Value), Is.EqualTo(Four));
    }

    [Test]
    public void ACancelledToken_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => Query.AnyAsync(cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
            Assert.That(() => Query.ToListAsync(cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
        }
    }
}
