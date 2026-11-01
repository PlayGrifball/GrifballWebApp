using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using HaloUser = Surprenant.Grunt.Models.User;
using User = GrifballWebApp.Database.Models.User;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class XuidLookupServiceTests
{
    private GrifballContext _context;
    private IHaloInfiniteClient _halo;
    private FakeTimeProvider _time;
    private XuidLookupOptions _options;
    private readonly List<TimeSpan> _delays = [];

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _halo = Substitute.For<IHaloInfiniteClient>();
        _halo.UserByGamertag(Arg.Any<string>()).Returns(Task.FromResult<HaloApiResultContainer<HaloUser, HaloApiErrorContainer>>(null!));
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));
        _options = new XuidLookupOptions { BatchSize = 50, LookupsPerMinute = 120 };
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private XuidLookupService Service()
    {
        var service = new XuidLookupService(_context, _halo, new GamertagMatching(_context), Options.Create(_options), _time);
        service.Delay = (delay, _) => { _delays.Add(delay); return Task.CompletedTask; };
        return service;
    }

    private async Task<User> Placeholder(string gamertag)
    {
        var user = new User
        {
            UserName = HistoricalKeys.DummyUserNamePrefix + gamertag,
            NormalizedUserName = (HistoricalKeys.DummyUserNamePrefix + gamertag).ToUpperInvariant(),
            DisplayName = gamertag,
            IsDummyUser = true,
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private void Holds(string gamertag, string xuid, string? current = null)
        => _halo.UserByGamertag(gamertag).Returns(Task.FromResult(
            new HaloApiResultContainer<HaloUser, HaloApiErrorContainer>(new HaloUser { gamertag = current ?? gamertag, xuid = xuid }, null)));

    [Test]
    public async Task Found_CreatesOnePendingClaim_WithTheCurrentHolder_AndNeverLinks()
    {
        var placeholder = await Placeholder("Old Tag");
        Holds("Old Tag", "456", current: "OLD TAG");

        var report = await Service().RunBatchAsync();

        _context.ChangeTracker.Clear();
        var claim = await _context.GamertagClaims.SingleAsync();
        Assert.Multiple(async () =>
        {
            Assert.That((report.LookedUp, report.Found, report.ClaimsCreated), Is.EqualTo((1, 1, 1)));
            Assert.That((claim.Kind, claim.Status, claim.XboxUserID, claim.Gamertag, claim.CurrentGamertag, claim.PlaceholderUserID),
                Is.EqualTo((GamertagClaimKind.XuidLookup, GamertagClaimStatus.Pending, 456L, "Old Tag", "OLD TAG", (int?)placeholder.Id)));
            Assert.That(claim.LookedUpAt, Is.EqualTo(_time.GetUtcNow().UtcDateTime));
            Assert.That(await _context.Users.Where(u => u.Id == placeholder.Id).Select(u => u.XboxUserID).SingleAsync(), Is.Null, "Never linked automatically");
            Assert.That(await _context.XboxUsers.Where(x => x.XboxUserID == 456).Select(x => x.Gamertag).SingleAsync(), Is.EqualTo("OLD TAG"));
            var lookup = await _context.XuidLookups.SingleAsync();
            Assert.That((lookup.UserID, lookup.Status, lookup.XboxUserID), Is.EqualTo((placeholder.Id, XuidLookupStatus.Found, (long?)456)));
        });
    }

    [Test]
    public async Task NotFound_CreatesNoClaim_AndRecordsTheLookup()
    {
        var placeholder = await Placeholder("Gone Tag");

        var report = await Service().RunBatchAsync();

        Assert.Multiple(async () =>
        {
            Assert.That((report.LookedUp, report.NotFound, report.ClaimsCreated), Is.EqualTo((1, 1, 0)));
            Assert.That(await _context.GamertagClaims.AnyAsync(), Is.False);
            var lookup = await _context.XuidLookups.SingleAsync();
            Assert.That((lookup.UserID, lookup.Status), Is.EqualTo((placeholder.Id, XuidLookupStatus.NotFound)));
        });
    }

    [Test]
    public async Task RunningAgain_DoesNotDuplicate_EvenAfterTheWindow()
    {
        await Placeholder("Old Tag");
        Holds("Old Tag", "456");

        await Service().RunBatchAsync();
        var soon = await Service().RunBatchAsync();
        _time.Advance(_options.LookAgainAfter + TimeSpan.FromMinutes(1));
        var later = await Service().RunBatchAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(soon.LookedUp, Is.Zero, "Looked up recently: skipped");
            Assert.That((later.LookedUp, later.ClaimsCreated), Is.EqualTo((1, 0)), "Due again, but the pair already has a claim");
            Assert.That(await _context.GamertagClaims.CountAsync(), Is.EqualTo(1));
            Assert.That(await _context.XuidLookups.CountAsync(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Failed_IsRetriedAfterTheRetryWindow()
    {
        await Placeholder("Flaky Tag");
        _halo.UserByGamertag("Flaky Tag").ThrowsAsync(new HttpRequestException("429 Too Many Requests"));

        var first = await Service().RunBatchAsync();
        var soon = await Service().RunBatchAsync();
        Holds("Flaky Tag", "789");
        _time.Advance(_options.RetryFailedAfter + TimeSpan.FromMinutes(1));
        var retry = await Service().RunBatchAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(first.Failed, Is.EqualTo(1));
            Assert.That(soon.LookedUp, Is.Zero);
            Assert.That((retry.Found, retry.ClaimsCreated), Is.EqualTo((1, 1)));
            Assert.That(await _context.XuidLookups.Select(l => l.Status).SingleAsync(), Is.EqualTo(XuidLookupStatus.Found));
        });
    }

    [Test]
    public async Task AnXuidThatIsNotANumber_IsAFailedLookup_AndNoClaim()
    {
        await Placeholder("Odd Tag");
        Holds("Odd Tag", "not-a-number");

        var report = await Service().RunBatchAsync();

        var lookup = await _context.XuidLookups.AsNoTracking().Include(l => l.User).SingleAsync();
        Assert.Multiple(async () =>
        {
            Assert.That((report.Failed, report.ClaimsCreated), Is.EqualTo((1, 0)));
            Assert.That((lookup.Status, lookup.Error), Is.EqualTo((XuidLookupStatus.Failed, "The XUID Xbox gave isn't a number")));
            Assert.That(lookup.User.IsDummyUser, Is.True);
            Assert.That(await _context.GamertagClaims.AnyAsync(), Is.False);
        });
    }

    [Test]
    public async Task OnlyImportPlaceholdersWithoutAnXuid_AreLookedUp()
    {
        await Placeholder("Old Tag");
        var linked = await Placeholder("Linked Tag");
        linked.XboxUser = new XboxUser { XboxUserID = 900, Gamertag = "Linked Tag" };
        _context.Users.Add(new User { UserName = "realuser", NormalizedUserName = "REALUSER" });
        await _context.SaveChangesAsync();

        var report = await Service().RunBatchAsync();

        Assert.That(report.LookedUp, Is.EqualTo(1));
        await _halo.Received(1).UserByGamertag("Old Tag");
        await _halo.DidNotReceive().UserByGamertag("Linked Tag");
    }

    [Test]
    public async Task Batches_RespectTheBatchSize_AndThrottleBetweenLookups()
    {
        for (var i = 0; i < 5; i++)
            await Placeholder($"Tag {i}");
        _options.BatchSize = 2;
        _options.LookupsPerMinute = 60;

        var first = await Service().RunBatchAsync();

        Assert.Multiple(() =>
        {
            Assert.That(first.LookedUp, Is.EqualTo(2));
            Assert.That(_delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(1) }), "One gap between two lookups, 60 a minute");
        });

        var services = new ServiceCollection();
        var connectionString = _context.Database.GetConnectionString()!;
        services.AddScoped(_ => new GrifballContext(TestDatabase.Options(connectionString).Options));
        services.AddScoped(sp =>
        {
            var context = sp.GetRequiredService<GrifballContext>();
            var service = new XuidLookupService(context, _halo, new GamertagMatching(context), Options.Create(_options), _time);
            service.Delay = (delay, _) => { _delays.Add(delay); return Task.CompletedTask; };
            return service;
        });
        var worker = new XuidLookupWorker(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), new XuidLookupSignal(),
            Options.Create(_options), NullLogger<XuidLookupWorker>.Instance);

        var rest = await worker.RunUntilDoneAsync(CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(rest, Is.EqualTo(3), "Batches of 2 until none is due: 2, then 1");
            Assert.That(await _context.XuidLookups.CountAsync(), Is.EqualTo(5));
            await _halo.Received(5).UserByGamertag(Arg.Any<string>());
        });
    }

    [Test]
    public async Task LookUpAgain_MakesTheNotFoundDue_AndKeepsTheFound()
    {
        await Placeholder("Old Tag");
        await Placeholder("Gone Tag");
        Holds("Old Tag", "456");
        await Service().RunBatchAsync();

        var cleared = await Service().LookUpAgainAsync();
        _context.ChangeTracker.Clear();
        var status = await Service().StatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cleared, Is.EqualTo(1));
            Assert.That((status.Placeholders, status.Due, status.Found, status.NotFound), Is.EqualTo((2, 1, 1, 0)));
        });
    }

    [Test]
    public async Task Approval_LinksThePlaceholder_WhenNoUserHasTheXuid()
    {
        var placeholder = await Placeholder("Old Tag");
        Holds("Old Tag", "456");
        await Service().RunBatchAsync();
        var claim = await _context.GamertagClaims.SingleAsync();
        _context.ChangeTracker.Clear();

        await new GamertagClaimService(_context, new UserMergeService(_context)).Approve(claim.GamertagClaimID);

        _context.ChangeTracker.Clear();
        Assert.Multiple(async () =>
        {
            Assert.That(await _context.Users.Where(u => u.Id == placeholder.Id).Select(u => u.XboxUserID).SingleAsync(), Is.EqualTo(456));
            Assert.That(await _context.XboxGamertagHistory.AnyAsync(), Is.False, "The gamertag is the account's current one, not a past one");
        });
    }

    [Test]
    public async Task Approval_MergesThePlaceholder_IntoTheXuidsUser()
    {
        var owner = new User { UserName = "owner", NormalizedUserName = "OWNER", XboxUser = new XboxUser { XboxUserID = 456, Gamertag = "Old Tag" } };
        _context.Users.Add(owner);
        await _context.SaveChangesAsync();
        var placeholder = await Placeholder("Old Tag");
        Holds("Old Tag", "456");
        await Service().RunBatchAsync();
        var claim = await _context.GamertagClaims.SingleAsync();
        _context.ChangeTracker.Clear();

        await new GamertagClaimService(_context, new UserMergeService(_context)).Approve(claim.GamertagClaimID);

        _context.ChangeTracker.Clear();
        Assert.That(await _context.Users.AnyAsync(u => u.Id == placeholder.Id), Is.False);
    }

    [Test]
    public async Task Rejection_SuppressesThePair()
    {
        await Placeholder("Old Tag");
        Holds("Old Tag", "456");
        await Service().RunBatchAsync();
        var claim = await _context.GamertagClaims.SingleAsync();
        _context.ChangeTracker.Clear();
        await new GamertagClaimService(_context, new UserMergeService(_context)).Reject(claim.GamertagClaimID);
        _context.ChangeTracker.Clear();

        await Service().LookUpAgainAsync();
        _time.Advance(_options.LookAgainAfter + TimeSpan.FromMinutes(1));
        var again = await Service().RunBatchAsync();

        Assert.Multiple(async () =>
        {
            Assert.That((again.Found, again.ClaimsCreated), Is.EqualTo((1, 0)));
            Assert.That(await _context.GamertagClaims.Select(c => c.Status).ToListAsync(), Is.EqualTo(new[] { GamertagClaimStatus.Rejected }));
        });
    }
}
