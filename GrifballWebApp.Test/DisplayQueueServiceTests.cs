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
        var queuedPlayers = new[]
        {
            new QueuedPlayer { DiscordUserID = 1, JoinedAt = now.AddMinutes(-8), DiscordUser = new DiscordUser { MMR = 1000, DiscordUserID = 1, DiscordUsername = "1" } },
            new QueuedPlayer { DiscordUserID = 2, JoinedAt = now.AddMinutes(-8), DiscordUser = new DiscordUser { MMR = 1200, DiscordUserID = 2, DiscordUsername = "2" } },
            new QueuedPlayer { DiscordUserID = 3, JoinedAt = now.AddMinutes(-7), DiscordUser = new DiscordUser { MMR = 1000, DiscordUserID = 3, DiscordUsername = "3" } },
            new QueuedPlayer { DiscordUserID = 4, JoinedAt = now.AddMinutes(-7), DiscordUser = new DiscordUser { MMR = 1200, DiscordUserID = 4, DiscordUsername = "4" } },
            new QueuedPlayer { DiscordUserID = 5, JoinedAt = now.AddMinutes(-6), DiscordUser = new DiscordUser { MMR = 1000, DiscordUserID = 5, DiscordUsername = "5" } },
            new QueuedPlayer { DiscordUserID = 6, JoinedAt = now.AddMinutes(-5), DiscordUser = new DiscordUser { MMR = 1200, DiscordUserID = 6, DiscordUsername = "6" } },
            new QueuedPlayer { DiscordUserID = 7, JoinedAt = now.AddMinutes(-4), DiscordUser = new DiscordUser { MMR = 1000, DiscordUserID = 7, DiscordUsername = "7" } },
            new QueuedPlayer { DiscordUserID = 8, JoinedAt = now.AddMinutes(-3), DiscordUser = new DiscordUser { MMR = 1200, DiscordUserID = 8, DiscordUsername = "8" } },
            new QueuedPlayer { DiscordUserID = 9, JoinedAt = now.AddMinutes(-2), DiscordUser = new DiscordUser { MMR = 1200, DiscordUserID = 9, DiscordUsername = "9" } },
            new QueuedPlayer { DiscordUserID = 10, JoinedAt = now.AddMinutes(-1), DiscordUser = new DiscordUser { MMR = 1200, DiscordUserID = 10, DiscordUsername = "10" } },
        };
        _context.QueuedPlayer.AddRange(queuedPlayers);
        await _context.SaveChangesAsync();

        // Act
        await _service.Go(CancellationToken.None);

        // Assert
        var count = await _context.MatchedMatchs
            .Where(x => x.Active)
            .CountAsync();
        var stillQueued = await _context.QueuedPlayer
            .Select(x => x.DiscordUserID)
            .ToArrayAsync();
        Assert.That(count, Is.EqualTo(1), "There should be one active match created when 8 players have been queued");
        Assert.That(stillQueued.Count(), Is.EqualTo(2), "There should two players left in the queue since we started with 10 queued players");
        Assert.That(stillQueued.Any(x => x is 9), Is.True, "Player 9 should still be in the queue since he queued recently");
        Assert.That(stillQueued.Any(x => x is 10), Is.True, "Player 10 should still be in the queue since he queued recently");

        // Check the dataPullerService check match has been called 
        await _dataPullService.Received(1).DownloadRecentMatchesForPlayers(Arg.Is<List<long>>(x => x.Count == 0), 0, 0, 2, Arg.Any<CancellationToken>());
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
        var count = await _context.MatchedMatchs
            .Where(x => x.Active)
            .CountAsync();
        Assert.That(count, Is.EqualTo(0), "There should be no active matches when only 1 player has been queued");
    }
}
