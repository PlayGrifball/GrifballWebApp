using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.Preview;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Surprenant.Grunt.Core;

namespace GrifballWebApp.Integration.Test;

/// <summary>A preview's smoke players' gamertags: made up without Halo, the same account each time; others go to Halo.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class PreviewSmokeXboxUsersTests
{
    private GrifballContext _context;
    private IHaloInfiniteClientFactory _halo;
    private PreviewSmokeXboxUsers _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await TestDatabase.NewGrifballContext();
        _halo = Substitute.For<IHaloInfiniteClientFactory>();
        var lookup = new GetsertXboxUserService(Substitute.For<ILogger<GetsertXboxUserService>>(), _context, _halo, TimeProvider.System);
        _service = new PreviewSmokeXboxUsers(lookup, _context);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task A_smoke_gamertag_is_made_up_with_a_negative_xuid_without_asking_Halo()
    {
        var (made, error) = await _service.GetsertXboxUserByGamertag("smoke-run1-p1");
        var (again, _) = await _service.GetsertXboxUserByGamertag("smoke-run1-p1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Null);
            Assert.That(made!.Gamertag, Is.EqualTo("smoke-run1-p1"));
            Assert.That(made.XboxUserID, Is.Negative);
            Assert.That(made.XboxUserID, Is.EqualTo(PreviewSmokeXboxUsers.Xuid("smoke-run1-p1")), "The same gamertag, the same xuid");
            Assert.That(again!.XboxUserID, Is.EqualTo(made.XboxUserID), "Found again, not made twice");
            Assert.That(await _context.XboxUsers.CountAsync(), Is.EqualTo(1));
            Assert.That(_halo.ReceivedCalls(), Is.Empty);
        }
    }

    [Test]
    public async Task Any_other_gamertag_is_looked_up_on_Halo()
    {
        var (found, error) = await _service.GetsertXboxUserByGamertag("RealPlayer");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.Null);
            Assert.That(error, Is.Not.Null);
            Assert.That(_halo.ReceivedCalls(), Is.Not.Empty, "Asked Halo");
        }
    }
}
