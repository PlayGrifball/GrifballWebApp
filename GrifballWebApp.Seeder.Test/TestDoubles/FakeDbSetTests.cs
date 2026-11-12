using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Seeder.Test;

/// <summary>The fake set answers EF's async operators the way a real one would, composed queries included.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class FakeDbSetTests
{
    private sealed record Item(int Value);

    private readonly FakeDbSet<Item> _set = new();

    [SetUp]
    public void AddItems() => _set.AddRange(new Item(1), new Item(2), new Item(3));

    [Test]
    public async Task ComposedQueries_RunAsynchronously()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _set.Where(i => i.Value > 1).AnyAsync(), Is.True);
            Assert.That(await _set.Where(i => i.Value > 3).AnyAsync(), Is.False);
            Assert.That(await _set.OrderByDescending(i => i.Value).FirstOrDefaultAsync(), Is.EqualTo(new Item(3)));
            Assert.That(await _set.CountAsync(i => i.Value != 2), Is.EqualTo(2));
            Assert.That(await _set.Where(i => i.Value < 3).Select(i => i.Value).ToListAsync(), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(await _set.ToListAsync(), Has.Count.EqualTo(3));
        }
    }

    [Test]
    public void Queries_SeeItemsAddedAfterThem()
    {
        var query = _set.Where(i => i.Value > 3);
        _set.Add(new Item(4));

        Assert.That(query.Select(i => i.Value), Is.EqualTo(new[] { 4 }));
    }

    [Test]
    public void ACancelledToken_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => _set.AnyAsync(cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
            Assert.That(() => _set.ToListAsync(cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
        }
    }
}
