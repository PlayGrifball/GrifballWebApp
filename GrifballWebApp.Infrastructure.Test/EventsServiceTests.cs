using GrifballWebApp.Application;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Events;
using GrifballWebApp.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// EventsService, on a real (temp-file SQLite) GrifballContext with its real ContextOutbox (so a season's queued
/// announcement is read back exactly as it was written): no database server, host or network.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class EventsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private const ulong EventsChannel = 555UL;

    private string _path = null!;
    private GrifballContext _context = null!;
    private FakeTimeProvider _time = null!;
    private EventsService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), $"grif-events-{Guid.NewGuid():N}.db");
        _context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={_path};Pooling=False").Options);
        try
        {
            await _context.Database.EnsureCreatedAsync();
        }
        catch
        {
            await _context.DisposeAsync();
            if (File.Exists(_path))
                File.Delete(_path);
            throw;
        }
        _time = new FakeTimeProvider(Now);
        var urls = new UrlService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());
        var discordOptions = Options.Create(new DiscordOptions { EventsChannel = EventsChannel });
        _service = new EventsService(NullLogger<EventsService>.Instance, discordOptions, new ContextOutbox(_context), _context, urls, _time);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
        if (File.Exists(_path))
            File.Delete(_path);
    }

    [Test]
    public void Constructor_NoEventsChannelConfigured_Throws()
    {
        var urls = new UrlService(new ConfigurationBuilder().Build());
        Assert.That(() => new EventsService(NullLogger<EventsService>.Instance, Options.Create(new DiscordOptions()), new ContextOutbox(_context), _context, urls, _time),
            Throws.Exception.With.Message.Contains("Discord:EventsChannel"));
    }

    private Season AddSeason(string name, DateTime? publicAt, DateTime signupsOpen, DateTime signupsClose, DateTime draftStart, DateTime seasonStart, DateTime seasonEnd)
    {
        var season = Season.Seed(seasonName: name);
        var scheduled = season.Schedule(signupsOpen, signupsClose, draftStart, seasonStart, seasonEnd);
        season.AnnounceAt(publicAt);
        _context.Seasons.Add(season);
        Assert.That(scheduled, Is.EqualTo(SeasonScheduleResult.Scheduled), "The test's own dates must be in order");
        return season;
    }

    private Season AddUpcomingAnnouncedSeason(string name = "S1") => AddSeason(name,
        publicAt: Now.UtcDateTime.AddDays(-1),
        signupsOpen: Now.UtcDateTime.AddDays(1), signupsClose: Now.UtcDateTime.AddDays(10),
        draftStart: Now.UtcDateTime.AddDays(11), seasonStart: Now.UtcDateTime.AddDays(12), seasonEnd: Now.UtcDateTime.AddDays(100));

    [Test]
    public async Task Go_AnnouncedUnfinishedSeason_QueuesTheAnnouncement()
    {
        var season = AddUpcomingAnnouncedSeason();
        await _context.SaveChangesAsync();

        await _service.Go(CancellationToken.None);

        var message = await _context.Outbox.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Kind, Is.EqualTo(EventsOutbox.SeasonUpsert));
            Assert.That(message.GroupKey, Is.EqualTo(EventsOutbox.Group(season.SeasonID)));
        }
    }

    [Test]
    public async Task Go_NotYetPublic_QueuesNothing()
    {
        AddSeason("S1", publicAt: null,
            signupsOpen: Now.UtcDateTime.AddDays(1), signupsClose: Now.UtcDateTime.AddDays(10),
            draftStart: Now.UtcDateTime.AddDays(11), seasonStart: Now.UtcDateTime.AddDays(12), seasonEnd: Now.UtcDateTime.AddDays(100));
        await _context.SaveChangesAsync();

        await _service.Go(CancellationToken.None);

        Assert.That(await _context.Outbox.AnyAsync(), Is.False);
    }

    [Test]
    public async Task Go_AlreadyEnded_QueuesNothing()
    {
        AddSeason("S1", publicAt: Now.UtcDateTime.AddDays(-100),
            signupsOpen: Now.UtcDateTime.AddDays(-99), signupsClose: Now.UtcDateTime.AddDays(-90),
            draftStart: Now.UtcDateTime.AddDays(-89), seasonStart: Now.UtcDateTime.AddDays(-88), seasonEnd: Now.UtcDateTime.AddDays(-1));
        await _context.SaveChangesAsync();

        await _service.Go(CancellationToken.None);

        Assert.That(await _context.Outbox.AnyAsync(), Is.False);
    }

    [Test]
    public async Task Go_CalledAgain_SameAnnouncement_QueuesNothingMore()
    {
        AddUpcomingAnnouncedSeason();
        await _context.SaveChangesAsync();
        await _service.Go(CancellationToken.None);

        await _service.Go(CancellationToken.None);

        Assert.That(await _context.Outbox.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Go_CalledAgain_AnnouncementChanged_QueuesAnother()
    {
        var season = AddUpcomingAnnouncedSeason();
        await _context.SaveChangesAsync();
        await _service.Go(CancellationToken.None);
        // Signups now open: the announcement's signup status (and so its payload) changes.
        _time.SetUtcNow(season.SignupsOpen.Add(TimeSpan.FromMinutes(1)));

        await _service.Go(CancellationToken.None);

        var messages = await _context.Outbox.OrderBy(x => x.Sequence).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(messages, Has.Count.EqualTo(2));
            Assert.That(messages[1].Sequence, Is.EqualTo(1));
            Assert.That(messages[0].Payload, Is.Not.EqualTo(messages[1].Payload));
        }
    }

    [Test]
    public async Task Go_CalledAgain_LastWasDeadLettered_QueuesAnotherEvenUnchanged()
    {
        AddUpcomingAnnouncedSeason();
        await _context.SaveChangesAsync();
        await _service.Go(CancellationToken.None);
        var first = await _context.Outbox.SingleAsync();
        first.Status = OutboxStatus.DeadLettered;
        await _context.SaveChangesAsync();

        await _service.Go(CancellationToken.None);

        Assert.That(await _context.Outbox.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task Go_SignupsNotYetOpen_AnnouncementSaysWhenTheyStart()
    {
        AddUpcomingAnnouncedSeason();
        await _context.SaveChangesAsync();

        await _service.Go(CancellationToken.None);

        var message = await _context.Outbox.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Payload, Does.Contain("\"signupsOpen\":false"));
            Assert.That(message.Payload, Does.Contain("S1"));
        }
    }

    [Test]
    public async Task Go_SignupsOpen_AnnouncementSaysSoAndLinksToSignUp()
    {
        var season = AddUpcomingAnnouncedSeason();
        await _context.SaveChangesAsync();
        _time.SetUtcNow(season.SignupsOpen.Add(TimeSpan.FromMinutes(1)));

        await _service.Go(CancellationToken.None);

        var message = await _context.Outbox.SingleAsync();
        Assert.That(message.Payload, Does.Contain("\"signupsOpen\":true"));
    }

    [Test]
    public async Task Go_SignupsClosed_AnnouncementSaysSo()
    {
        var season = AddUpcomingAnnouncedSeason();
        await _context.SaveChangesAsync();
        _time.SetUtcNow(season.SignupsClose.Add(TimeSpan.FromMinutes(1)));

        await _service.Go(CancellationToken.None);

        var message = await _context.Outbox.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Payload, Does.Contain("\"signupsOpen\":false"));
            Assert.That(message.Payload, Does.Contain("Signups are closed"));
        }
    }
}
