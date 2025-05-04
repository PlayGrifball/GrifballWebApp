using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Matchmaking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Microsoft.Extensions.Configuration;
using GrifballWebApp.Server;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore.Diagnostics;
using GrifballWebApp.Seeder;
using EntityFrameworkCore.Testing.NSubstitute;
using GrifballWebApp.Server.Services;
using NetCord.Services;
using Surprenant.Grunt.Models.HaloInfinite;


namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class DisplayQueueServiceTests
{
    private ILogger<DisplayQueueService> _logger;
    private IServiceScopeFactory _serviceScopeFactory;
    private IServiceScope _serviceScope;
    private IQueueService _queueService;
    private IDataPullService _dataPullService;
    private GrifballContext _context;
    private IDiscordClient _restClient;
    private IConfiguration _configuration;
    private IOptions<DiscordOptions> _discordOptions;

    private DisplayQueueService _service;

    [SetUp]
    public async Task Setup()
    {
        // Configure in-memory database with unique name per test
        var options = new DbContextOptionsBuilder<GrifballContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique database per test
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = Create.MockedDbContextFor<GrifballContext>(options);

        // Substitute dependencies
        _logger = Substitute.For<ILogger<DisplayQueueService>>();
        _serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceScope = Substitute.For<IServiceScope>();
        _queueService = new QueryService(_context);
        _dataPullService = Substitute.For<IDataPullService>();
        _restClient = Substitute.For<IDiscordClient>();
        _configuration = Substitute.For<IConfiguration>();
        _discordOptions = Substitute.For<IOptions<DiscordOptions>>();
        _discordOptions.Value.Returns(new DiscordOptions
        {
            QueueChannel = 123456789012345678, // Example channel ID
            MatchPlayers = 8,
        });

        // Setup service scope factory
        _serviceScopeFactory.CreateScope().Returns(_serviceScope);

        // Setup service provider
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IQueueService)).Returns(_queueService);
        serviceProvider.GetService(typeof(IDataPullService)).Returns(_dataPullService);
        serviceProvider.GetService(typeof(GrifballContext)).Returns(_context);
        serviceProvider.GetService(typeof(IDiscordClient)).Returns(_restClient);
        serviceProvider.GetService(typeof(IConfiguration)).Returns(_configuration);
        serviceProvider.GetService(typeof(IOptions<DiscordOptions>)).Returns(_discordOptions);

        _serviceScope.ServiceProvider.Returns(serviceProvider);

        // Initialize the service
        _service = new DisplayQueueService(_logger, _serviceScopeFactory);

        // Seed ranks
        await new RankSeeder(_context, new TestReader()).SeedRanks();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceScope.Dispose();
        _context.Dispose();
        //_restClient.Dispose();
        _service.Dispose();
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
        _context.XboxUsers.AddRange([
            new() { XboxUserID = 1, Gamertag = "1" },
            new() { XboxUserID = 2, Gamertag = "2" },
            new() { XboxUserID = 3, Gamertag = "3" },
            new() { XboxUserID = 4, Gamertag = "4" },
            new() { XboxUserID = 5, Gamertag = "5" },
            new() { XboxUserID = 6, Gamertag = "6" },
            new() { XboxUserID = 7, Gamertag = "7" },
            new() { XboxUserID = 8, Gamertag = "8" },
            new() { XboxUserID = 9, Gamertag = "9" },
            new() { XboxUserID = 10, Gamertag = "10" },
            ]);
        await _context.SaveChangesAsync();
        var queuedPlayers = new[]
        {
            new QueuedPlayer { DiscordUserID = 1, JoinedAt = now.AddMinutes(-9), DiscordUser = new DiscordUser { MMR = 1000, DiscordUserID = 1, DiscordUsername = "1", XboxUserID = 1 } },
            new QueuedPlayer { DiscordUserID = 2, JoinedAt = now.AddMinutes(-8), DiscordUser = new DiscordUser { MMR = 1000, DiscordUserID = 2, DiscordUsername = "2", XboxUserID = 2 } },
            new QueuedPlayer { DiscordUserID = 3, JoinedAt = now.AddMinutes(-7), DiscordUser = new DiscordUser { MMR = 1000, DiscordUserID = 3, DiscordUsername = "3", XboxUserID = 3 } },
            new QueuedPlayer { DiscordUserID = 4, JoinedAt = now.AddMinutes(-6), DiscordUser = new DiscordUser { MMR = 1000, DiscordUserID = 4, DiscordUsername = "4", XboxUserID = 4 } },
            new QueuedPlayer { DiscordUserID = 5, JoinedAt = now.AddMinutes(-5), DiscordUser = new DiscordUser { MMR = 1000, DiscordUserID = 5, DiscordUsername = "5", XboxUserID = 5 } },
            new QueuedPlayer { DiscordUserID = 6, JoinedAt = now.AddMinutes(-4), DiscordUser = new DiscordUser { MMR = 1000, DiscordUserID = 6, DiscordUsername = "6", XboxUserID = 6 } },
            new QueuedPlayer { DiscordUserID = 7, JoinedAt = now.AddMinutes(-3), DiscordUser = new DiscordUser { MMR = 1000, DiscordUserID = 7, DiscordUsername = "7", XboxUserID = 7 } },
            new QueuedPlayer { DiscordUserID = 8, JoinedAt = now.AddMinutes(-2), DiscordUser = new DiscordUser { MMR = 1000, DiscordUserID = 8, DiscordUsername = "8", XboxUserID = 8 } },
            new QueuedPlayer { DiscordUserID = 9, JoinedAt = now.AddMinutes(-1), DiscordUser = new DiscordUser { MMR = 1000, DiscordUserID = 9, DiscordUsername = "9", XboxUserID = 9 } },
            new QueuedPlayer { DiscordUserID = 10, JoinedAt = now.AddMinutes(-1), DiscordUser = new DiscordUser { MMR = 1000, DiscordUserID = 10, DiscordUsername = "10", XboxUserID = 10 } },
        };
        _context.QueuedPlayer.AddRange(queuedPlayers);
        await _context.SaveChangesAsync();

        // Act
        await _service.Go(CancellationToken.None);

        // Assert
        var count = await _context.MatchedMatches
            .Where(x => x.Active)
            .CountAsync();
        var stillQueued = await _context.QueuedPlayer
            .Select(x => x.DiscordUserID)
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

        // Arrange - Fake match
        var start = now.AddSeconds(10);
        var end = now.AddMinutes(12);
        var duration = end - start;
        var match = new Match()
        {
            MatchID = Guid.NewGuid(),
            StartTime = start,
            EndTime = end,
            Duration = duration,
            StatsPullDate = DateTime.UtcNow,
        };
        _context.Matches.Add(match);

        var team0 = new MatchTeam()
        {
            Match = match,
            TeamID = 0,
            Score = 5,
            Outcome = Outcomes.Won,
            MatchParticipants = [
                new MatchParticipant { XboxUserID = 1},
                new MatchParticipant { XboxUserID = 3},
                new MatchParticipant { XboxUserID = 5},
                new MatchParticipant { XboxUserID = 7},
            ],
        };
        var team1 = new MatchTeam()
        {
            Match = match,
            TeamID = 1,
            Score = 3,
            Outcome = Outcomes.Lost,
            MatchParticipants = [
                new MatchParticipant { XboxUserID = 2},
                new MatchParticipant { XboxUserID = 4},
                new MatchParticipant { XboxUserID = 6},
                new MatchParticipant { XboxUserID = 8},
            ],
        };
        await _context.MatchTeams.AddRangeAsync([team0, team1]);
        match.MatchTeams = [team0, team1];

        await _context.SaveChangesAsync();

        // Act
        await _service.Go(CancellationToken.None);

        // Assert
        // Matched match should be created now not be active
        var count2 = await _context.MatchedMatches
            .Where(x => x.Active)
            .CountAsync();
        var count3 = await _context.MatchedMatches
            .Where(x => !x.Active)
            .CountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(count2, Is.EqualTo(0), "There should be no active matches after we pull in a 'matched' infinite match");
            Assert.That(count3, Is.EqualTo(1), "There previously active match has been complete after we pull in a 'matched' infinite match");
        });

        // Check the players MMR has been adjusted
        var winners = await _context.DiscordUsers
            .Where(x => x.DiscordUserID == 1 || x.DiscordUserID == 3 || x.DiscordUserID == 5 || x.DiscordUserID == 7)
            .Select(x => x.MMR)
            .ToArrayAsync();
        var losers = await _context.DiscordUsers
            .Where(x => x.DiscordUserID == 2 || x.DiscordUserID == 4 || x.DiscordUserID == 6 || x.DiscordUserID == 8)
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
        var queuedPlayers = new[]
        {
            new QueuedPlayer { DiscordUserID = 9, DiscordUser = new DiscordUser { MMR = 1000, DiscordUserID = 9, DiscordUsername = "9" } },
        };
        _context.QueuedPlayer.AddRange(queuedPlayers);
        await _context.SaveChangesAsync();

        // Act
        await _service.Go(CancellationToken.None);

        // Assert
        var count = await _context.MatchedMatches
            .Where(x => x.Active)
            .CountAsync();
        Assert.That(count, Is.EqualTo(0), "There should be no active matches when only 1 player has been queued");
    }
}
