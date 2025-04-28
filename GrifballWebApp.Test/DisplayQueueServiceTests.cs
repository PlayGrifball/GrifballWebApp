using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using NUnit.Framework;
using NSubstitute;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NSubstitute.ExceptionExtensions;
using GrifballWebApp.Server;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;


namespace GrifballWebApp.Test;

public class DisplayQueueServiceTests
{
    private ILogger<DisplayQueueService> _logger;
    private IServiceScopeFactory _serviceScopeFactory;
    private IServiceScope _serviceScope;
    private IQueueService _queueService;
    private GrifballContext _context;
    private IDiscordClient _restClient;
    private IConfiguration _configuration;
    private IOptions<DiscordOptions> _discordOptions;

    private DisplayQueueService _service;

    [SetUp]
    public void Setup()
    {
        // Substitute dependencies
        _logger = Substitute.For<ILogger<DisplayQueueService>>();
        _serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceScope = Substitute.For<IServiceScope>();
        _queueService = Substitute.For<IQueueService>();
        _restClient = Substitute.For<IDiscordClient>();
        _configuration = Substitute.For<IConfiguration>();
        _discordOptions = Substitute.For<IOptions<DiscordOptions>>();
        _discordOptions.Value.Returns(new DiscordOptions
        {
            QueueChannel = 123456789012345678 // Example channel ID
        });

        // Configure in-memory database
        var options = new DbContextOptionsBuilder<GrifballContext>()
            .UseInMemoryDatabase(databaseName: "TestDatabase")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new GrifballContext(options);

        // Setup service scope factory
        _serviceScopeFactory.CreateScope().Returns(_serviceScope);

        // Setup service provider
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IQueueService)).Returns(_queueService);
        serviceProvider.GetService(typeof(GrifballContext)).Returns(_context);
        serviceProvider.GetService(typeof(IDiscordClient)).Returns(_restClient);
        serviceProvider.GetService(typeof(IConfiguration)).Returns(_configuration);
        serviceProvider.GetService(typeof(IOptions<DiscordOptions>)).Returns(_discordOptions);

        _serviceScope.ServiceProvider.Returns(serviceProvider);

        // Initialize the service
        _service = new DisplayQueueService(_logger, _serviceScopeFactory);
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
    public async Task Go_ShouldRemoveTimedOutPlayers_WhenQueueIsEmpty()
    {
        // Arrange
        _queueService.GetQueuePlayersWithInfo(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new QueuedPlayer[0]));

        // Act
        await _service.Go(CancellationToken.None);

        // Assert
        //await _queueService.Received(1).GetQueuePlayersWithInfo(Arg.Any<CancellationToken>());
        //await _context.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.That(_context.MatchedMatchs.Count(), Is.EqualTo(0));
        Assert.That(_context.QueuedPlayer.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Go_ShouldCreateMatch_WhenEnoughPlayersInQueue()
    {
        // Arrange
        var queuedPlayers = new[]
        {
            new QueuedPlayer { DiscordUserID = 1, DiscordUser = new DiscordUser { MMR = 1000 } },
            new QueuedPlayer { DiscordUserID = 2, DiscordUser = new DiscordUser { MMR = 1200 } }
        };

        _queueService.GetQueuePlayersWithInfo(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(queuedPlayers));

        // Act
        await _service.Go(CancellationToken.None);

        // Assert
        await _context.Received(1).MatchedMatchs.AddAsync(Arg.Any<MatchedMatch>(), Arg.Any<CancellationToken>());
        await _context.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
