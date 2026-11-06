using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NUnit.Framework;

namespace GrifballWebApp.Test;

/// <summary>GrifballContext's transaction layers, on every database the app supports (the suite runs once per provider).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GrifballContextTransactionTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private static User NewUser(string name) => new() { UserName = name, NormalizedUserName = name.ToUpperInvariant(), DisplayName = name };

    private Task<List<string?>> UserNames() => _context.Users.AsNoTracking().OrderBy(u => u.UserName).Select(u => u.UserName).ToListAsync();

    [Test]
    public async Task AfterAnInnerRollback_TheOuterLayerSavesNothing_AndStartsNothing_UntilItRollsBack()
    {
        await _context.StartTransactionAsync();
        _context.Users.Add(NewUser("in-the-transaction"));
        await _context.SaveChangesAsync();
        await _context.StartTransactionAsync();
        await _context.RollbackTransactionAsync();
        _context.ChangeTracker.Clear();

        // No database transaction is left: a save would commit on its own, outside what the caller thinks it is in
        _context.Users.Add(NewUser("after-the-inner-rollback"));
        Assert.Multiple(async () =>
        {
            Assert.That(_context.InTransaction, Is.True);
            Assert.That(_context.RolledBackInside, Is.True);
            Assert.That(await Assert.ThrowsAsync<InvalidOperationException>(() => _context.SaveChangesAsync()), Has.Message.Contains("inner transaction has rolled back"));
            Assert.Throws<InvalidOperationException>(() => _context.SaveChanges());
            await Assert.ThrowsAsync<InvalidOperationException>(() => _context.StartTransactionAsync());
            await Assert.ThrowsAsync<Exception>(() => _context.CommitTransactionAsync());
        });

        await _context.RollbackTransactionAsync();
        _context.ChangeTracker.Clear();
        Assert.That(_context.InTransaction, Is.False);
        Assert.That(_context.RolledBackInside, Is.False);
        Assert.That(await UserNames(), Is.Empty, "Nothing saved after the inner rollback, nor before it");

        // The context starts afresh
        _context.Users.Add(NewUser("afterwards"));
        await _context.SaveChangesAsync();
        Assert.That(await UserNames(), Is.EqualTo(new[] { "afterwards" }));
    }

    [Test]
    public async Task ANewLayerRefusedAfterAnInnerRollback_LeavesTheLayersAsTheyWere()
    {
        await _context.StartTransactionAsync();
        await _context.StartTransactionAsync();
        await _context.RollbackTransactionAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _context.StartTransactionAsync());

        // One layer is open still: one rollback closes it
        await _context.RollbackTransactionAsync();
        Assert.That(_context.InTransaction, Is.False);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _context.RollbackTransactionAsync());
    }

    [Test]
    public async Task ATransactionTheServerEnded_IsLetGoOf_WithoutAnErrorOfItsOwn()
    {
        await _context.StartTransactionAsync();
        _context.Users.Add(NewUser("rolled-back-by-the-server"));
        await _context.SaveChangesAsync();
        // What a deadlock victim's transaction is: rolled back by the server, with no connection left
        _context.Database.CurrentTransaction!.GetDbTransaction().Rollback();

        await _context.RollbackTransactionAsync();

        _context.ChangeTracker.Clear();
        Assert.That(_context.InTransaction, Is.False);
        Assert.That(await UserNames(), Is.Empty);
        await _context.StartTransactionAsync();
        _context.Users.Add(NewUser("committed"));
        await _context.SaveChangesAsync();
        await _context.CommitTransactionAsync();
        Assert.That(await UserNames(), Is.EqualTo(new[] { "committed" }));
    }

    [Test]
    public async Task OutsideATransaction_SavesAreUnaffected()
    {
        _context.Users.Add(NewUser("plain"));
        Assert.That(_context.SaveChanges(), Is.EqualTo(1));
        Assert.That(_context.RolledBackInside, Is.False);
        Assert.That(await UserNames(), Is.EqualTo(new[] { "plain" }));
    }
}
