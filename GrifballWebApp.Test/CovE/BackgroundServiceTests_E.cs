using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Events;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Test.CovE;

/// <summary>
/// The background services tick every 10 seconds, so only the first (immediate) iteration is exercised here.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class BackgroundServiceTests_E
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static IServiceScopeFactory ScopeFactoryReturning(Type serviceType, object? instance, Action? onCreateScope = null)
    {
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(serviceType).Returns(instance);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(provider);
        var factory = Substitute.For<IServiceScopeFactory>();
        factory.CreateScope().Returns(_ => { onCreateScope?.Invoke(); return scope; });
        return factory;
    }

    [Test]
    public async Task QueueBackgroundService_WhenQueueServiceCannotBeResolved_LogsErrorAndKeepsRunning()
    {
        var logger = new RecordingLogger_E<QueueBackgroundService>();
        var service = new QueueBackgroundService(logger, ScopeFactoryReturning(typeof(QueueService), null));

        await service.StartAsync(CancellationToken.None);
        var logged = await logger.WaitForEntries(1, Timeout);
        await service.StopAsync(CancellationToken.None);

        Assert.That(logged, Is.True, "First iteration should run immediately");
        var entry = logger.Entries.First();
        Assert.Multiple(() =>
        {
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Message, Is.EqualTo("An error occurred while executing the DisplayQueueService"));
            Assert.That(entry.Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(service.ExecuteTask!.IsCompletedSuccessfully, Is.True, "ExecuteAsync hands the loop off to a background task");
        });
    }

    [Test]
    public async Task EventsBackgroundService_WhenEventsServiceCannotBeResolved_LogsError()
    {
        var logger = new RecordingLogger_E<EventsBackgroundService>();
        var service = new EventsBackgroundService(logger, ScopeFactoryReturning(typeof(EventsService), null));

        await service.StartAsync(CancellationToken.None);
        var logged = await logger.WaitForEntries(1, Timeout);
        await service.StopAsync(CancellationToken.None);

        Assert.That(logged, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(logger.Entries.First().Level, Is.EqualTo(LogLevel.Error));
            Assert.That(logger.Entries.First().Message, Is.EqualTo("An error occurred while executing the EventsService"));
        });
    }

    [Test]
    public async Task QueueBackgroundService_StopAsync_CancelsBeforeNextTick()
    {
        var created = 0;
        var logger = new RecordingLogger_E<QueueBackgroundService>();
        var service = new QueueBackgroundService(logger, ScopeFactoryReturning(typeof(QueueService), null, () => Interlocked.Increment(ref created)));

        await service.StartAsync(CancellationToken.None);
        await logger.WaitForEntries(1, Timeout);
        await service.StopAsync(CancellationToken.None);
        await Task.Delay(50);

        Assert.That(created, Is.EqualTo(1), "Only the immediate iteration ran; the 10s timer never ticked");
    }
}

/// <summary>
/// Background service happy paths: the first iteration resolves the real service from the scope and runs it.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class BackgroundServiceDbTests_E
{
    private GrifballContext _context;
    private IDiscordRestClient _discordClient;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _discordClient = Substitute.For<IDiscordRestClient>();
        _discordClient.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private static IServiceScopeFactory ScopeFactory<T>(T instance) where T : class
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => instance);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    [Test]
    public async Task QueueBackgroundService_RunsQueueServiceImmediately()
    {
        var me = Substitute.For<IDiscordCurrentUser>();
        _discordClient.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(me);
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _discordClient.SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(_ => { sent.TrySetResult(); return Substitute.For<IDiscordRestMessage>(); });
        var queueService = new QueueService(Substitute.For<ILogger<QueueService>>(), Options.Create(new DiscordOptions { QueueChannel = 55 }),
            new QueueRepository(_context), _discordClient, _context, Substitute.For<IDataPullService>());
        var logger = new RecordingLogger_E<QueueBackgroundService>();
        var service = new QueueBackgroundService(logger, ScopeFactory(queueService));

        await service.StartAsync(CancellationToken.None);
        var finished = await Task.WhenAny(sent.Task, Task.Delay(TimeSpan.FromSeconds(30))) == sent.Task;
        await service.StopAsync(CancellationToken.None);

        Assert.That(finished, Is.True, "Queue message should have been posted by the first iteration");
        await _discordClient.Received(1).SendMessageAsync(55ul, Arg.Is<MessageProperties>(m => m.Content == "Matchmaking Queue"), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        Assert.That(logger.Entries, Is.Empty, "No error should be logged");
    }

    [Test]
    public async Task EventsBackgroundService_RunsEventsServiceImmediately()
    {
        var now = DateTime.UtcNow;
        _context.Seasons.Add(new Season
        {
            SeasonName = "BG Season",
            PublicAt = now.AddDays(-1),
            SignupsOpen = now.AddDays(-1),
            SignupsClose = now.AddDays(1),
            DraftStart = now.AddDays(2),
            SeasonStart = now.AddDays(3),
            SeasonEnd = now.AddDays(30),
        });
        await _context.SaveChangesAsync();
        var upserted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _discordClient.UpsertMessageAsync(Arg.Any<ulong>(), Arg.Any<ulong?>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(_ => { upserted.TrySetResult(); return Substitute.For<IDiscordRestMessage>(); });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://example.test" }).Build();
        var eventsService = new EventsService(Substitute.For<ILogger<EventsService>>(), Options.Create(new DiscordOptions { EventsChannel = 66 }),
            _discordClient, _context, new UrlService(config));
        var logger = new RecordingLogger_E<EventsBackgroundService>();
        var service = new EventsBackgroundService(logger, ScopeFactory(eventsService));

        await service.StartAsync(CancellationToken.None);
        var finished = await Task.WhenAny(upserted.Task, Task.Delay(TimeSpan.FromSeconds(30))) == upserted.Task;
        await service.StopAsync(CancellationToken.None);

        Assert.That(finished, Is.True);
        await _discordClient.Received(1).UpsertMessageAsync(66ul, null, Arg.Is<MessageProperties>(m => m.Content == "BG Season"), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        Assert.That(logger.Entries, Is.Empty);
    }
}
