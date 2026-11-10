using DiscordInterface.Generated;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using NSubstitute;
using Quartz;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Infrastructure.Events;
using GrifballWebApp.Infrastructure.Matchmaking;
using static GrifballWebApp.Infrastructure.Test.DiscordJobsTestData;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The Discord jobs Quartz runs every 10 seconds (the matchmaking queue, the season announcements): their schedule, off
/// while Discord is, and a run against the database.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordJobsTests
{
    private GrifballContext _context;
    private IDiscordRestClient _discordClient;

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
        _discordClient = Substitute.For<IDiscordRestClient>();
        _discordClient.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task TheMatchmakingJob_RunsTheQueue()
    {
        var me = Substitute.For<IDiscordCurrentUser>();
        _discordClient.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(me);
        _discordClient.SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(_ => Substitute.For<IDiscordRestMessage>());
        var queueService = new QueueService(Substitute.For<ILogger<QueueService>>(), Options.Create(new DiscordOptions { QueueChannel = 55 }),
            new QueueRepository(_context), _discordClient, _context, Substitute.For<IDataPullService>(), new ContextOutbox(_context), Substitute.For<IQueueNotifier>());

        await new MatchmakingQueueJob(queueService, Failures()).Execute(Substitute.For<IJobExecutionContext>());

        await _discordClient.Received(1).SendMessageAsync(55ul, Arg.Is<MessageProperties>(m => m.Content == "Late League Queue"), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TheSeasonEventsJob_QueuesTheAnnouncement()
    {
        var now = DateTime.UtcNow;
        _context.Seasons.Add(Season.Seed(seasonName: "BG Season").WithDates(publicAt: now.AddDays(-1), signupsOpen: now.AddDays(-1), signupsClose: now.AddDays(1), draftStart: now.AddDays(2), seasonStart: now.AddDays(3), seasonEnd: now.AddDays(30)));
        await _context.SaveChangesAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://example.test" }).Build();
        var eventsService = new EventsService(Substitute.For<ILogger<EventsService>>(), Options.Create(new DiscordOptions { EventsChannel = 66 }),
            new ContextOutbox(_context), _context, new UrlService(config));

        await new SeasonEventsJob(eventsService, Failures()).Execute(Substitute.For<IJobExecutionContext>());

        // The announcement is queued on the outbox, which sends it.
        await using var check = _context.NewContextLike();
        var queued = await check.Outbox.SingleOrDefaultAsync();
        Assert.That(queued, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(queued!.Kind, Is.EqualTo(EventsOutbox.SeasonUpsert));
            Assert.That(queued.Payload, Does.Contain("\"seasonName\":\"BG Season\""));
        }
    }
}
