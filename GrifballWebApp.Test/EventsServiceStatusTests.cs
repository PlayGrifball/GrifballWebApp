using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class EventsServiceStatusTests
{
    private GrifballContext _context;
    private IDiscordRestClient _discordClient;
    private EventsService _service;
    private UrlService _urlService;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _discordClient = Substitute.For<IDiscordRestClient>();
        _discordClient.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://example.test" }).Build();
        _urlService = new UrlService(config);
        _service = new EventsService(Substitute.For<ILogger<EventsService>>(), Options.Create(new DiscordOptions { EventsChannel = 66 }), _discordClient, _context, _urlService);
    }

    [TearDown]
    public async Task TearDown()
    {
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
        var ex = Assert.Throws<Exception>(() => new EventsService(Substitute.For<ILogger<EventsService>>(), Options.Create(new DiscordOptions()), _discordClient, _context, _urlService));
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
            Assert.That(((ActionRowProperties)openMsg.Components!.Single()).Buttons.Count(), Is.EqualTo(2), "Open signups get a Signup button");
            Assert.That(((ActionRowProperties)futureMsg.Components!.Single()).Buttons.Count(), Is.EqualTo(1));
            Assert.That(((ActionRowProperties)closedMsg.Components!.Single()).Buttons.Count(), Is.EqualTo(1));
        });
    }
}
