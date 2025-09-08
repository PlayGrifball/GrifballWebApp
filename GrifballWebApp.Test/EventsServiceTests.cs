using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using GrifballWebApp.Server;
using Microsoft.Extensions.Configuration;
using NetCord.Rest;

namespace GrifballWebApp.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class EventsServiceTests
    {
        private GrifballContext _context;
        private ILogger<EventsService> _logger;
        private IOptions<DiscordOptions> _discordOptions;
        private IDiscordRestClient _discordClient;
        private UrlService _urlService;
        private EventsService _service;

        [SetUp]
        public async Task SetUp()
        {
            _context = await SetUpFixture.NewGrifballContext();
            _logger = Substitute.For<ILogger<EventsService>>();
            _discordOptions = Substitute.For<IOptions<DiscordOptions>>();
            _discordOptions.Value.Returns(new DiscordOptions { EventsChannel = 123456 });
            _discordClient = Substitute.For<IDiscordRestClient>();
            var config = Substitute.For<IConfiguration>();
            config["BaseUrl"].Returns("https://localhost:4200");
            _urlService = new UrlService(config);
            _service = new EventsService(_logger, _discordOptions, _discordClient, _context, _urlService);
        }

        [TearDown]
        public async Task TearDown()
        {
            await _context.DropDatabaseAndDispose();
        }

        [Test]
        public async Task Go_Should_UpsertMessage_WhenNoExistingMessage()
        {
            // Arrange: Seed a season that is currently active
            var now = DateTime.UtcNow;
            var season = new Season
            {
                SeasonName = "Test Season",
                PublicAt = now.AddDays(-1),
                SeasonStart = now.AddDays(-1),
                SeasonEnd = now.AddDays(10),
                SignupsOpen = now.AddDays(-1),
                SignupsClose = now.AddDays(5),
                DraftStart = now.AddDays(2),
                SeasonID = 42
            };
            _context.Seasons.Add(season);
            var trans = await _context.Database.BeginTransactionAsync();
            await _context.DisableContraints("Event.Seasons");
            await _context.SaveChangesAsync();
            await _context.EnableContraints("Event.Seasons");
            await trans.CommitAsync();

            // Mock Discord client: No existing message
            _discordClient.GetCurrentUserAsync(null, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Substitute.For<IDiscordCurrentUser>()));
            _discordClient.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), null)
                .Returns(AsyncEnumerable.Empty<IDiscordRestMessage>());

            // Act
            await _service.Go(CancellationToken.None);
            
            // Assert: UpsertMessageAsync should be called to send a new message
            await _discordClient.Received(1).UpsertMessageAsync(
                Arg.Any<ulong>(),
                null, // No message ID, so should send new
                Arg.Is<MessageProperties>(mp => mp.Content == "Test Season"),
                null,
                Arg.Any<CancellationToken>()
            );
        }

        [Test]
        public async Task Go_Should_UpsertMessage_WhenExistingMessage()
        {
            // Arrange: Seed a season that is currently active
            var now = DateTime.UtcNow;
            var season = new Season
            {
                SeasonName = "Test Season",
                PublicAt = now.AddDays(-1),
                SeasonStart = now.AddDays(-1),
                SeasonEnd = now.AddDays(10),
                SignupsOpen = now.AddDays(-1),
                SignupsClose = now.AddDays(5),
                DraftStart = now.AddDays(2),
                SeasonID = 43
            };
            _context.Seasons.Add(season);
            var trans = await _context.Database.BeginTransactionAsync();
            await _context.DisableContraints("Event.Seasons");
            await _context.SaveChangesAsync();
            await _context.EnableContraints("Event.Seasons");
            await trans.CommitAsync();

            // Mock Discord client: Existing message with same content
            var existingMessage = Substitute.For<IDiscordRestMessage>();
            existingMessage.Content.Returns("Test Season");
            existingMessage.CreatedAt.Returns(now.AddMinutes(-10));
            existingMessage.Id.Returns(123UL);

            _discordClient.GetCurrentUserAsync(null, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Substitute.For<IDiscordCurrentUser>()));
            _discordClient.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), null)
                .Returns(new[] { existingMessage }.ToAsyncEnumerable());

            // Act
            await _service.Go(CancellationToken.None);

            // Assert: UpsertMessageAsync should be called to modify the existing message
            await _discordClient.Received(1).UpsertMessageAsync(
                Arg.Any<ulong>(),
                123UL, // Existing message ID
                Arg.Is<MessageProperties>(mp => mp.Content == "Test Season"),
                null,
                Arg.Any<CancellationToken>()
            );
        }
    }
}
