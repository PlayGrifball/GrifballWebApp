using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Seeder;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Outbox;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QueueServiceTests
{
    private ILogger<QueueService> _logger;
    private IQueueRepository _queueRepository;
    private IDataPullService _dataPullService;
    private GrifballContext _context;
    private IDiscordRestClient _discordClient;
    private IOptions<DiscordOptions> _discordOptions;

    private QueueService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();

        // Substitute dependencies
        _logger = Substitute.For<ILogger<QueueService>>();
        _queueRepository = new QueueRepository(_context);
        _dataPullService = Substitute.For<IDataPullService>();
        _discordClient = Substitute.For<IDiscordRestClient>();
        var msg = Substitute.For<IDiscordRestMessage>();
        var author = Substitute.For<IDiscordUser>();
        author.Id.Returns(1ul); // Should match bot id
        msg.Author.Returns(author);
        _discordClient.SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(msg);
        var thread = Substitute.For<IDiscordGuildThread>();
        thread.Id.Returns(1ul);
        _discordClient.CreateGuildThreadAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<GuildThreadFromMessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(thread);
        _discordOptions = Substitute.For<IOptions<DiscordOptions>>();
        _discordOptions.Value.Returns(new DiscordOptions
        {
            QueueChannel = 123456789012345678, // Example channel ID
            MatchPlayers = 8,
        });

        // Initialize the service
        _service = new QueueService(_logger, _discordOptions, _queueRepository, _discordClient, _context, _dataPullService, new ContextOutbox(_context), Substitute.For<IQueueNotifier>());

        // Seed ranks
        await new RankSeeder(_context, new TestReader()).SeedRanks();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task ShouldHaveRanks()
    {
        // Arrange
        // No specific arrangement needed for this test. Currently, arranged in setup. TODO: move this test to its own file.

        // Act
        var hasRanks = await _context.Ranks.AnyAsync();

        // Assert
        Assert.That(hasRanks, Is.True, "There should be ranks");
    }

    [Test]
    public async Task Go_ShouldCreateMatch_WhenEnoughPlayersInQueue()
    {
        var now = DateTime.UtcNow;
        // Arrange
        using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.DisableContraints("[Auth].[Users]");
        _context.XboxUsers.AddRange([
            XboxUser.FromXbox(1, "1"),
            XboxUser.FromXbox(2, "2"),
            XboxUser.FromXbox(3, "3"),
            XboxUser.FromXbox(4, "4"),
            XboxUser.FromXbox(5, "5"),
            XboxUser.FromXbox(6, "6"),
            XboxUser.FromXbox(7, "7"),
            XboxUser.FromXbox(8, "8"),
            XboxUser.FromXbox(9, "9"),
            XboxUser.FromXbox(10, "10"),
            ]);
        await _context.SaveChangesAsync();
        var queuedPlayers = new[]
        {
            QueuedPlayer.Seed(User.Seed(mmr: 1000, id: 1, xboxUserID: 1), now.AddMinutes(-9)),
            QueuedPlayer.Seed(User.Seed(mmr: 1000, id: 2, xboxUserID: 2), now.AddMinutes(-8)),
            QueuedPlayer.Seed(User.Seed(mmr: 1000, id: 3, xboxUserID: 3), now.AddMinutes(-7)),
            QueuedPlayer.Seed(User.Seed(mmr: 1000, id: 4, xboxUserID: 4), now.AddMinutes(-6)),
            QueuedPlayer.Seed(User.Seed(mmr: 1000, id: 5, xboxUserID: 5), now.AddMinutes(-5)),
            QueuedPlayer.Seed(User.Seed(mmr: 1000, id: 6, xboxUserID: 6), now.AddMinutes(-4)),
            QueuedPlayer.Seed(User.Seed(mmr: 1000, id: 7, xboxUserID: 7), now.AddMinutes(-3)),
            QueuedPlayer.Seed(User.Seed(mmr: 1000, id: 8, xboxUserID: 8), now.AddMinutes(-2)),
            QueuedPlayer.Seed(User.Seed(mmr: 1000, id: 9, xboxUserID: 9), now.AddMinutes(-1)),
            QueuedPlayer.Seed(User.Seed(mmr: 1000, id: 10, xboxUserID: 10), now.AddMinutes(-1)),
        };
        _context.QueuedPlayer.AddRange(queuedPlayers);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        // Act
        await _service.Go(CancellationToken.None);

        // Assert
        var count = await _context.MatchedMatches
            .Where(x => x.Status == MatchedMatchStatus.Active)
            .CountAsync();
        var stillQueued = await _context.QueuedPlayer
            .Select(x => x.UserID)
            .ToArrayAsync();
        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(1), "There should be one active match created when 8 players have been queued");
            Assert.That(stillQueued.Count(), Is.EqualTo(2), "There should two players left in the queue since we started with 10 queued players");
            Assert.That(stillQueued.Any(x => x is 9), Is.True, "Player 9 should still be in the queue since he queued recently");
            Assert.That(stillQueued.Any(x => x is 10), Is.True, "Player 10 should still be in the queue since he queued recently");
        });

        // Check the dataPullerService check match has been called 
        await _dataPullService.Received(1).DownloadRecentMatchesForPlayers(Arg.Is<List<long>>(x => x.Count == 0), 0, 0, 2, Arg.Any<CancellationToken>());

        // The match is announced (its vote message posted) by the outbox: only then can it end.
        await using (var outbox = new OutboxTestDriver(_context, _discordClient, _discordOptions.Value))
            await outbox.DrainAsync();
        _context.ChangeTracker.Clear();

        // Arrange - Fake match
        var start = now.AddSeconds(10);
        var end = now.AddMinutes(12);
        var duration = end - start;
        var match = Match.Seed(Guid.NewGuid(), startTime: start, endTime: end, duration: duration, statsPullDate: DateTime.UtcNow);
        match.AddTeam(0, 5, Outcomes.Won);
        match.AddTeam(1, 3, Outcomes.Lost);
        foreach (var xuid in new long[] { 1, 3, 5, 7 })
            match.AddParticipant(0, xuid, new MatchParticipantStats());
        foreach (var xuid in new long[] { 2, 4, 6, 8 })
            match.AddParticipant(1, xuid, new MatchParticipantStats());
        _context.Matches.Add(match);

        await _context.SaveChangesAsync();

        // Act
        await _service.Go(CancellationToken.None);

        // Assert
        // Matched match should be created now not be active
        var count2 = await _context.MatchedMatches
            .Where(x => x.Status == MatchedMatchStatus.Active)
            .CountAsync();
        var count3 = await _context.MatchedMatches
            .Where(x => x.Status == MatchedMatchStatus.Reported && x.MatchID != null && x.WinnerSide == null)
            .CountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(count2, Is.EqualTo(0), "There should be no active matches after we pull in a 'matched' infinite match");
            Assert.That(count3, Is.EqualTo(1), "The previously active match is reported with the Infinite match pulled in");
        });

        // Check the players MMR has been adjusted
        var winners = await _context.Users
            .Where(x => x.Id == 1 || x.Id == 3 || x.Id == 5 || x.Id == 7)
            .Select(x => x.MMR)
            .ToArrayAsync();
        var losers = await _context.Users
            .Where(x => x.Id == 2 || x.Id == 4 || x.Id == 6 || x.Id == 8)
            .Select(x => x.MMR)
            .ToArrayAsync();

        Assert.Multiple(() =>
        {
            Assert.That(winners.Length, Is.EqualTo(4), "There should be 4 winners");
            Assert.That(losers.Length, Is.EqualTo(4), "There should be 4 losers");
            Assert.That(winners.All(x => x > 1000), Is.True, "All winners should have an MMR greater than 1000");
            Assert.That(losers.All(x => x < 1000), Is.True, "All losers should have an MMR less than 1000");
        });
    }

    [Test]
    public async Task Go_ShouldNotCreateMatch_WhenNotEnoughPlayersInQueue()
    {
        //Arrange
        using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.DisableContraints("[Auth].[Users]");
        var queuedPlayers = new[]
        {
            QueuedPlayer.Seed(User.Seed(mmr: 1000, id: 9), DateTime.UtcNow),
        };
        _context.QueuedPlayer.AddRange(queuedPlayers);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        // Act
        await _service.Go(CancellationToken.None);

        // Assert
        var count = await _context.MatchedMatches
            .Where(x => x.Status == MatchedMatchStatus.Active)
            .CountAsync();
        Assert.That(count, Is.EqualTo(0), "There should be no active matches when only 1 player has been queued");
    }

    [Test]
    public async Task Go_WaitsForTheQueueLock_WhileAnotherProcessMatchmakes()
    {
        // The matchmaking job on a worker and a queue button on grif-discord: the second waits for the first, so the
        // same queue is never read twice and its players put in two matches
        await using var otherProcess = _context.NewContextLike();
        var held = await otherProcess.Database.AcquireLockAsync(QueueService.LockName);

        var go = _service.Go(CancellationToken.None);
        await Task.Delay(500);
        Assert.That(go.IsCompleted, Is.False, "Waiting for the other process's run");

        await held.DisposeAsync();
        await go.WaitAsync(TimeSpan.FromSeconds(30));

        // And it let go of the lock once done
        await (await otherProcess.Database.AcquireLockAsync(QueueService.LockName).WaitAsync(TimeSpan.FromSeconds(30))).DisposeAsync();
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QueueServiceCountTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task Setup() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private QueueService Service(IQueueNotifier notifier) => new(Substitute.For<ILogger<QueueService>>(),
        Options.Create(new DiscordOptions { QueueChannel = 1, MatchPlayers = 8 }), new QueueRepository(_context),
        Substitute.For<IDiscordRestClient>(), _context, Substitute.For<IDataPullService>(), new ContextOutbox(_context), notifier);

    [Test]
    public async Task Go_TellsTheQueueWatchers_ItsSize()
    {
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            await _context.DisableContraints("[Auth].[Users]");
            _context.QueuedPlayer.Add(QueuedPlayer.Seed(User.Seed(mmr: 1000, id: 9), DateTime.UtcNow));
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        var notifier = Substitute.For<IQueueNotifier>();

        await Service(notifier).Go(CancellationToken.None);

        await notifier.Received(1).Send(1);
    }
}
