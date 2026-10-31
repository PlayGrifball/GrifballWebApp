using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Events;
using GrifballWebApp.Server.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class EventsServiceStatusTests
{
    private GrifballContext _context;
    private IDiscordRestClient _discordClient;
    private EventsService _service;
    private UrlService _urlService;
    private readonly DiscordOptions _options = new() { EventsChannel = 66 };
    private OutboxTestDriver _outbox;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _discordClient = Substitute.For<IDiscordRestClient>();
        _discordClient.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://example.test" }).Build();
        _urlService = new UrlService(config);
        _service = new EventsService(Substitute.For<ILogger<EventsService>>(), Options.Create(_options), new ContextOutbox(_context), _context, _urlService);
        _outbox = new OutboxTestDriver(_context, _discordClient, _options);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _outbox.DisposeAsync();
        await _context.DropDatabaseAndDispose();
    }

    private async Task<Season> AddSeason(string name, DateTime signupsOpen, DateTime signupsClose, DateTime? publicAt = null, DateTime? seasonEnd = null)
    {
        var now = DateTime.UtcNow;
        var season = new Season
        {
            SeasonName = name,
            PublicAt = publicAt ?? now.AddDays(-10),
            SignupsOpen = signupsOpen,
            SignupsClose = signupsClose,
            DraftStart = now.AddDays(5),
            SeasonStart = now.AddDays(6),
            SeasonEnd = seasonEnd ?? now.AddDays(30),
        };
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        return season;
    }

    private MessageProperties Upserted(string name) => (MessageProperties)_discordClient.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.UpsertMessageAsync))
        .Select(c => c.GetArguments()[2]!)
        .Cast<MessageProperties>()
        .Single(m => m.Content == name);

    [Test]
    public void Constructor_Throws_WhenEventsChannelMissing()
    {
        var ex = Assert.Throws<Exception>(() => new EventsService(Substitute.For<ILogger<EventsService>>(), Options.Create(new DiscordOptions()), new ContextOutbox(_context), _context, _urlService));
        Assert.That(ex!.Message, Is.EqualTo("Discord:EventsChannel is not set"));
    }

    [Test]
    public async Task Go_SignupStatuses_ProduceExpectedMessagesAndButtons()
    {
        var now = DateTime.UtcNow;
        var open = await AddSeason("Open", now.AddDays(-1), now.AddDays(1));
        await AddSeason("Future", now.AddDays(2), now.AddDays(4));
        await AddSeason("Closed", now.AddDays(-4), now.AddDays(-2));
        await AddSeason("NotPublic", now.AddDays(-1), now.AddDays(1), publicAt: now.AddDays(1));
        await AddSeason("Ended", now.AddDays(-9), now.AddDays(-8), seasonEnd: now.AddDays(-1));

        await _service.Go(CancellationToken.None);
        await _outbox.DrainAsync();

        var openMsg = Upserted("Open");
        var futureMsg = Upserted("Future");
        var closedMsg = Upserted("Closed");
        Assert.Multiple(() =>
        {
            Assert.That(_discordClient.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.UpsertMessageAsync)), Is.EqualTo(3),
                "Only public seasons that have not ended are posted");
            Assert.That(openMsg.Embeds!.Single().Description, Does.StartWith("Signups are open until <t:"));
            Assert.That(openMsg.Embeds!.Single().Description, Does.Contain(_urlService.SignupForm(open.SeasonID)));
            Assert.That(futureMsg.Embeds!.Single().Description, Does.StartWith("Signups will start at <t:").And.Contain(" and close at <t:"));
            Assert.That(closedMsg.Embeds!.Single().Description, Does.StartWith("Signups are closed.\n"));
            Assert.That(closedMsg.Embeds!.Single().Description, Does.Contain("The [draft](https://example.test/season/"));
            Assert.That(((ActionRowProperties)openMsg.Components!.Single()).Components.Count(), Is.EqualTo(2), "Open signups get a Signup button");
            Assert.That(((ActionRowProperties)futureMsg.Components!.Single()).Components.Count(), Is.EqualTo(1));
            Assert.That(((ActionRowProperties)closedMsg.Components!.Single()).Components.Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Go_SeasonWithoutPublicAt_IsNotPosted()
    {
        var now = DateTime.UtcNow;
        var season = await AddSeason("Unannounced", now.AddDays(-1), now.AddDays(1));
        season.PublicAt = null;
        await _context.SaveChangesAsync();

        await _service.Go(CancellationToken.None);
        await _outbox.DrainAsync();

        Assert.That(_discordClient.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.UpsertMessageAsync)), Is.Zero);
    }

    private int Upserts() => _discordClient.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.UpsertMessageAsync));

    [Test]
    public async Task Go_Unchanged_QueuesNothing_AndAChange_EditsTheMessageItPosted()
    {
        var now = DateTime.UtcNow;
        var sent = Substitute.For<IDiscordRestMessage>();
        sent.Id.Returns(500ul);
        _discordClient.UpsertMessageAsync(Arg.Any<ulong>(), Arg.Any<ulong?>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(sent);
        var season = await AddSeason("Season", now.AddDays(1), now.AddDays(3));

        await _service.Go(CancellationToken.None);
        await _service.Go(CancellationToken.None);
        await _outbox.DrainAsync();
        var afterTwoTicks = Upserts();

        // Signups open: the message changes, and the one posted is edited.
        season.SignupsOpen = now.AddDays(-1);
        await _context.SaveChangesAsync();
        await _service.Go(CancellationToken.None);
        await _outbox.DrainAsync();

        var calls = _discordClient.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.UpsertMessageAsync)).ToList();
        Assert.Multiple(async () =>
        {
            Assert.That(afterTwoTicks, Is.EqualTo(1), "Queued once while it reads the same");
            Assert.That(calls, Has.Count.EqualTo(2));
            Assert.That(calls[0].GetArguments()[1], Is.Null, "Posted");
            Assert.That(calls[1].GetArguments()[1], Is.EqualTo(500ul), "Then edited, by the id it kept");
            Assert.That(((ActionRowProperties)((MessageProperties)calls[1].GetArguments()[2]!).Components!.Single()).Components.Count(), Is.EqualTo(2), "Now with the Signup button");
            Assert.That((await _outbox.MessagesAsync()).Select(x => x.Status), Is.All.EqualTo(OutboxStatus.Sent));
        });
    }

    [Test]
    public async Task Go_MessageDeletedSinceItWasPosted_IsPostedAgain()
    {
        var now = DateTime.UtcNow;
        var sent = Substitute.For<IDiscordRestMessage>();
        sent.Id.Returns(500ul);
        _discordClient.UpsertMessageAsync(Arg.Any<ulong>(), null, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(sent);
        _discordClient.UpsertMessageAsync(Arg.Any<ulong>(), 500ul, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IDiscordRestMessage>(new RestException(System.Net.HttpStatusCode.NotFound, "Not Found", new RestError(10008, "Unknown Message", null))));
        var season = await AddSeason("Season", now.AddDays(1), now.AddDays(3));
        await _service.Go(CancellationToken.None);
        await _outbox.DrainAsync();

        season.SignupsOpen = now.AddDays(-1);
        await _context.SaveChangesAsync();
        await _service.Go(CancellationToken.None);
        await _outbox.DrainAsync();

        var ids = _discordClient.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.UpsertMessageAsync)).Select(c => c.GetArguments()[1]).ToList();
        Assert.That(ids, Is.EqualTo(new object?[] { null, 500ul, null }));
        Assert.That((await _outbox.MessagesAsync()).Select(x => x.Status), Is.All.EqualTo(OutboxStatus.Sent));
    }

    [Test]
    public async Task Go_LastVersionDeadLettered_IsQueuedAgain()
    {
        var now = DateTime.UtcNow;
        await AddSeason("Season", now.AddDays(1), now.AddDays(3));
        await _service.Go(CancellationToken.None);
        await _context.Outbox.ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, OutboxStatus.DeadLettered));

        await _service.Go(CancellationToken.None);

        Assert.That((await _outbox.MessagesAsync()).Select(x => x.Status), Is.EqualTo(new[] { OutboxStatus.DeadLettered, OutboxStatus.Pending }));
    }
}
