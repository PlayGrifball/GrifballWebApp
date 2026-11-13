using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// DatabaseLocks.AcquireLockAsync on SQLite (an in-process semaphore, no server): no database, host, file or network.
/// Each test uses its own lock name, since SQLite's locks are a static dictionary shared by the whole process.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class DatabaseLocksSqliteTests
{
    private static DatabaseFacade SqliteDatabase() =>
        new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, "Data Source=database-locks-test;Mode=Memory").Options).Database;

    private static string LockName() => $"lock-{Guid.NewGuid():N}";

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void AcquireLockAsync_NoName_Throws(string? name)
    {
        var database = SqliteDatabase();

        Assert.That(async () => await database.AcquireLockAsync(name!), Throws.ArgumentException);
    }

    [Test]
    public async Task AcquireLockAsync_Sqlite_CanBeAcquiredAndReleased()
    {
        var database = SqliteDatabase();
        var name = LockName();

        var handle = await database.AcquireLockAsync(name);
        await handle.DisposeAsync();

        // Released: taken again at once, no wait.
        var second = database.AcquireLockAsync(name);
        Assert.That(second.IsCompletedSuccessfully, Is.True);
        await (await second).DisposeAsync();
    }

    [Test]
    public async Task AcquireLockAsync_Sqlite_SecondAcquire_WaitsForTheFirstsRelease()
    {
        var database = SqliteDatabase();
        var name = LockName();
        var handle = await database.AcquireLockAsync(name);

        var secondTask = database.AcquireLockAsync(name);

        // Not granted while the first is held.
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        Assert.That(secondTask.IsCompleted, Is.False);

        await handle.DisposeAsync();
        var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(5));
        await second.DisposeAsync();
    }

    [Test]
    public async Task AcquireLockAsync_Sqlite_DifferentNames_DoNotBlockEachOther()
    {
        var database = SqliteDatabase();

        // Granted together, well within a lock's wait: two different names never contend.
        var handles = await Task.WhenAll(database.AcquireLockAsync(LockName()), database.AcquireLockAsync(LockName()))
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(handles, Has.Length.EqualTo(2));
        await handles[0].DisposeAsync();
        await handles[1].DisposeAsync();
    }

    [Test]
    public async Task AcquireLockAsync_Sqlite_DisposingTwice_ReleasesOnlyOnce()
    {
        var database = SqliteDatabase();
        var name = LockName();
        var handle = await database.AcquireLockAsync(name);
        await handle.DisposeAsync();

        // Not a double release (which SemaphoreSlim.Release would reject once already at its max count): a no-op.
        Assert.That(async () => await handle.DisposeAsync(), Throws.Nothing);

        var reacquired = await database.AcquireLockAsync(name).WaitAsync(TimeSpan.FromSeconds(5));
        await reacquired.DisposeAsync();
    }

    [Test]
    public async Task AcquireLockAsync_Sqlite_Cancelled_StopsWaiting()
    {
        var database = SqliteDatabase();
        var name = LockName();
        var handle = await database.AcquireLockAsync(name);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        try
        {
            Assert.That(async () => await database.AcquireLockAsync(name, cts.Token), Throws.InstanceOf<OperationCanceledException>());
        }
        finally
        {
            await handle.DisposeAsync();
        }
    }

    [Test]
    public void Key_SameName_IsStable()
    {
        var name = LockName();

        Assert.That(DatabaseLocks.Key(name), Is.EqualTo(DatabaseLocks.Key(name)));
    }

    [Test]
    public void Key_DifferentNames_AreDifferent()
    {
        Assert.That(DatabaseLocks.Key("a"), Is.Not.EqualTo(DatabaseLocks.Key("b")));
    }
}
