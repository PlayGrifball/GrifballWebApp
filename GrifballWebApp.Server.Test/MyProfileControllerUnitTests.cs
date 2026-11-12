using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Server.Profile;
using Microsoft.AspNetCore.Http;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// MyProfileController's unit tests. Regions and SetRegion need nothing but a GrifballContext (temp-file SQLite,
/// #402): RegionService is IApplicationDbContext-backed alone. The claims actions stay
/// GrifballWebApp.Integration.Test's (UserGamertagClaimService needs more than a database) except their sign-in gate.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MyProfileControllerUnitTests : SqliteGrifballContextFixture
{
    private MyProfileController Controller() => new(new RegionService(Db), null!);

    [Test]
    public async Task Regions_ReturnsTheSeededRegions()
    {
        var regions = await Controller().Regions(CancellationToken.None);

        Assert.That(regions.Select(x => x.RegionName), Has.Member("Australia"));
    }

    [Test]
    public async Task SetRegion_RepliesUnauthorized_ForAnUnknownCaller()
    {
        var controller = Controller().WithAnonymousUser();

        var result = await controller.SetRegion(new SetRegionRequest { RegionID = 6 }, CancellationToken.None);

        result.AssertProblem(StatusCodes.Status401Unauthorized, "You must be signed in");
    }

    [Test]
    public async Task SetRegion_SetsTheCallersRegion()
    {
        var user = await AddUser(discordUserId: 1, gamertag: "Grifter1");
        var controller = Controller().WithUser(user.Id.ToString());

        var result = await controller.SetRegion(new SetRegionRequest { RegionID = 6 }, CancellationToken.None);

        Assert.That(ControllerTestHelpers.StatusOf(result), Is.EqualTo(StatusCodes.Status204NoContent));
    }

    [Test]
    public async Task SetRegion_RepliesBadRequest_ForAnUnknownRegion()
    {
        var user = await AddUser(discordUserId: 1, gamertag: "Grifter1");
        var controller = Controller().WithUser(user.Id.ToString());

        var result = await controller.SetRegion(new SetRegionRequest { RegionID = 999 }, CancellationToken.None);

        result.AssertProblem(StatusCodes.Status400BadRequest, "There is no such region");
    }

    [Test]
    public async Task PreviewClaim_RepliesUnauthorized_ForAnUnknownCaller()
    {
        var controller = Controller().WithAnonymousUser();

        var result = await controller.PreviewClaim("SomeGamertag", CancellationToken.None);

        result.Result.AssertProblem(StatusCodes.Status401Unauthorized, "You must be signed in");
    }

    [Test]
    public async Task ClaimGamertag_RepliesUnauthorized_ForAnUnknownCaller()
    {
        var controller = Controller().WithAnonymousUser();

        var result = await controller.ClaimGamertag(new SubmitGamertagClaimRequest { Gamertag = "SomeGamertag" }, CancellationToken.None);

        result.Result.AssertProblem(StatusCodes.Status401Unauthorized, "You must be signed in");
    }

    [Test]
    public async Task GamertagClaims_RepliesUnauthorized_ForAnUnknownCaller()
    {
        var controller = Controller().WithAnonymousUser();

        var result = await controller.GamertagClaims(CancellationToken.None);

        result.Result.AssertProblem(StatusCodes.Status401Unauthorized, "You must be signed in");
    }
}
