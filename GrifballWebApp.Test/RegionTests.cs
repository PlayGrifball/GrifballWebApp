using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.UserManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Security.Claims;

namespace GrifballWebApp.Test;

/// <summary>Regions: one way of reading them, set by the user, a sysadmin or the import (which never overwrites the other two).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RegionTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task Setup() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [TestCase("East", Regions.EastNorthAmerica)]
    [TestCase("Eastern", Regions.EastNorthAmerica)]
    [TestCase("NA East", Regions.EastNorthAmerica)]
    [TestCase("na-east", Regions.EastNorthAmerica)]
    [TestCase("EST", Regions.EastNorthAmerica)]
    [TestCase("Central", Regions.CentralNorthAmerica)]
    [TestCase("CST", Regions.CentralNorthAmerica)]
    [TestCase(" west ", Regions.WestNorthAmerica)]
    [TestCase("PST", Regions.WestNorthAmerica)]
    [TestCase("Europe", Regions.Europe)]
    [TestCase("EU", Regions.Europe)]
    [TestCase("Oceania", Regions.Australia)]
    public void Normalize_ReadsTheSheetsWays(string written, int region)
        => Assert.That(Regions.Normalize(written), Is.EqualTo(region));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Bot")]
    [TestCase("Mars")]
    public void Normalize_OfNothingKnown_IsNull(string? written)
        => Assert.That(Regions.Normalize(written), Is.Null);

    [Test]
    public async Task TheSeed_HasEveryRegionTheHelperGives()
    {
        var ids = await _context.Regions.Select(r => r.RegionID).ToListAsync();
        Assert.That(ids, Is.SupersetOf(new[] { Regions.WestNorthAmerica, Regions.CentralNorthAmerica, Regions.EastNorthAmerica, Regions.NorthEurope, Regions.SouthEurope, Regions.Australia, Regions.Europe }));
    }

    private MyProfileController Controller(int userId)
    {
        var controller = new MyProfileController(_context, new UserGamertagClaimService(_context, new GamertagMatching(_context), TimeProvider.System, Options.Create(new UserClaimOptions())));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test")) },
        };
        return controller;
    }

    [Test]
    public async Task AUser_SetsTheirOwnRegion_AndSeesItOnTheirProfile()
    {
        var user = new User { UserName = "me" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        var controller = Controller(user.Id);

        var set = await controller.SetRegion(new SetRegionRequest { RegionID = Regions.CentralNorthAmerica }, CancellationToken.None);
        var regions = await controller.Regions(CancellationToken.None);
        var profile = await new ProfileController(_context, Substitute.For<ISetGamertagService>(), new GrifballWebApp.Server.Stats.SeasonStatsService(_context))
            .GetProfileStats(user.Id, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var saved = await _context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Multiple(() =>
        {
            Assert.That(set, Is.TypeOf<NoContentResult>());
            Assert.That((saved.RegionID, saved.RegionSource), Is.EqualTo(((int?)Regions.CentralNorthAmerica, (RegionSource?)RegionSource.User)));
            Assert.That(regions.Select(r => r.RegionName), Does.Contain("Central North America"));
            Assert.That((profile.Value!.RegionID, profile.Value.RegionName), Is.EqualTo(((int?)Regions.CentralNorthAmerica, "Central North America")));
        });
    }

    [Test]
    public async Task AUser_ClearsTheirRegion_OrIsRefusedAnUnknownOne()
    {
        var user = new User { UserName = "me", RegionID = Regions.WestNorthAmerica, RegionSource = RegionSource.User };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        var controller = Controller(user.Id);

        var unknown = await controller.SetRegion(new SetRegionRequest { RegionID = 999 }, CancellationToken.None);
        await controller.SetRegion(new SetRegionRequest { RegionID = null }, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var saved = await _context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.That(unknown, Is.TypeOf<BadRequestObjectResult>());
        Assert.That((saved.RegionID, saved.RegionSource), Is.EqualTo(((int?)null, (RegionSource?)null)));
    }

    [Test]
    public async Task SetRegion_ForAUserThatIsGone_IsNotFound()
    {
        Assert.That(await Controller(999).SetRegion(new SetRegionRequest { RegionID = 1 }, CancellationToken.None), Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task ASysadmin_EditsARegion_MarkedAsTheirs()
    {
        var user = new User { UserName = "TestUser" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        var userManager = Substitute.For<UserManager<User>>(Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);
        userManager.GetRolesAsync(Arg.Any<User>()).Returns(new List<string>());
        var service = new UserManagementService(_context, userManager, Substitute.For<IGetsertXboxUserService>());
        var dto = (await service.GetUser(user.Id, CancellationToken.None))!;
        dto.Roles = [];

        dto.RegionID = 999;
        var unknown = await service.EditUser(dto, CancellationToken.None);
        dto.RegionID = Regions.Europe;
        var result = await service.EditUser(dto, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var saved = await _context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.That(unknown, Is.EqualTo("There is no such region"));
        Assert.That(result, Is.Null);
        Assert.That((saved.RegionID, saved.RegionSource), Is.EqualTo(((int?)Regions.Europe, (RegionSource?)RegionSource.Sysadmin)));
        Assert.That((await service.GetUser(user.Id, CancellationToken.None))!.RegionID, Is.EqualTo(Regions.Europe));
    }

    [Test]
    public async Task TheImport_FillsMissingRegions_ButNeverOverwritesTheUsersOrASysadmins()
    {
        _context.Users.AddRange(
            new User { UserName = "historical:Empty", NormalizedUserName = "HISTORICAL:EMPTY", IsDummyUser = true },
            new User { UserName = "historical:Chosen", NormalizedUserName = "HISTORICAL:CHOSEN", IsDummyUser = true, RegionID = Regions.WestNorthAmerica, RegionSource = RegionSource.User },
            new User { UserName = "historical:Admin", NormalizedUserName = "HISTORICAL:ADMIN", IsDummyUser = true, RegionID = Regions.WestNorthAmerica, RegionSource = RegionSource.Sysadmin },
            new User { UserName = "historical:Imported", NormalizedUserName = "HISTORICAL:IMPORTED", IsDummyUser = true, RegionID = Regions.WestNorthAmerica, RegionSource = RegionSource.Import });
        await _context.SaveChangesAsync();
        var import = new HistoricalImportService(_context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions()), NullLogger<HistoricalImportService>.Instance,
            new GrifballWebApp.Server.Championships.ChampionshipService(_context), new GamertagMatching(_context), TimeProvider.System);

        var report = await import.Import(new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive(),
            Players =
            [
                new ArchivePlayerInput { Gamertag = "Empty", Region = "East" },
                new ArchivePlayerInput { Gamertag = "Chosen", Region = "East" },
                new ArchivePlayerInput { Gamertag = "Admin", Region = "East" },
                new ArchivePlayerInput { Gamertag = "Imported", Region = "East" },
                new ArchivePlayerInput { Gamertag = "Nobody", Region = "East" },
            ],
        });

        _context.ChangeTracker.Clear();
        var regions = await _context.Users.OrderBy(u => u.Id).Select(u => new { u.UserName, u.RegionID, u.RegionSource }).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(report.RegionsSet, Is.EqualTo(2));
            Assert.That(regions.Select(r => (r.UserName, r.RegionID, r.RegionSource)), Is.EqualTo(new[]
            {
                ("historical:Empty", (int?)Regions.EastNorthAmerica, (RegionSource?)RegionSource.Import),
                ("historical:Chosen", (int?)Regions.WestNorthAmerica, (RegionSource?)RegionSource.User),
                ("historical:Admin", (int?)Regions.WestNorthAmerica, (RegionSource?)RegionSource.Sysadmin),
                ("historical:Imported", (int?)Regions.EastNorthAmerica, (RegionSource?)RegionSource.Import),
            }), "Nobody has no placeholder and no Discord ID: nothing made for a region alone");
        });
    }
}
