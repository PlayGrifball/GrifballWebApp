using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using GruntUser = Surprenant.Grunt.Models.User;
using User = GrifballWebApp.Database.Models.User;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QueueRepositoryTests
{
    private GrifballContext _context;
    private IQueueRepository _repo;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _repo = new QueueRepository(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private async Task<User> AddUser(string name)
    {
        var user = User.Seed(userName: name);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    [Test]
    public async Task AddGetRemove_RoundTrip()
    {
        var user = await AddUser("a");

        var before = await _repo.GetQueuePlayer(user.Id);
        var added = await _repo.AddPlayerToQueue(QueuedPlayer.Seed(user.Id, DateTime.UtcNow));
        var addedAgain = await _repo.AddPlayerToQueue(QueuedPlayer.Seed(user.Id, DateTime.UtcNow));
        var during = await _repo.GetQueuePlayer(user.Id);
        var removed = await _repo.RemovePlayerToQueue(user.Id);
        var removedAgain = await _repo.RemovePlayerToQueue(user.Id);
        var after = await _repo.GetQueuePlayer(user.Id);

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.Null);
            Assert.That(added, Is.True);
            Assert.That(addedAgain, Is.False, "Adding twice is rejected");
            Assert.That(during!.UserID, Is.EqualTo(user.Id));
            Assert.That(during.JoinedAt, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(1)));
            Assert.That(removed, Is.True);
            Assert.That(removedAgain, Is.False);
            Assert.That(after, Is.Null);
        });
    }

    [Test]
    public async Task IsInMatch_TrueOnlyForNonKickedPlayersOfActiveMatches()
    {
        var home = await AddUser("home");
        var away = await AddUser("away");
        var kicked = await AddUser("kicked");
        var finished = await AddUser("finished");
        var idle = await AddUser("idle");
        _context.MatchedMatches.Add(MatchedMatch.Start(
            new MatchedTeam([new MatchedPlayer(home.Id), new MatchedPlayer(kicked.Id).AlreadyKicked()]),
            new MatchedTeam([new MatchedPlayer(away.Id)])));
        _context.MatchedMatches.Add(MatchedMatch.Start(
            new MatchedTeam([new MatchedPlayer(finished.Id)]),
            new MatchedTeam([])).Canceled());
        await _context.SaveChangesAsync();

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await _repo.IsInMatch(home.Id), Is.True);
            Assert.That(await _repo.IsInMatch(away.Id), Is.True, "Away team players count too");
            Assert.That(await _repo.IsInMatch(kicked.Id), Is.False);
            Assert.That(await _repo.IsInMatch(finished.Id), Is.False);
            Assert.That(await _repo.IsInMatch(idle.Id), Is.False);
        });
        var active = await _repo.GetActiveMatches(CancellationToken.None);
        Assert.That(active.Single().HomeTeam.Players, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task AddPlayerToQueue_AddedMeanwhileByAnotherRequest_IsAlreadyInTheQueue()
    {
        // The Discord button and the site at once: the other insert lands between this one's check and its own
        var user = await AddUser("racer");
        var raced = false;
        _context.SavingChanges += (_, _) =>
        {
            if (raced)
                return;
            raced = true;
            using var other = _context.NewContextLike();
            other.QueuedPlayer.Add(QueuedPlayer.Seed(user.Id, DateTime.UtcNow));
            other.SaveChanges();
        };

        var added = await _repo.AddPlayerToQueue(QueuedPlayer.Seed(user.Id, DateTime.UtcNow));

        Assert.That(added, Is.False);
        Assert.That(_context.ChangeTracker.Entries<QueuedPlayer>().Any(x => x.State == EntityState.Added), Is.False, "The failed insert is not tried again by a later save");
        await using var fresh = _context.NewContextLike();
        Assert.That(await fresh.QueuedPlayer.CountAsync(x => x.UserID == user.Id), Is.EqualTo(1));
    }

    [Test]
    public async Task AddPlayerToQueue_AnyOtherSaveFailure_IsThrown()
    {
        var user = await AddUser("unlucky");
        _context.SavingChanges += (_, _) => throw new DbUpdateException("database down");

        await Assert.ThrowsAsync<DbUpdateException>(() => _repo.AddPlayerToQueue(QueuedPlayer.Seed(user.Id, DateTime.UtcNow)));
        await using var fresh = _context.NewContextLike();
        Assert.That(await fresh.QueuedPlayer.AnyAsync(), Is.False);
    }

    [Test]
    public async Task PlayerState_IsTheirPlaceAndTheQueueSize_InOneRead()
    {
        var queued = await AddUser("queued");
        var playing = await AddUser("playing");
        var kicked = await AddUser("kicked");
        var linked = User.Seed(userName: "linked", xboxUser: GrifballWebApp.Database.Models.XboxUser.FromXbox(77, "GT77"), discordUser: GrifballWebApp.Database.Models.DiscordUser.FromDiscord(7700, "d7700"));
        _context.Users.Add(linked);
        _context.QueuedPlayer.AddRange(QueuedPlayer.Seed(queued.Id, DateTime.UtcNow), QueuedPlayer.Seed(linked, DateTime.UtcNow));
        _context.MatchedMatches.Add(MatchedMatch.Start(
            new MatchedTeam([new MatchedPlayer(playing.Id)]),
            new MatchedTeam([new MatchedPlayer(kicked.Id).AlreadyKicked()])));
        await _context.SaveChangesAsync();

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await _repo.PlayerState(queued.Id), Is.EqualTo(new QueuePlayerState(queued.Id, HasGamertag: false, InQueue: true, InMatch: false, QueueCount: 2)));
            Assert.That(await _repo.PlayerState(playing.Id), Is.EqualTo(new QueuePlayerState(playing.Id, HasGamertag: false, InQueue: false, InMatch: true, QueueCount: 2)));
            Assert.That(await _repo.PlayerState(kicked.Id), Is.EqualTo(new QueuePlayerState(kicked.Id, HasGamertag: false, InQueue: false, InMatch: false, QueueCount: 2)));
            Assert.That(await _repo.PlayerStateByDiscord(7700), Is.EqualTo(new QueuePlayerState(linked.Id, HasGamertag: true, InQueue: true, InMatch: false, QueueCount: 2)));
            Assert.That(await _repo.PlayerState(-1), Is.Null);
            Assert.That(await _repo.PlayerStateByDiscord(1), Is.Null);
        });
    }
}
