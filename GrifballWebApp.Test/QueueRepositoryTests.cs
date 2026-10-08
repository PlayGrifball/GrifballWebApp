using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Services;
using MediatR;
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
        var user = new User { UserName = name };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    [Test]
    public async Task AddGetRemove_RoundTrip()
    {
        var user = await AddUser("a");

        var before = await _repo.GetQueuePlayer(user.Id);
        var added = await _repo.AddPlayerToQueue(user.Id);
        var addedAgain = await _repo.AddPlayerToQueue(user.Id);
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
        _context.MatchedMatches.Add(new MatchedMatch
        {
            HomeTeam = new MatchedTeam { Players = [new MatchedPlayer { UserID = home.Id }, new MatchedPlayer { UserID = kicked.Id, Kicked = true }] },
            AwayTeam = new MatchedTeam { Players = [new MatchedPlayer { UserID = away.Id }] },
        });
        _context.MatchedMatches.Add(new MatchedMatch
        {
            Active = false,
            HomeTeam = new MatchedTeam { Players = [new MatchedPlayer { UserID = finished.Id }] },
            AwayTeam = new MatchedTeam(),
        });
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
}
