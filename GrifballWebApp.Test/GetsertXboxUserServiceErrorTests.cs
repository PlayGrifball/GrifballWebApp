using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using GruntUser = Surprenant.Grunt.Models.User;
using User = GrifballWebApp.Database.Models.User;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
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
        _service = new GetsertXboxUserService(_logger, _context, _factory);
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
        _context.XboxUsers.AddRange(new XboxUser { XboxUserID = 1, Gamertag = "a" }, new XboxUser { XboxUserID = 2, Gamertag = "b" });
        await _context.SaveChangesAsync();

        var result = await _service.GetsertXboxUsersByXuid([1, 2]);

        Assert.That(result.Select(x => x.Gamertag), Is.EquivalentTo(new[] { "a", "b" }));
        Assert.That(_factory.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public void GetsertXboxUsersByXuid_ApiError_Throws()
    {
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(Error("rate limited"));

        var ex = Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUsersByXuid([5]));

        Assert.That(ex!.Message, Is.EqualTo("Failed to resolved gamertag: rate limited"));
    }

    [Test]
    public async Task GetsertXboxUsersByXuid_ApiMissingSomeUsers_ThrowsListingThem()
    {
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(Users(("5", "five")));

        var ex = Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUsersByXuid([5, 6, 7]));

        Assert.That(ex!.Message, Is.EqualTo("Failed to find the following xbox users: 6,7"));
        Assert.That(await _context.XboxUsers.AnyAsync(), Is.False, "Nothing is saved when any user is missing");
    }

    [Test]
    public void GetsertXboxUserByXuid_ApiError_Throws()
    {
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(Error("down"));

        var ex = Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUserByXuid(5));

        Assert.That(ex!.Message, Is.EqualTo("Failed to resolved gamertag: down"));
    }

    [Test]
    public void GetsertXboxUserByXuid_UserNotReturned_LogsAndThrows()
    {
        _factory.Users(Arg.Any<IEnumerable<string>>()).Returns(Users(("6", "someone else")));

        var ex = Assert.ThrowsAsync<Exception>(() => _service.GetsertXboxUserByXuid(5));

        Assert.That(ex!.Message, Is.EqualTo("Failed to find user"));
        var entry = _logger.Entries.Single();
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
        Assert.That(entry.Message, Is.EqualTo("Gamertag 5 not found"));
    }
}
