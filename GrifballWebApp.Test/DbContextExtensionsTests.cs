using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DbContextExtensionsTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task DiscardPendingChanges_UndoesAddsEditsAndRemovals_AndKeepsTheRestTracked()
    {
        var edited = await _context.AddUser("edited");
        var removed = await _context.AddUser("removed");
        var untouched = await _context.AddUser("untouched");
        edited.MMR = 5;
        _context.Users.Remove(removed);
        var added = new User { UserName = "added" };
        _context.Users.Add(added);

        _context.DiscardPendingChanges();
        await _context.SaveChangesAsync();

        await using var fresh = _context.NewContextLike();
        Assert.Multiple(async () =>
        {
            Assert.That(_context.ChangeTracker.HasChanges(), Is.False);
            Assert.That(edited.MMR, Is.EqualTo(1000), "The edit is undone in the entity too");
            Assert.That(_context.Entry(added).State, Is.EqualTo(EntityState.Detached));
            Assert.That(_context.Entry(untouched).State, Is.EqualTo(EntityState.Unchanged));
            Assert.That(await fresh.Users.Select(x => x.UserName).ToListAsync(), Is.EquivalentTo(new[] { "edited", "removed", "untouched" }));
        });
    }
}
