using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GetsertXboxUserServiceTests
{
    private GrifballContext _context;
    private ILogger<GetsertXboxUserService> _logger;
    private IHaloInfiniteClientFactory _haloInfiniteClientFactory;
    private GetsertXboxUserService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _logger = Substitute.For<ILogger<GetsertXboxUserService>>();
        _haloInfiniteClientFactory = Substitute.For<IHaloInfiniteClientFactory>();
        _service = new GetsertXboxUserService(_logger, _context, _haloInfiniteClientFactory);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task GetsertXboxUserByGamertag_ShouldReturnExistingUser_WhenUserExists()
    {
        var user = new XboxUser { XboxUserID = 123, Gamertag = "TestUser" };
        _context.XboxUsers.Add(user);
        await _context.SaveChangesAsync();

        var (result, error) = await _service.GetsertXboxUserByGamertag("TestUser");
        Assert.Multiple(() => {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.XboxUserID, Is.EqualTo(123));
            Assert.That(error, Is.Null);
        });
    }

    // Gamertags match whatever their case, on both databases (Postgres's case-insensitive collation): a
    // different case finds the same user, without asking Halo again or adding a second one.
    [Test]
    public async Task GetsertXboxUserByGamertag_DifferentCase_ReturnsExistingUser()
    {
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 123, Gamertag = "TestUser" });
        await _context.SaveChangesAsync();

        var (result, error) = await _service.GetsertXboxUserByGamertag("tESTuSER");

        Assert.Multiple(() =>
        {
            Assert.That(result?.XboxUserID, Is.EqualTo(123));
            Assert.That(error, Is.Null);
        });
        await _haloInfiniteClientFactory.DidNotReceiveWithAnyArgs().UserByGamertag(default!);
    }

    [Test]
    public async Task GetsertXboxUserByGamertag_ShouldCreateUser_WhenNotExistsAndApiReturnsValid()
    {
        var apiResponse = new HaloApiResultContainer<Surprenant.Grunt.Models.User, HaloApiErrorContainer>(
            new Surprenant.Grunt.Models.User { gamertag = "NewUser", xuid = "456" }, null);
        _haloInfiniteClientFactory.UserByGamertag("NewUser").Returns(Task.FromResult(apiResponse));

        var (result, error) = await _service.GetsertXboxUserByGamertag("NewUser");
        Assert.Multiple(() => {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.XboxUserID, Is.EqualTo(456));
            Assert.That(result.Gamertag, Is.EqualTo("NewUser"));
            Assert.That(error, Is.Null);
        });
        Assert.That(_context.XboxUsers.Any(x => x.XboxUserID == 456), Is.True);
    }

    [Test]
    public async Task GetsertXboxUserByGamertag_ShouldReturnError_WhenApiReturnsNull()
    {
        _haloInfiniteClientFactory.UserByGamertag("MissingUser").Returns(Task.FromResult<HaloApiResultContainer<Surprenant.Grunt.Models.User, HaloApiErrorContainer>>(null));
        var (result, error) = await _service.GetsertXboxUserByGamertag("MissingUser");
        Assert.Multiple(() => {
            Assert.That(result, Is.Null);
            Assert.That(error, Is.EqualTo("Did not find that gamertag"));
        });
    }

    [Test]
    public async Task GetsertXboxUserByGamertag_ShouldReturnError_WhenApiReturnsInvalidXuid()
    {
        var apiResponse = new HaloApiResultContainer<Surprenant.Grunt.Models.User, HaloApiErrorContainer>(
            new Surprenant.Grunt.Models.User { gamertag = "BadUser", xuid = "notanumber" }, null);
        _haloInfiniteClientFactory.UserByGamertag("BadUser").Returns(Task.FromResult(apiResponse));
        var (result, error) = await _service.GetsertXboxUserByGamertag("BadUser");
        Assert.Multiple(() => {
            Assert.That(result, Is.Null);
            Assert.That(error, Is.EqualTo("Failed to parse xuid. Contact sysadmin"));
        });
    }

    [Test]
    public async Task GetsertXboxUserByXuid_ShouldReturnExistingUser()
    {
        var user = new XboxUser { XboxUserID = 789, Gamertag = "XuidUser" };
        _context.XboxUsers.Add(user);
        await _context.SaveChangesAsync();
        var result = await _service.GetsertXboxUserByXuid(789);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.XboxUserID, Is.EqualTo(789));
    }

    [Test]
    public async Task GetsertXboxUserByXuid_ShouldCreateUser_WhenNotExistsAndApiReturnsValid()
    {
        var apiResponse = new HaloApiResultContainer<List<Surprenant.Grunt.Models.User>, HaloApiErrorContainer>(
            new List<Surprenant.Grunt.Models.User> { new Surprenant.Grunt.Models.User { gamertag = "XuidNew", xuid = "101112" } }, null);
        _haloInfiniteClientFactory.Users(Arg.Is<IEnumerable<string>>(x => x.Contains("101112"))).Returns(apiResponse);
        var fooo = await _haloInfiniteClientFactory.Users(["101112"]);
        var result = await _service.GetsertXboxUserByXuid(101112);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.XboxUserID, Is.EqualTo(101112));
        Assert.That(result.Gamertag, Is.EqualTo("XuidNew"));
    }

    [Test]
    public async Task GetsertXboxUsersByXuid_ShouldReturnExistingAndCreateMissing()
    {
        var existing = new XboxUser { XboxUserID = 1, Gamertag = "Existing" };
        _context.XboxUsers.Add(existing);
        await _context.SaveChangesAsync();
        var apiResponse = new HaloApiResultContainer<List<Surprenant.Grunt.Models.User>, HaloApiErrorContainer>(
            new List<Surprenant.Grunt.Models.User> {
                //new Surprenant.Grunt.Models.User { gamertag = "Existing", xuid = "1" }, // The service is smart enough to not call the API for existing users
                new Surprenant.Grunt.Models.User { gamertag = "NewGuy", xuid = "2" }
            }, null);
        _haloInfiniteClientFactory.Users(Arg.Is<IEnumerable<string>>(x => x.Contains("2") && x.Count() == 1)).Returns(apiResponse);
        var result = await _service.GetsertXboxUsersByXuid([1, 2]);
        Assert.That(result.Length, Is.EqualTo(2));
        Assert.That(result.Any(x => x.XboxUserID == 1), Is.True);
        Assert.That(result.Any(x => x.XboxUserID == 2), Is.True);
    }

    [Test]
    public async Task GetsertXboxUserByGamertag_RenamedPlayer_UpdatesTheStoredGamertag_InsteadOfInsertingAgain()
    {
        var owner = new GrifballWebApp.Database.Models.User { UserName = "owner" };
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 789, Gamertag = "OldName", User = owner });
        await _context.SaveChangesAsync();
        var apiResponse = new HaloApiResultContainer<Surprenant.Grunt.Models.User, HaloApiErrorContainer>(
            new Surprenant.Grunt.Models.User { gamertag = "NewName", xuid = "789" }, null);
        _haloInfiniteClientFactory.UserByGamertag("newname").Returns(Task.FromResult(apiResponse));

        var (result, error) = await _service.GetsertXboxUserByGamertag("newname");

        await using var fresh = _context.NewContextLike();
        var stored = fresh.XboxUsers.Single();
        Assert.Multiple(() =>
        {
            Assert.That(error, Is.Null);
            Assert.That(result!.XboxUserID, Is.EqualTo(789));
            Assert.That(result.User?.UserName, Is.EqualTo("owner"), "Still the same account's, so it can't be taken");
            Assert.That(stored.Gamertag, Is.EqualTo("NewName"), "Halo's spelling");
        });
    }

    [Test]
    public async Task GetsertXboxUserByGamertag_RenamedPlayer_KeepsTheOldGamertagInItsHistory()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 789, Gamertag = "OldName" });
        await _context.SaveChangesAsync();
        var apiResponse = new HaloApiResultContainer<Surprenant.Grunt.Models.User, HaloApiErrorContainer>(
            new Surprenant.Grunt.Models.User { gamertag = "NewName", xuid = "789" }, null);
        _haloInfiniteClientFactory.UserByGamertag("NewName").Returns(Task.FromResult(apiResponse));

        await _service.GetsertXboxUserByGamertag("NewName");

        await using var fresh = _context.NewContextLike();
        var history = await fresh.XboxGamertagHistory.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That((history.XboxUserID, history.Gamertag, history.NormalizedGamertag, history.Source), Is.EqualTo((789L, "OldName", "OLDNAME", GamertagHistorySource.RenameDetected)));
            Assert.That(history.FirstSeen is null || history.FirstSeen <= history.LastSeen, "From when it was stored (its last change), if known");
            Assert.That(history.LastSeen, Is.GreaterThan(before), "Until the rename was seen");
        });
    }

    [Test]
    public async Task GetsertXboxUserByGamertag_SameNameInAnotherCase_IsNoRename()
    {
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 789, Gamertag = "Some Name" });
        await _context.SaveChangesAsync();
        // Found by the case-insensitive lookup first; asked of the API only when it isn't
        var (result, _) = await _service.GetsertXboxUserByGamertag("some name");

        Assert.That(result!.XboxUserID, Is.EqualTo(789));
        Assert.That(await _context.XboxGamertagHistory.AnyAsync(), Is.False);
    }
}
