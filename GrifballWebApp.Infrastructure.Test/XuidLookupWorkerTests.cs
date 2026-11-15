using Microsoft.Extensions.Logging.Testing;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Infrastructure.Historical;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using GruntUser = Surprenant.Grunt.Models.User;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// XuidLookupWorker.RunUntilDoneAsync: batch after batch of placeholders looked up, each in a scope of its own, until a
/// batch comes back short; between two lookups of a batch, the throttle's wait. On a real (temp-file SQLite)
/// GrifballContext behind a small DI container, with Halo substituted and the throttle recorded instead of waited.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class XuidLookupWorkerTests : SqliteContextFixture
{
    private readonly IHaloInfiniteClient _halo = Substitute.For<IHaloInfiniteClient>();
    private readonly List<TimeSpan> _waits = [];
    private ServiceProvider _provider = null!;
    private readonly FakeLogger<XuidLookupWorker> _log = new();
    private static readonly string[] BatchLines = ["XUID lookups: 2 found, 0 not found, 0 failed, 2 claims", "XUID lookups: 1 found, 0 not found, 0 failed, 1 claims"];
    private static readonly string[] SkippedThenFoundLines = ["XUID lookups: 2 names that can't be gamertags skipped", "XUID lookups: 1 found, 0 not found, 0 failed, 1 claims"];

    private XuidLookupWorker Worker(int batchSize, int lookupsPerMinute = 6000, bool enabled = true)
    {
        var options = Options.Create(new XuidLookupOptions { BatchSize = batchSize, LookupsPerMinute = lookupsPerMinute, Enabled = enabled });
        var services = new ServiceCollection();
        services.AddDbContext<GrifballContext>(o => o.UseGrifballDatabase(DatabaseProvider.Sqlite, ConnectionString));
        services.AddScoped(sp => new XuidLookupService(sp.GetRequiredService<GrifballContext>(), _halo, new GamertagMatching(sp.GetRequiredService<GrifballContext>()),
            options, new FakeTimeProvider(new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero)))
        {
            Delay = (wait, _) =>
            {
                lock (_waits)
                    _waits.Add(wait);
                return Task.CompletedTask;
            },
        });
        _provider = services.BuildServiceProvider();
        return new XuidLookupWorker(_provider.GetRequiredService<IServiceScopeFactory>(), new XuidLookupSignal(), options, _log);
    }

    [TearDown]
    public async Task DisposeProvider()
    {
        if (_provider is not null)
            await _provider.DisposeAsync();
    }

    private async Task AddPlaceholdersAsync(params string[] gamertags)
    {
        foreach (var gamertag in gamertags)
        {
            Context.Users.Add(User.ImportPlaceholder($"historical:{gamertag}", gamertag, null));
            _halo.UserByGamertag(gamertag).Returns(new HaloApiResultContainer<GruntUser, HaloApiErrorContainer>(
                new GruntUser { xuid = $"{100 + gamertags.ToList().IndexOf(gamertag)}", gamertag = gamertag }, null!));
        }
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task RunUntilDone_BatchAfterBatch_UntilNoneIsDue()
    {
        await AddPlaceholdersAsync("Alpha", "Bravo", "Charlie");
        using var worker = Worker(batchSize: 2);

        var looked = await worker.RunUntilDoneAsync(CancellationToken.None);

        Context.ChangeTracker.Clear();
        var lookups = await Context.XuidLookups.AsNoTracking().Select(l => l.Status).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(looked, Is.EqualTo(3));
            Assert.That(lookups, Has.Count.EqualTo(3).And.All.EqualTo(XuidLookupStatus.Found));
            // Two batches (2, then 1): one wait between the first batch's two lookups
            Assert.That(_waits, Is.EqualTo(new[] { TimeSpan.FromMinutes(1) / 6000 }));
            Assert.That(_log.At(Microsoft.Extensions.Logging.LogLevel.Information),
                Is.EqualTo(BatchLines), "a line a batch");
        }
        await _halo.Received(3).UserByGamertag(Arg.Any<string>());
    }

    [Test]
    public async Task RunUntilDone_Disabled_LooksUpNothing_AndLeavesThemDue()
    {
        await AddPlaceholdersAsync("Alpha", "Bravo");
        using var worker = Worker(batchSize: 2, enabled: false);

        var looked = await worker.RunUntilDoneAsync(CancellationToken.None);

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(looked, Is.Zero);
            Assert.That(await Context.XuidLookups.AnyAsync(), Is.False, "nothing recorded: still due when enabled again");
        }
        await _halo.DidNotReceiveWithAnyArgs().UserByGamertag(default!);
    }

    [Test]
    public async Task RunUntilDone_Throttled_StopsAtOnce_AndPausesForTheRetryAfter()
    {
        await AddPlaceholdersAsync("Alpha", "Bravo", "Charlie");
        _halo.UserByGamertag("Bravo").Returns(new HaloApiResultContainer<GruntUser, HaloApiErrorContainer>(null!,
            new HaloApiErrorContainer { Code = 429, RetryAfter = TimeSpan.FromMinutes(5) }));
        using var worker = Worker(batchSize: 2);

        var looked = await worker.RunUntilDoneAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(looked, Is.EqualTo(1), "Alpha; Bravo was turned away and nothing after it asked");
            Assert.That(worker.PausedFor, Is.EqualTo(TimeSpan.FromMinutes(5)));
        }
        await _halo.DidNotReceive().UserByGamertag("Charlie");
    }

    [Test]
    public async Task RunUntilDone_NotThrottled_DoesNotPause()
    {
        await AddPlaceholdersAsync("Alpha");
        using var worker = Worker(batchSize: 2);

        await worker.RunUntilDoneAsync(CancellationToken.None);

        Assert.That(worker.PausedFor, Is.Null);
    }

    [Test]
    public async Task RunUntilDone_NothingDue_LooksUpNothing()
    {
        using var worker = Worker(batchSize: 2);

        Assert.That(await worker.RunUntilDoneAsync(CancellationToken.None), Is.Zero);
        await _halo.DidNotReceiveWithAnyArgs().UserByGamertag(default!);
    }

    [Test]
    public async Task RunUntilDone_AFullLastBatch_LooksOnceMore_ThenStops()
    {
        await AddPlaceholdersAsync("Alpha", "Bravo");
        using var worker = Worker(batchSize: 1, lookupsPerMinute: 60);

        var looked = await worker.RunUntilDoneAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(looked, Is.EqualTo(2));
            Assert.That(_waits, Is.Empty, "one lookup a batch: nothing to wait between");
        }
    }

    [Test]
    public async Task RunUntilDone_ABatchOfNamesThatAreNotGamertags_GoesOnToTheNextBatch()
    {
        // Ordered by id: the first batch is both names that can't be gamertags, the real one comes after
        await AddPlaceholdersAsync("Bowdery(1)(G)", "Blubun(G)", "Alpha");
        using var worker = Worker(batchSize: 2);

        var looked = await worker.RunUntilDoneAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(looked, Is.EqualTo(1), "only Alpha was asked about");
            Assert.That(_log.At(Microsoft.Extensions.Logging.LogLevel.Information), Is.EqualTo(SkippedThenFoundLines));
        }
        await _halo.Received(1).UserByGamertag("Alpha");
        await _halo.DidNotReceive().UserByGamertag("Bowdery(1)(G)");
    }
}
