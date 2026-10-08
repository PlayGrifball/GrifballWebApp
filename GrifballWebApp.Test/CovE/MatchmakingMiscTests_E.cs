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

namespace GrifballWebApp.Test.CovE;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class QueueRepositoryTests_E
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

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class GetsertXboxUserServiceErrorTests_E
{
    private GrifballContext _context;
    private RecordingLogger_E<GetsertXboxUserService> _logger;
    private IHaloInfiniteClientFactory _factory;
    private GetsertXboxUserService _service;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _logger = new RecordingLogger_E<GetsertXboxUserService>();
        _factory = Substitute.For<IHaloInfiniteClientFactory>();
        _service = new GetsertXboxUserService(_logger, _context, _factory);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private static HaloApiResultContainer<List<GruntUser>, HaloApiErrorContainer> Error(string message) =>
        new(null, new HaloApiErrorContainer { Message = message });

    private static HaloApiResultContainer<List<GruntUser>, HaloApiErrorContainer> Users(params (string Xuid, string Gt)[] users) =>
        new(users.Select(u => new GruntUser { xuid = u.Xuid, gamertag = u.Gt }).ToList(), null);

    [Test]
    public async Task GetsertXboxUsersByXuid_AllExisting_DoesNotCallApi()
    {
        _context.XboxUsers.AddRange(new XboxUser { XboxUserID = 1, Gamertag = "a" }, new XboxUser { XboxUserID = 2, Gamertag = "b" });
        await _context.SaveChangesAsync();

        var result = await _service.GetsertXboxUsersByXuid([1, 2]);

        Assert.That(result.Select(x => x.Gamertag), Is.EquivalentTo(new[] { "a", "b" }));
        Assert.That(_factory.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public void GetsertXboxUsersByXuid_ApiError_Throws()
    {
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(Error("rate limited"));

        var ex = Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUsersByXuid([5]));

        Assert.That(ex!.Message, Is.EqualTo("Failed to resolved gamertag: rate limited"));
    }

    [Test]
    public async Task GetsertXboxUsersByXuid_ApiMissingSomeUsers_ThrowsListingThem()
    {
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(Users(("5", "five")));

        var ex = Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUsersByXuid([5, 6, 7]));

        Assert.That(ex!.Message, Is.EqualTo("Failed to find the following xbox users: 6,7"));
        Assert.That(await _context.XboxUsers.AnyAsync(), Is.False, "Nothing is saved when any user is missing");
    }

    [Test]
    public void GetsertXboxUserByXuid_ApiError_Throws()
    {
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(Error("down"));

        var ex = Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUserByXuid(5));

        Assert.That(ex!.Message, Is.EqualTo("Failed to resolved gamertag: down"));
    }

    [Test]
    public void GetsertXboxUserByXuid_UserNotReturned_LogsAndThrows()
    {
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(Users(("6", "someone else")));

        var ex = Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUserByXuid(5));

        Assert.That(ex!.Message, Is.EqualTo("Failed to find user"));
        var entry = _logger.Entries.Single();
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
        Assert.That(entry.Message, Is.EqualTo("Gamertag 5 not found"));
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class MatchmakingButtonVoteTests_E
{
    private GrifballContext _context;
    private IDiscordButtonInteractionContext _discordContext;
    private ButtonInteractions _buttons;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var discordClient = Substitute.For<IDiscordRestClient>();
        var options = Options.Create(new DiscordOptions { QueueChannel = 1 });
        var repo = Substitute.For<IQueueRepository>();
        var queueService = new QueueService(Substitute.For<ILogger<QueueService>>(), options, repo, discordClient, _context, Substitute.For<IDataPullService>());
        _buttons = new ButtonInteractions(repo, Substitute.For<IPublisher>(), _context, discordClient, options, queueService);
        _discordContext = Substitute.For<IDiscordButtonInteractionContext>();
        typeof(ButtonInteractions).GetField("_discordContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(_buttons, _discordContext);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task VoteForWinner_UnparseableWinner_RepliesWithExpectedValues()
    {
        _context.Users.Add(new User { UserName = "voter", XboxUser = new XboxUser { XboxUserID = 3, Gamertag = "gt" }, DiscordUser = new Database.Models.DiscordUser { DiscordUserID = 42, DiscordUsername = "voter" } });
        await _context.SaveChangesAsync();
        _discordContext.User.Id.Returns(42ul);

        await _buttons.VoteForWinner(1, "Nobody");

        await _discordContext.AssertSendResponse("I could not parse the value Nobody. Contact developer, expected values are Home,Away,Cancel");
        Assert.That(await _context.MatchedWinnerVotes.AnyAsync(), Is.False);
    }
}
