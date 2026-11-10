using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using GruntUser = Surprenant.Grunt.Models.User;
using GrifballWebApp.Infrastructure.HaloInfinite;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GetsertXboxUserServiceErrorTests
{
    private GrifballContext _context;
    private RecordingLogger<GetsertXboxUserService> _logger;
    private IHaloInfiniteClientFactory _factory;
    private GetsertXboxUserService _service;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _logger = new RecordingLogger<GetsertXboxUserService>();
        _factory = Substitute.For<IHaloInfiniteClientFactory>();
        _service = new GetsertXboxUserService(_logger, _context, _factory, TimeProvider.System);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private static HaloApiResultContainer<List<GruntUser>, HaloApiErrorContainer> Error(string message) =>
        new(null, new HaloApiErrorContainer { Message = message });

    private static HaloApiResultContainer<List<GruntUser>, HaloApiErrorContainer> Users(params (string Xuid, string Gt)[] users) =>
        new(users.Select(u => new GruntUser { xuid = u.Xuid, gamertag = u.Gt }).ToList(), null);

    [Test]
    public async Task GetsertXboxUsersByXuid_AllExisting_DoesNotCallApi()
    {
        _context.XboxUsers.AddRange(XboxUser.FromXbox(1, "a"), XboxUser.FromXbox(2, "b"));
        await _context.SaveChangesAsync();

        var result = await _service.GetsertXboxUsersByXuid([1, 2]);

        Assert.That(result.Select(x => x.Gamertag), Is.EquivalentTo(new[] { "a", "b" }));
        Assert.That(_factory.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task GetsertXboxUsersByXuid_ApiError_Throws()
    {
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(Error("rate limited"));

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUsersByXuid([5]));

        Assert.That(ex!.Message, Is.EqualTo("Failed to resolve gamertag: rate limited"));
    }

    [Test]
    public async Task GetsertXboxUsersByXuid_HalosHtmlErrorPage_IsCutToItsTitle()
    {
        // As Halo's edge answered a batch that timed out: a 504 page, passed on by the gateway
        var page = "<!DOCTYPE html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN'><html xmlns='http://www.w3.org/1999/xhtml'><head><style type='text/css'> body { font-family: Arial; } </style><title>Service unavailable</title></head><body><div id='content'></div></body></html>";
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(Error(page));

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUsersByXuid([5]));

        Assert.That(ex!.Message, Is.EqualTo("Failed to resolve gamertag: an HTML error page (Service unavailable)"));
    }

    [Test]
    public async Task GetsertXboxUsersByXuid_NoResultAndNoError_SaysSoRatherThanThrowingNull()
    {
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(new HaloApiResultContainer<List<GruntUser>, HaloApiErrorContainer>(null!, null!));

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUsersByXuid([5]));

        Assert.That(ex!.Message, Is.EqualTo("Failed to resolve gamertag: no error message"));
    }

    [Test]
    public async Task GetsertXboxUsersByXuid_ApiMissingSomeUsers_ThrowsListingThem()
    {
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(Users(("5", "five")));

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUsersByXuid([5, 6, 7]));

        Assert.That(ex!.Message, Is.EqualTo("Failed to find the following xbox users: 6,7"));
        Assert.That(await _context.XboxUsers.AnyAsync(), Is.False, "Nothing is saved when any user is missing");
    }

    [Test]
    public async Task GetsertXboxUserByXuid_ApiError_Throws()
    {
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(Error("down"));

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUserByXuid(5));

        Assert.That(ex!.Message, Is.EqualTo("Failed to resolve gamertag: down"));
    }

    [Test]
    public async Task GetsertXboxUserByXuid_NoResultAndNoError_SaysSoRatherThanThrowingNull()
    {
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(new HaloApiResultContainer<List<GruntUser>, HaloApiErrorContainer>(null!, null!));

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUserByXuid(5));

        Assert.That(ex!.Message, Is.EqualTo("Failed to resolve gamertag: no error message"));
    }

    [Test]
    public async Task GetsertXboxUserByXuid_UserNotReturned_LogsAndThrows()
    {
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(Users(("6", "someone else")));

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUserByXuid(5));

        Assert.That(ex!.Message, Is.EqualTo("Failed to find user"));
        var entry = _logger.Entries.Single();
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
        Assert.That(entry.Message, Is.EqualTo("Gamertag 5 not found"));
    }

    /// <summary>Halo gave the XUID with a blank gamertag: missing too (XboxUser.FromXbox refuses a blank one), the log naming it.</summary>
    [TestCase("")]
    [TestCase("  ")]
    public async Task GetsertXboxUserByXuid_BlankGamertag_IsMissing(string gamertag)
    {
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(Users(("5", gamertag)));

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUserByXuid(5));

        Assert.That(ex!.Message, Is.EqualTo("Failed to find user"));
        Assert.That(_logger.Entries.Single().Message, Is.EqualTo("Gamertag 5 not found"));
        Assert.That(await _context.XboxUsers.AnyAsync(), Is.False);
    }

    /// <summary>A blank gamertag in a batch is missing too: the error names its XUID, not the framework's "empty string".</summary>
    [Test]
    public async Task GetsertXboxUsersByXuid_BlankGamertag_IsListedAsMissing()
    {
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(Users(("5", "five"), ("6", " ")));

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUsersByXuid([5, 6]));

        Assert.That(ex!.Message, Is.EqualTo("Failed to find the following xbox users: 6"));
        Assert.That(await _context.XboxUsers.AnyAsync(), Is.False, "Nothing is saved when any user is missing");
    }
}
