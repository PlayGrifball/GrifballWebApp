using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>DatabaseLocks: a named lock held across every context (process) on the database, on each provider.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DatabaseLocksTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task A_lock_held_on_one_context_makes_another_wait_until_it_is_released()
    {
        await using var other = _context.NewContextLike();
        var held = await _context.Database.AcquireLockAsync("test-lock");

        var waiting = other.Database.AcquireLockAsync("test-lock");
        await Task.Delay(500);
        Assert.That(waiting.IsCompleted, Is.False, "Held by the first context");

        await held.DisposeAsync();
        var second = await waiting.WaitAsync(TimeSpan.FromSeconds(30));
        await second.DisposeAsync();
        await held.DisposeAsync(); // Released once, however often disposed

        // Released: taken again at once, and the context still works
        await using (await _context.Database.AcquireLockAsync("test-lock").WaitAsync(TimeSpan.FromSeconds(30)))
            Assert.That(await _context.Users.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task Locks_of_other_names_dont_wait_for_each_other()
    {
        await using var other = _context.NewContextLike();
        await using var first = await _context.Database.AcquireLockAsync("test-lock-a");
        await using var second = await other.Database.AcquireLockAsync("test-lock-b").WaitAsync(TimeSpan.FromSeconds(30));
        Assert.That(second, Is.Not.Null);
    }

    [Test]
    public async Task A_cancelled_wait_stops_waiting_and_leaves_the_lock_to_its_holder()
    {
        await using var other = _context.NewContextLike();
        var held = await _context.Database.AcquireLockAsync("test-lock");
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.CatchAsync<OperationCanceledException>(async () => await other.Database.AcquireLockAsync("test-lock", cancel.Token));

        await held.DisposeAsync();
        await using (await other.Database.AcquireLockAsync("test-lock").WaitAsync(TimeSpan.FromSeconds(30)))
            Assert.That(await other.Users.CountAsync(), Is.Zero, "The cancelled wait left the context usable");
    }

    [Test]
    public async Task A_lock_taken_inside_a_transaction_outlives_it()
    {
        await using var other = _context.NewContextLike();
        if (TestDatabase.Provider != DatabaseProvider.Sqlite)
        {
            // SQLite's transactions take the file's write lock: a second context's would wait for the first anyway
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var held = await _context.Database.AcquireLockAsync("test-lock");
            await transaction.RollbackAsync();
            var waiting = other.Database.AcquireLockAsync("test-lock");
            await Task.Delay(300);
            Assert.That(waiting.IsCompleted, Is.False, "Owned by the session, not the transaction");
            await held.DisposeAsync();
            await (await waiting.WaitAsync(TimeSpan.FromSeconds(30))).DisposeAsync();
        }
        else
        {
            await (await other.Database.AcquireLockAsync("test-lock")).DisposeAsync();
        }
    }

    [Test]
    public async Task A_name_is_required_and_its_number_is_the_same_everywhere()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () => await _context.Database.AcquireLockAsync(" "));
        Assert.That(DatabaseLocks.Key("matchmaking-queue"), Is.EqualTo(DatabaseLocks.Key("matchmaking-queue")));
        Assert.That(DatabaseLocks.Key("matchmaking-queue"), Is.Not.EqualTo(DatabaseLocks.Key("other")));
    }
}
