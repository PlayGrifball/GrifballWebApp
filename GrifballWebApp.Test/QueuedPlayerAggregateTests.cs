using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>
/// A spot in the Late League queue as an aggregate (#347): joined through <see cref="QueuedPlayer.Join(int, DateTime)"/> by
/// the rules of <see cref="QueuedPlayer.CheckJoin"/>, put back at the front through <see cref="QueuedPlayer.PutBackAtFront"/>,
/// and matched out through <see cref="QueuedPlayer.MatchUp"/>.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class QueuedPlayerAggregateTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    [TestCase(false, false, false, QueueJoinResult.NoGamertag)]
    [TestCase(false, true, true, QueueJoinResult.NoGamertag)]
    [TestCase(true, true, false, QueueJoinResult.InMatch)]
    [TestCase(true, true, true, QueueJoinResult.InMatch)]
    [TestCase(true, false, true, QueueJoinResult.AlreadyInQueue)]
    [TestCase(true, false, false, QueueJoinResult.Joined)]
    public void CheckJoin_NeedsAGamertag_NoMatch_AndNoSpotYet(bool hasGamertag, bool inMatch, bool inQueue, QueueJoinResult expected)
        => Assert.That(QueuedPlayer.CheckJoin(hasGamertag, inMatch, inQueue), Is.EqualTo(expected));

    [Test]
    public void Join_IsTheUsersSpot_FromThen()
    {
        var spot = QueuedPlayer.Join(4, Now);

        Assert.Multiple(() =>
        {
            Assert.That(spot.UserID, Is.EqualTo(4));
            Assert.That(spot.JoinedAt, Is.EqualTo(Now));
            Assert.That(spot.IsOf(4), Is.True);
            Assert.That(spot.IsOf(5), Is.False);
            Assert.That(QueuedPlayer.Of(4).Compile()(spot), Is.True);
            Assert.That(QueuedPlayer.Of(5).Compile()(spot), Is.False);
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Join_RefusesNoUser(int userId)
        => Assert.That(() => QueuedPlayer.Join(userId, Now), Throws.InstanceOf<ArgumentOutOfRangeException>());

    [Test]
    public void MatchUp_TakesWholeMatches_LongestQueuedFirst()
    {
        // Joined out of key order: the order is the join time's
        var queue = new[] { 5, 3, 1, 6, 2, 4, 7 }.Select(id => QueuedPlayer.Join(id, Now.AddMinutes(id))).Reverse().ToList();

        var matches = QueuedPlayer.MatchUp(queue, 3);

        Assert.That(matches.Select(m => m.Select(x => x.UserID)), Is.EqualTo(new[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 } }), "7 waits for two more");
    }

    [TestCase(0)]
    [TestCase(-2)]
    [TestCase(5)]
    public void MatchUp_TooFewPlayers_OrNoMatchSize_MatchesNobody(int playersPerMatch)
    {
        var queue = Enumerable.Range(1, 4).Select(id => QueuedPlayer.Join(id, Now)).ToList();

        Assert.That(QueuedPlayer.MatchUp(queue, playersPerMatch), Is.Empty);
    }

    [Test]
    public void PutBackAtFront_EmptyQueue_EndsASecondBeforeTheMatchBegan()
    {
        var back = QueuedPlayer.PutBackAtFront([3, 1, 2], Now, []);

        Assert.Multiple(() =>
        {
            Assert.That(back.Select(x => x.UserID), Is.EqualTo(new[] { 3, 1, 2 }));
            Assert.That(back.Select(x => x.JoinedAt), Is.EqualTo(new[] { Now.AddSeconds(-3), Now.AddSeconds(-2), Now.AddSeconds(-1) }));
        });
    }

    [Test]
    public void PutBackAtFront_GoesAheadOfSomeoneQueuedBeforeTheMatchBegan()
    {
        var earlier = Now.AddMinutes(-10);
        var back = QueuedPlayer.PutBackAtFront([3, 1], Now, [QueuedPlayer.Join(9, earlier), QueuedPlayer.Join(8, Now.AddMinutes(5))]);

        Assert.That(back.Select(x => x.JoinedAt), Is.EqualTo(new[] { earlier.AddSeconds(-2), earlier.AddSeconds(-1) }));
    }

    [Test]
    public void PutBackAtFront_LeavesOutThoseQueuedAgain_AndTheSamePlayerTwice()
    {
        var back = QueuedPlayer.PutBackAtFront([3, 1, 3, 2], Now, [QueuedPlayer.Join(1, Now.AddMinutes(1))]);

        Assert.Multiple(() =>
        {
            Assert.That(back.Select(x => x.UserID), Is.EqualTo(new[] { 3, 2 }));
            Assert.That(back.Select(x => x.JoinedAt), Is.EqualTo(new[] { Now.AddSeconds(-2), Now.AddSeconds(-1) }));
        });
    }

    [Test]
    public void IsPlayingIn_AMatchStillPlayed_NotOnceKicked()
    {
        var match = MatchedMatch.Start([1, 2], [3, 4]);
        match.HomeTeam.Players.Single(x => x.UserID == 2).Kick();

        Assert.Multiple(() =>
        {
            Assert.That(QueuedPlayer.Join(1, Now).IsPlayingIn([match]), Is.True);
            Assert.That(QueuedPlayer.Join(4, Now).IsPlayingIn([match]), Is.True);
            Assert.That(QueuedPlayer.Join(2, Now).IsPlayingIn([match]), Is.False, "Kicked");
            Assert.That(QueuedPlayer.Join(5, Now).IsPlayingIn([match]), Is.False);
            Assert.That(QueuedPlayer.Join(1, Now).IsPlayingIn([]), Is.False);
        });
    }
}

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
        _context.QueuedPlayer.AddRange(QueuedPlayer.Join(users[0].Id, DateTime.UtcNow), QueuedPlayer.Join(users[1].Id, DateTime.UtcNow));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var loaded = await _context.QueuedPlayer.AsNoTracking().ToListAsync();
        foreach (var user in users)
            Assert.That(await _context.QueuedPlayer.AnyAsync(QueuedPlayer.Of(user.Id)), Is.EqualTo(loaded.Any(x => x.IsOf(user.Id))), $"User {user.Id}");

        await using var other = _context.NewContextLike();
        other.QueuedPlayer.Add(QueuedPlayer.Join(users[0].Id, DateTime.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync(), "Queued twice");
    }
}
