using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QueuedPlayerAggregateDatabaseTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task Of_in_a_query_agrees_with_IsOf_and_a_user_is_queued_once()
    {
        var users = Enumerable.Range(0, 3).Select(i => User.Seed(userName: $"queued{i}")).ToList();
        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();
        _context.QueuedPlayer.AddRange(QueuedPlayer.Seed(users[0].Id, DateTime.UtcNow), QueuedPlayer.Seed(users[1].Id, DateTime.UtcNow));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var loaded = await _context.QueuedPlayer.AsNoTracking().ToListAsync();
        foreach (var user in users)
            Assert.That(await _context.QueuedPlayer.AnyAsync(QueuedPlayer.Of(user.Id)), Is.EqualTo(loaded.Any(x => x.IsOf(user.Id))), $"User {user.Id}");

        await using var other = _context.NewContextLike();
        other.QueuedPlayer.Add(QueuedPlayer.Seed(users[0].Id, DateTime.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync(), "Queued twice");
    }
}
