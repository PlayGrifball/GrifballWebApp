using GrifballWebApp.Application.Historical;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Historical;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using GruntUser = Surprenant.Grunt.Models.User;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// XuidLookupService, on a real (temp-file SQLite) GrifballContext with a substituted IHaloInfiniteClient: no
/// database server, host or network.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class XuidLookupServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private string _path = null!;
    private GrifballContext _context = null!;
    private IHaloInfiniteClient _halo = null!;
    private FakeTimeProvider _time = null!;
    private XuidLookupOptions _options = null!;

    [SetUp]
    public async Task SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), $"grif-xuid-lookup-{Guid.NewGuid():N}.db");
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
        _halo = Substitute.For<IHaloInfiniteClient>();
        _time = new FakeTimeProvider(Now);
        _options = new XuidLookupOptions { BatchSize = 10, LookupsPerMinute = 6000 };
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
        if (File.Exists(_path))
            File.Delete(_path);
    }

    private XuidLookupService Service() => new(_context, _halo, new GamertagMatching(_context), Options.Create(_options), _time)
    {
        Delay = (_, _) => Task.CompletedTask,
    };

    private async Task<User> AddPlaceholder(string gamertag)
    {
        var user = User.ImportPlaceholder($"historical:{gamertag}", gamertag, null);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private static GruntUser GamertagResult(string xuid, string gamertag) => new() { xuid = xuid, gamertag = gamertag };

    [Test]
    public async Task RunBatchAsync_NoPlaceholders_LooksUpNothing()
    {
        var report = await Service().RunBatchAsync();

        Assert.That(report.LookedUp, Is.Zero);
    }

    [Test]
    public async Task RunBatchAsync_PlaceholderFound_RecordsItAndMakesAClaim()
    {
        var placeholder = await AddPlaceholder("Grif");
        _halo.UserByGamertag("Grif").Returns(new HaloApiResultContainer<GruntUser, HaloApiErrorContainer>(GamertagResult("42", "Grif"), null!));

        var report = await Service().RunBatchAsync();

        var lookup = await _context.XuidLookups.SingleAsync();
        var claims = await _context.GamertagClaims.ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.LookedUp, Is.EqualTo(1));
            Assert.That(report.Found, Is.EqualTo(1));
            Assert.That(report.ClaimsCreated, Is.EqualTo(1));
            Assert.That(lookup.UserID, Is.EqualTo(placeholder.Id));
            Assert.That(lookup.Status, Is.EqualTo(XuidLookupStatus.Found));
            Assert.That(lookup.XboxUserID, Is.EqualTo(42));
            Assert.That(claims.Single().PlaceholderUserID, Is.EqualTo(placeholder.Id));
        }
        Assert.That(await _context.XboxUsers.AnyAsync(x => x.XboxUserID == 42), Is.True, "The Xbox account is stored since it wasn't known");
    }

    [Test]
    public async Task RunBatchAsync_HaloHasNoAnswer_NotFound()
    {
        await AddPlaceholder("Grif");
        _halo.UserByGamertag("Grif").Returns(new HaloApiResultContainer<GruntUser, HaloApiErrorContainer>(GamertagResult("1", ""), null!));

        var report = await Service().RunBatchAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.NotFound, Is.EqualTo(1));
            Assert.That((await _context.XuidLookups.SingleAsync()).Status, Is.EqualTo(XuidLookupStatus.NotFound));
        }
    }

    [Test]
    public async Task RunBatchAsync_HaloGivesAnUnparseableXuid_Fails()
    {
        await AddPlaceholder("Grif");
        _halo.UserByGamertag("Grif").Returns(new HaloApiResultContainer<GruntUser, HaloApiErrorContainer>(GamertagResult("not-a-number", "Grif"), null!));

        var report = await Service().RunBatchAsync();

        var lookup = await _context.XuidLookups.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Failed, Is.EqualTo(1));
            Assert.That(lookup.Status, Is.EqualTo(XuidLookupStatus.Failed));
            Assert.That(lookup.Error, Is.EqualTo("The XUID Xbox gave isn't a number"));
        }
    }

    [Test]
    public async Task RunBatchAsync_HaloThrows_FailsWithTheExceptionsMessage_Truncated()
    {
        await AddPlaceholder("Grif");
        _halo.UserByGamertag("Grif").ThrowsAsync(new Exception(new string('x', 300)));

        var report = await Service().RunBatchAsync();

        var lookup = await _context.XuidLookups.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Failed, Is.EqualTo(1));
            Assert.That(lookup.Error, Has.Length.EqualTo(200));
        }
    }

    [Test]
    public async Task RunBatchAsync_LookedUpRecently_IsNotDueAgain()
    {
        await AddPlaceholder("Grif");
        _halo.UserByGamertag("Grif").Returns(new HaloApiResultContainer<GruntUser, HaloApiErrorContainer>(GamertagResult("1", ""), null!));
        await Service().RunBatchAsync();
        _context.ChangeTracker.Clear();

        var report = await Service().RunBatchAsync();

        Assert.That(report.LookedUp, Is.Zero);
    }

    [Test]
    public async Task RunBatchAsync_FailedPastTheRetryWindow_IsDueAgain()
    {
        await AddPlaceholder("Grif");
        _halo.UserByGamertag("Grif").ThrowsAsync(new Exception("down"));
        await Service().RunBatchAsync();
        _context.ChangeTracker.Clear();
        _time.Advance(_options.RetryFailedAfter + TimeSpan.FromMinutes(1));
        _halo.UserByGamertag("Grif").Returns(new HaloApiResultContainer<GruntUser, HaloApiErrorContainer>(GamertagResult("42", "Grif"), null!));

        var report = await Service().RunBatchAsync();

        Assert.That(report.Found, Is.EqualTo(1));
    }

    [Test]
    public async Task LookUpAgainAsync_DeletesEverythingButFoundLookups()
    {
        var a = await AddPlaceholder("Found");
        var b = await AddPlaceholder("NotFound");
        _context.XuidLookups.AddRange(
            new XuidLookup { UserID = a.Id, Status = XuidLookupStatus.Found, LookedUpAt = Now.UtcDateTime },
            new XuidLookup { UserID = b.Id, Status = XuidLookupStatus.NotFound, LookedUpAt = Now.UtcDateTime });
        await _context.SaveChangesAsync();

        var deleted = await Service().LookUpAgainAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deleted, Is.EqualTo(1));
            Assert.That((await _context.XuidLookups.SingleAsync()).UserID, Is.EqualTo(a.Id));
        }
    }

    [Test]
    public async Task StatusAsync_CountsPlaceholdersAndLookupsByOutcome()
    {
        var found = await AddPlaceholder("Found");
        await AddPlaceholder("Due");
        _context.XuidLookups.Add(new XuidLookup { UserID = found.Id, Status = XuidLookupStatus.Found, LookedUpAt = Now.UtcDateTime });
        await _context.SaveChangesAsync();

        var status = await Service().StatusAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(status.Placeholders, Is.EqualTo(2));
            Assert.That(status.Found, Is.EqualTo(1));
            Assert.That(status.Due, Is.EqualTo(1), "The due one has no lookup yet");
            Assert.That(status.LastLookedUpAt, Is.EqualTo(Now.UtcDateTime));
        }
    }
}
