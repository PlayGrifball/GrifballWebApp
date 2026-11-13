using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.HaloInfinite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using GruntUser = Surprenant.Grunt.Models.User;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// GetsertXboxUserService, on a real (temp-file SQLite) GrifballContext: no database server, host or network. Its
/// HaloErrors.Describe helper and its error paths already have their own unit tests
/// (GetsertXboxUserServiceErrorUnitTests); this covers the gets, the inserts and the rename-on-lookup.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GetsertXboxUserServiceTests : SqliteContextFixture
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly long[] OneAndTwo = [1, 2];
    private static readonly long[] JustOne = [1];
    private static readonly long[] JustSeven = [7];
    private static readonly string[] OneAndTwoGamertags = ["One", "Two"];

    private IHaloInfiniteClient _client = null!;
    private FakeTimeProvider _time = null!;
    private GetsertXboxUserService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _client = Substitute.For<IHaloInfiniteClient>();
        _time = new FakeTimeProvider(Now);
        _service = new GetsertXboxUserService(NullLogger<GetsertXboxUserService>.Instance, Context, _client, _time);
    }

    private static GruntUser Gamertag(string xuid, string gamertag) => new() { xuid = xuid, gamertag = gamertag };

    [Test]
    public async Task GetsertXboxUserByGamertag_AlreadyStored_ReturnsItWithoutCallingHalo()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(42, "Grif"));
        await Context.SaveChangesAsync();

        var (xboxUser, error) = await _service.GetsertXboxUserByGamertag("Grif");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(xboxUser?.XboxUserID, Is.EqualTo(42));
            Assert.That(error, Is.Null);
        }
        await _client.DidNotReceive().UserByGamertag(Arg.Any<string>());
    }

    [Test]
    public async Task GetsertXboxUserByGamertag_NotStored_LooksItUpAndInserts()
    {
        _client.UserByGamertag("Grif").Returns(new HaloApiResultContainer<GruntUser, HaloApiErrorContainer>(Gamertag("42", "Grif"), null!));

        var (xboxUser, error) = await _service.GetsertXboxUserByGamertag("Grif");

        var stored = await Context.XboxUsers.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Null);
            Assert.That(xboxUser?.XboxUserID, Is.EqualTo(42));
            Assert.That(stored.Gamertag, Is.EqualTo("Grif"));
        }
    }

    [TestCase(null)]
    [TestCase("")]
    public async Task GetsertXboxUserByGamertag_HaloFindsNoGamertag_ReturnsAnError(string? gamertag)
    {
        _client.UserByGamertag("Missing").Returns(new HaloApiResultContainer<GruntUser, HaloApiErrorContainer>(Gamertag("1", gamertag!), null!));

        var (xboxUser, error) = await _service.GetsertXboxUserByGamertag("Missing");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(xboxUser, Is.Null);
            Assert.That(error, Is.EqualTo("Did not find that gamertag"));
        }
    }

    [Test]
    public async Task GetsertXboxUserByGamertag_HaloAnswersNull_ReturnsAnError()
    {
        _client.UserByGamertag("Missing").Returns((HaloApiResultContainer<GruntUser, HaloApiErrorContainer>)null!);

        var (xboxUser, error) = await _service.GetsertXboxUserByGamertag("Missing");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(xboxUser, Is.Null);
            Assert.That(error, Is.EqualTo("Did not find that gamertag"));
        }
    }

    [Test]
    public async Task GetsertXboxUserByGamertag_UnparseableXuid_ReturnsAnError()
    {
        _client.UserByGamertag("Grif").Returns(new HaloApiResultContainer<GruntUser, HaloApiErrorContainer>(Gamertag("not-a-number", "Grif"), null!));

        var (xboxUser, error) = await _service.GetsertXboxUserByGamertag("Grif");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(xboxUser, Is.Null);
            Assert.That(error, Is.EqualTo("Failed to parse xuid. Contact sysadmin"));
        }
    }

    [Test]
    public async Task GetsertXboxUserByGamertag_RenamedAccount_UpdatesTheStoredGamertagInstead()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(42, "OldName"));
        await Context.SaveChangesAsync();
        _client.UserByGamertag("NewName").Returns(new HaloApiResultContainer<GruntUser, HaloApiErrorContainer>(Gamertag("42", "NewName"), null!));

        var (xboxUser, error) = await _service.GetsertXboxUserByGamertag("NewName");

        var stored = await Context.XboxUsers.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Null);
            Assert.That(xboxUser?.XboxUserID, Is.EqualTo(42));
            Assert.That(stored.Gamertag, Is.EqualTo("NewName"));
            Assert.That(stored.PastGamertags.Select(h => h.Gamertag), Has.Member("OldName"));
        }
    }

    [Test]
    public async Task GetsertXboxUsersByXuid_SomeAlreadyStored_OnlyLooksUpTheRest()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(1, "One"));
        await Context.SaveChangesAsync();
        var newGamertags = new List<GruntUser> { Gamertag("2", "Two") };
        _client.Users(Arg.Is<IEnumerable<string>>(x => x.Single() == "2")).Returns(new HaloApiResultContainer<List<GruntUser>, HaloApiErrorContainer>(newGamertags, null!));

        var result = await _service.GetsertXboxUsersByXuid(OneAndTwo);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Select(x => x.XboxUserID), Is.EquivalentTo(OneAndTwo));
            Assert.That((await Context.XboxUsers.ToListAsync()).Select(x => x.Gamertag), Is.EquivalentTo(OneAndTwoGamertags));
        }
    }

    [Test]
    public async Task GetsertXboxUsersByXuid_AllAlreadyStored_NeverCallsHalo()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(1, "One"));
        await Context.SaveChangesAsync();

        var result = await _service.GetsertXboxUsersByXuid(JustOne);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Select(x => x.XboxUserID), Is.EqualTo(JustOne));
            await _client.DidNotReceive().Users(Arg.Any<IEnumerable<string>>());
        }
    }

    [Test]
    public void GetsertXboxUsersByXuid_HaloAnswersNoResult_Throws()
    {
        _client.Users(Arg.Any<IEnumerable<string>>())
            .Returns(new HaloApiResultContainer<List<GruntUser>, HaloApiErrorContainer>(null!, new HaloApiErrorContainer { Code = 500 }));

        Assert.That(async () => await _service.GetsertXboxUsersByXuid(JustSeven), Throws.Exception.With.Message.Contains("Failed to resolve gamertag"));
    }

    [Test]
    public void GetsertXboxUsersByXuid_HaloMissesSomeXuids_ThrowsNamingThem()
    {
        _client.Users(Arg.Any<IEnumerable<string>>())
            .Returns(new HaloApiResultContainer<List<GruntUser>, HaloApiErrorContainer>([], null!));

        Assert.That(async () => await _service.GetsertXboxUsersByXuid(JustSeven), Throws.Exception.With.Message.Contains("7"));
    }

    [Test]
    public async Task GetsertXboxUserByXuid_AlreadyStored_ReturnsItWithoutCallingHalo()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(42, "Grif"));
        await Context.SaveChangesAsync();

        var xboxUser = await _service.GetsertXboxUserByXuid(42);

        Assert.That(xboxUser.Gamertag, Is.EqualTo("Grif"));
        await _client.DidNotReceive().Users(Arg.Any<IEnumerable<string>>());
    }

    [Test]
    public async Task GetsertXboxUserByXuid_NotStored_LooksItUpAndInserts()
    {
        _client.Users(Arg.Any<IEnumerable<string>>()).Returns(new HaloApiResultContainer<List<GruntUser>, HaloApiErrorContainer>([Gamertag("42", "Grif")], null!));

        var xboxUser = await _service.GetsertXboxUserByXuid(42);

        var stored = await Context.XboxUsers.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(xboxUser.Gamertag, Is.EqualTo("Grif"));
            Assert.That(stored.XboxUserID, Is.EqualTo(42));
        }
    }

    [Test]
    public void GetsertXboxUserByXuid_HaloAnswersNoResult_Throws()
    {
        _client.Users(Arg.Any<IEnumerable<string>>())
            .Returns(new HaloApiResultContainer<List<GruntUser>, HaloApiErrorContainer>(null!, new HaloApiErrorContainer { Code = 500 }));

        Assert.That(async () => await _service.GetsertXboxUserByXuid(7), Throws.Exception.With.Message.Contains("Failed to resolve gamertag"));
    }

    [Test]
    public void GetsertXboxUserByXuid_HaloFindsNoMatchingUser_Throws()
    {
        _client.Users(Arg.Any<IEnumerable<string>>()).Returns(new HaloApiResultContainer<List<GruntUser>, HaloApiErrorContainer>([], null!));

        Assert.That(async () => await _service.GetsertXboxUserByXuid(7), Throws.Exception.With.Message.EqualTo("Failed to find user"));
    }

    [Test]
    public void GetsertXboxUserByXuid_HaloAnswersABlankGamertag_Throws()
    {
        _client.Users(Arg.Any<IEnumerable<string>>()).Returns(new HaloApiResultContainer<List<GruntUser>, HaloApiErrorContainer>([Gamertag("7", "")], null!));

        Assert.That(async () => await _service.GetsertXboxUserByXuid(7), Throws.Exception.With.Message.EqualTo("Failed to find user"));
    }
}
