using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Security.Claims;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.UserManagement;

namespace GrifballWebApp.Integration.Test;

/// <summary>Regions: one way of reading them, set by the user, a sysadmin or the import (which never overwrites the other two).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RegionTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task Setup() => _context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task TheSeed_HasEveryRegionTheHelperGives()
    {
        var ids = await _context.Regions.Select(r => r.RegionID).ToListAsync();
        Assert.That(ids, Is.SupersetOf(new[] { Regions.WestNorthAmerica, Regions.CentralNorthAmerica, Regions.EastNorthAmerica, Regions.NorthEurope, Regions.SouthEurope, Regions.Australia, Regions.Europe }));
    }

    private MyProfileController Controller(int userId)
    {
        var controller = new MyProfileController(new GrifballWebApp.Application.Profile.RegionService(_context), new UserGamertagClaimService(_context, new GamertagMatching(_context), TimeProvider.System, Options.Create(new UserClaimOptions())));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test")) },
        };
        return controller;
    }

    [Test]
    public async Task AUser_SetsTheirOwnRegion_AndSeesItOnTheirProfile()
    {
        var user = User.Seed(userName: "me");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        var controller = Controller(user.Id);

        var set = await controller.SetRegion(new SetRegionRequest { RegionID = Regions.CentralNorthAmerica }, CancellationToken.None);
        var regions = await controller.Regions(CancellationToken.None);
        var profile = await new ProfileController(_context, Substitute.For<ISetGamertagService>(), new GrifballWebApp.Application.Profile.ProfileStatsService(_context, new GrifballWebApp.Application.Stats.SeasonStatsService(_context), new GrifballWebApp.Application.LateLeague.MatchmakingStatsService(_context)), new GrifballWebApp.Application.Profile.ProfileCommendationsService(_context))
            .GetProfileStats(user.Id, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var saved = await _context.Users.SingleAsync(u => u.Id == user.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(set, Is.TypeOf<NoContentResult>());
            Assert.That((saved.RegionID, saved.RegionSource), Is.EqualTo(((int?)Regions.CentralNorthAmerica, (RegionSource?)RegionSource.User)));
            Assert.That(regions.Select(r => r.RegionName), Does.Contain("Central North America"));
            Assert.That((profile.Value!.RegionID, profile.Value.RegionName), Is.EqualTo(((int?)Regions.CentralNorthAmerica, "Central North America")));
        }
    }

    [Test]
    public async Task AUser_ClearsTheirRegion_OrIsRefusedAnUnknownOne()
    {
        var user = User.Seed(userName: "me", regionID: Regions.WestNorthAmerica, regionSource: RegionSource.User);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        var controller = Controller(user.Id);

        var unknown = await controller.SetRegion(new SetRegionRequest { RegionID = 999 }, CancellationToken.None);
        await controller.SetRegion(new SetRegionRequest { RegionID = null }, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var saved = await _context.Users.SingleAsync(u => u.Id == user.Id);
        using (Assert.EnterMultipleScope())
        {
            unknown.AssertProblem(Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest, "There is no such region");
            Assert.That((saved.RegionID, saved.RegionSource), Is.EqualTo(((int?)null, (RegionSource?)null)));
        }
    }

    [Test]
    public async Task SetRegion_ForAUserThatIsGone_IsNotFound()
    {
        (await Controller(999).SetRegion(new SetRegionRequest { RegionID = 1 }, CancellationToken.None)).AssertProblem(Microsoft.AspNetCore.Http.StatusCodes.Status404NotFound, "There is no such user");
    }

    [Test]
    public async Task ASysadmin_EditsARegion_MarkedAsTheirs()
    {
        var user = User.Seed(userName: "TestUser");
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(unknown, Is.EqualTo("There is no such region"));
            Assert.That(result, Is.Null);
            Assert.That((saved.RegionID, saved.RegionSource), Is.EqualTo(((int?)Regions.Europe, (RegionSource?)RegionSource.Sysadmin)));
            Assert.That((await service.GetUser(user.Id, CancellationToken.None))!.RegionID, Is.EqualTo(Regions.Europe));
        }
    }

    [Test]
    public async Task TheImport_FillsMissingRegions_ButNeverOverwritesTheUsersOrASysadmins()
    {
        _context.Users.AddRange(
            User.Seed(userName: "historical:Empty", normalizedUserName: "HISTORICAL:EMPTY", isDummyUser: true),
            User.Seed(userName: "historical:Chosen", normalizedUserName: "HISTORICAL:CHOSEN", isDummyUser: true, regionID: Regions.WestNorthAmerica, regionSource: RegionSource.User),
            User.Seed(userName: "historical:Admin", normalizedUserName: "HISTORICAL:ADMIN", isDummyUser: true, regionID: Regions.WestNorthAmerica, regionSource: RegionSource.Sysadmin),
            User.Seed(userName: "historical:Imported", normalizedUserName: "HISTORICAL:IMPORTED", isDummyUser: true, regionID: Regions.WestNorthAmerica, regionSource: RegionSource.Import));
        await _context.SaveChangesAsync();
        var import = new HistoricalImportService(_context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions()), NullLogger<HistoricalImportService>.Instance,
            new GamertagMatching(_context), TimeProvider.System);

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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.RegionsSet, Is.EqualTo(2));
            Assert.That(regions.Select(r => (r.UserName, r.RegionID, r.RegionSource)), Is.EqualTo(new[]
            {
                ("historical:Empty", (int?)Regions.EastNorthAmerica, (RegionSource?)RegionSource.Import),
                ("historical:Chosen", (int?)Regions.WestNorthAmerica, (RegionSource?)RegionSource.User),
                ("historical:Admin", (int?)Regions.WestNorthAmerica, (RegionSource?)RegionSource.Sysadmin),
                ("historical:Imported", (int?)Regions.EastNorthAmerica, (RegionSource?)RegionSource.Import),
            }), "Nobody has no placeholder and no Discord ID: nothing made for a region alone");
        }
    }
}
