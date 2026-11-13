using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// QueueMembership (joining and leaving, by user and by Discord account, and what the watchers are told) over the real
/// QueueRepository on the fake IApplicationDbContext, and QueueRepository's own reads (a player's state, the queue with
/// its players' names, the active matches through MatchedMatchQueries.WithPlayerNames). AddPlayerToQueue's race (a spot
/// saved by another request between its check and its insert) is staged with a second context on the same connection,
/// and the membership's answer to it with a substituted repository.
/// </summary>
[TestFixture]
internal sealed class QueueMembershipUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Joined = new(2026, 6, 1, 20, 0, 0, DateTimeKind.Utc);
    private const int GamerID = 1;
    private const int NoGamertagID = 2;
    private const int QueuedID = 3;
    private const int PlayingID = 4;
    private const int OpponentID = 5;
    private const long GamerDiscordID = 111;
    private static readonly int[] OnlyGamer = [GamerID];
    private static readonly int[] OnlyQueued = [QueuedID];

    private IQueueNotifier _notifier = null!;
    private QueueRepository _repository = null!;
    private QueueMembership _membership = null!;

    [SetUp]
    public void SetUp()
    {
        _notifier = Substitute.For<IQueueNotifier>();
        _repository = new QueueRepository(Context);
        _membership = new QueueMembership(_repository, Context, _notifier);
    }

    /// <summary>
    /// A gamer (gamertag, Discord account), a player without a gamertag, one queued already, and two in an active match
    /// (plus a canceled match the gamer was in, which doesn't count).
    /// </summary>
    private async Task Seed()
    {
        Context.DiscordUsers.Add(DiscordUser.FromDiscord(GamerDiscordID, "gamer#1"));
        Context.Users.AddRange(
            User.Seed(userName: "gamer", xboxUser: XboxUser.FromXbox(100, "Gamer"), discordUserID: GamerDiscordID, id: GamerID),
            User.Seed(userName: "nogt", id: NoGamertagID),
            User.Seed(userName: "queued", xboxUser: XboxUser.FromXbox(300, "Queued"), id: QueuedID),
            User.Seed(userName: "playing", xboxUser: XboxUser.FromXbox(400, "Playing"), id: PlayingID),
            User.Seed(userName: "opponent", xboxUser: XboxUser.FromXbox(500, "Opponent"), id: OpponentID));
        await Context.SaveChangesAsync();
        Context.QueuedPlayer.Add(QueuedPlayer.Seed(QueuedID, Joined));
        Context.MatchedMatches.Add(MatchedMatch.Start([PlayingID], [OpponentID], Joined));
        Context.MatchedMatches.Add(MatchedMatch.Start([GamerID], [OpponentID], Joined.AddDays(-1)).Canceled());
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    private Task<List<int>> Queue() => Context.QueuedPlayer.AsNoTracking().Select(q => q.UserID).OrderBy(id => id).ToListAsync();

    [Test]
    public async Task Join_PlayerWithGamertag_JoinsAndTellsTheWatchers()
    {
        await Seed();

        var (result, count) = await _membership.Join(GamerID);

        var queue = await Queue();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(QueueJoinResult.Joined));
            Assert.That(count, Is.EqualTo(2));
            Assert.That(queue, Does.Contain(GamerID));
        }
        await _notifier.Received(1).Send(2);
        await _notifier.Received(1).SendStatusChanged(Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(OnlyGamer)));
    }

    [TestCase(NoGamertagID, QueueJoinResult.NoGamertag)]
    [TestCase(999, QueueJoinResult.NoGamertag)]
    [TestCase(QueuedID, QueueJoinResult.AlreadyInQueue)]
    [TestCase(PlayingID, QueueJoinResult.InMatch)]
    public async Task Join_Refused_SaysWhyAndTellsNoOne(int userID, QueueJoinResult expected)
    {
        await Seed();

        var (result, count) = await _membership.Join(userID);

        var queue = await Queue();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(count, Is.Zero);
            Assert.That(queue, Is.EqualTo(OnlyQueued));
        }
        await _notifier.DidNotReceive().Send(Arg.Any<int>());
    }

    [Test]
    public async Task JoinByDiscord_LinkedAccount_JoinsAsItsUser()
    {
        await Seed();

        var (result, count) = await _membership.JoinByDiscord(GamerDiscordID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(QueueJoinResult.Joined));
            Assert.That(count, Is.EqualTo(2));
            Assert.That(await Queue(), Does.Contain(GamerID));
        }
    }

    [Test]
    public async Task JoinByDiscord_UnknownAccount_NoGamertag()
    {
        await Seed();

        var (result, count) = await _membership.JoinByDiscord(999);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(QueueJoinResult.NoGamertag));
            Assert.That(count, Is.Zero);
        }
    }

    [Test]
    public async Task Join_AddedByAnotherRequestSinceTheCheck_AlreadyInQueue()
    {
        var queue = Substitute.For<IQueueRepository>();
        queue.PlayerState(GamerID, Arg.Any<CancellationToken>()).Returns(new QueuePlayerState(GamerID, HasGamertag: true, InQueue: false, InMatch: false, QueueCount: 0));
        queue.AddPlayerToQueue(Arg.Any<QueuedPlayer>(), Arg.Any<CancellationToken>()).Returns(false);
        var membership = new QueueMembership(queue, Context, _notifier);

        var (result, count) = await membership.Join(GamerID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(QueueJoinResult.AlreadyInQueue));
            Assert.That(count, Is.Zero);
        }
        await queue.Received(1).AddPlayerToQueue(Arg.Is<QueuedPlayer>(q => q.UserID == GamerID), Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().Send(Arg.Any<int>());
    }

    [Test]
    public async Task Leave_Queued_LeavesAndTellsTheWatchers()
    {
        await Seed();

        var (result, count) = await _membership.Leave(QueuedID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(QueueLeaveResult.Left));
            Assert.That(count, Is.Zero);
            Assert.That(await Queue(), Is.Empty);
        }
        await _notifier.Received(1).Send(0);
        await _notifier.Received(1).SendStatusChanged(Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(OnlyQueued)));
    }

    [Test]
    public async Task Leave_NotQueued_NotInQueue()
    {
        await Seed();

        var (result, count) = await _membership.Leave(GamerID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(QueueLeaveResult.NotInQueue));
            Assert.That(count, Is.Zero);
        }
        await _notifier.DidNotReceive().Send(Arg.Any<int>());
    }

    [Test]
    public async Task LeaveByDiscord_LinkedAccountQueued_Leaves()
    {
        await Seed();
        await _membership.Join(GamerID);
        _notifier.ClearReceivedCalls();

        var (result, count) = await _membership.LeaveByDiscord(GamerDiscordID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(QueueLeaveResult.Left));
            Assert.That(count, Is.EqualTo(1));
            Assert.That(await Queue(), Is.EqualTo(OnlyQueued));
        }
    }

    [Test]
    public async Task LeaveByDiscord_UnknownAccount_NotInQueue()
    {
        await Seed();

        var (result, count) = await _membership.LeaveByDiscord(999);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(QueueLeaveResult.NotInQueue));
            Assert.That(count, Is.Zero);
        }
    }

    [Test]
    public async Task PlayerState_ReadsGamertagQueueMatchAndTheQueuesSize()
    {
        await Seed();

        var gamer = await _repository.PlayerState(GamerID);
        var playing = await _repository.PlayerState(PlayingID);
        var queued = await _repository.PlayerStateByDiscord(GamerDiscordID);
        var nobody = await _repository.PlayerState(999);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gamer, Is.EqualTo(new QueuePlayerState(GamerID, HasGamertag: true, InQueue: false, InMatch: false, QueueCount: 1)));
            Assert.That(playing, Is.EqualTo(new QueuePlayerState(PlayingID, HasGamertag: true, InQueue: false, InMatch: true, QueueCount: 1)));
            Assert.That(queued!.UserId, Is.EqualTo(GamerID));
            Assert.That(nobody, Is.Null);
        }
    }

    [Test]
    public async Task IsInMatch_OnlyAnActiveMatchCounts()
    {
        await Seed();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _repository.IsInMatch(PlayingID), Is.True);
            Assert.That(await _repository.IsInMatch(GamerID), Is.False);
            Assert.That(await _repository.IsInMatch(QueuedID), Is.False);
        }
    }

    [Test]
    public async Task GetQueuePlayer_AndCount_ReadTheQueue()
    {
        await Seed();

        var queued = await _repository.GetQueuePlayer(QueuedID);
        var notQueued = await _repository.GetQueuePlayer(GamerID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(queued!.JoinedAt, Is.EqualTo(Joined));
            Assert.That(notQueued, Is.Null);
            Assert.That(await _repository.Count(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task GetQueuePlayersWithInfo_LoadsTheirGamertags()
    {
        await Seed();

        var players = await _repository.GetQueuePlayersWithInfo(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(players.Single().UserID, Is.EqualTo(QueuedID));
            Assert.That(players.Single().User.XboxUser!.Gamertag, Is.EqualTo("Queued"));
        }
    }

    [Test]
    public async Task GetActiveMatches_OnlyActiveOnes_WithTheirPlayersNamesAndVotes()
    {
        await Seed();

        var matches = await _repository.GetActiveMatches(CancellationToken.None);

        var match = matches.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(match.Status, Is.EqualTo(MatchedMatchStatus.Active));
            Assert.That(match.HomeTeam.Players.Single().User.XboxUser!.Gamertag, Is.EqualTo("Playing"));
            Assert.That(match.AwayTeam.Players.Single().User.XboxUser!.Gamertag, Is.EqualTo("Opponent"));
            Assert.That(match.MatchedWinnerVotes, Is.Empty);
            Assert.That(match.MatchedKickVotes, Is.Empty);
        }
    }

    [Test]
    public async Task AddPlayerToQueue_AlreadyQueued_False()
    {
        await Seed();

        var added = await _repository.AddPlayerToQueue(QueuedPlayer.Seed(QueuedID, Joined.AddMinutes(5)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(added, Is.False);
            Assert.That(await Queue(), Is.EqualTo(OnlyQueued));
        }
    }

    [Test]
    public async Task AddPlayerToQueue_QueuedByAnotherRequestMeanwhile_False_AndKeepsThatSpot()
    {
        await Seed();
        var connection = Context.Database.GetDbConnection();
        var plain = new DbContextOptionsBuilder<FakeApplicationDbContext>().UseSqlite(connection).Options;
        // The site's join is saved after the Discord button's checked the queue, as the button's saves
        var site = new BeforeFirstSave(async ct =>
        {
            await using var other = new FakeApplicationDbContext(plain);
            other.QueuedPlayer.Add(QueuedPlayer.Seed(GamerID, Joined));
            await other.SaveChangesAsync(ct);
        });
        await using var racing = new FakeApplicationDbContext(new DbContextOptionsBuilder<FakeApplicationDbContext>().UseSqlite(connection).AddInterceptors(site).Options);

        var added = await new QueueRepository(racing).AddPlayerToQueue(QueuedPlayer.Seed(GamerID, Joined.AddMinutes(1)));

        var spot = await Context.QueuedPlayer.AsNoTracking().SingleAsync(q => q.UserID == GamerID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(added, Is.False, "they are in the queue: the other request put them there");
            Assert.That(spot.JoinedAt, Is.EqualTo(Joined), "the other request's spot stays");
        }
    }

    [Test]
    public async Task AddPlayerToQueue_SaveFailsForAnotherReason_Throws()
    {
        await Seed();

        // No such user: the insert breaks the foreign key, not the queue's key
        await Assert.ThrowsAsync<DbUpdateException>(() => _repository.AddPlayerToQueue(QueuedPlayer.Seed(999, Joined)));

        Assert.That(await Queue(), Is.EqualTo(OnlyQueued));
    }

    [Test]
    public async Task RemovePlayerToQueue_NotQueued_False()
    {
        await Seed();

        Assert.That(await _repository.RemovePlayerToQueue(GamerID), Is.False);
    }

    [TestCase(QueueJoinResult.NoGamertag, QueueMessages.NoGamertag)]
    [TestCase(QueueJoinResult.AlreadyInQueue, "You are already in the Late League queue!")]
    [TestCase(QueueJoinResult.InMatch, "You are already in a Late League match!")]
    [TestCase(QueueJoinResult.Joined, "You have joined the Late League queue! 🎉")]
    public void QueueMessages_ForEachJoinResult(QueueJoinResult result, string expected)
    {
        Assert.That(QueueMessages.For(result), Is.EqualTo(expected));
    }

    [TestCase(QueueLeaveResult.NotInQueue, "You are not in the Late League queue.")]
    [TestCase(QueueLeaveResult.Left, "You have left the Late League queue")]
    public void QueueMessages_ForEachLeaveResult(QueueLeaveResult result, string expected)
    {
        Assert.That(QueueMessages.For(result), Is.EqualTo(expected));
    }
}
