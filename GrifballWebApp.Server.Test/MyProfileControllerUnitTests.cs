using GrifballWebApp.Domain.Players;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Server.Profile;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

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
    private MyProfileController Controller() => new(new RegionService(Db), new ProfilePictureService(Db), null!);

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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControllerTestHelpers.StatusOf(result), Is.EqualTo(StatusCodes.Status204NoContent));
            var regionID = await Db.Users.Where(u => u.Id == user.Id).Select(u => u.RegionID).SingleAsync();
            Assert.That(regionID, Is.EqualTo(6));
        }
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
    public async Task SetProfilePicture_RepliesUnauthorized_ForAnUnknownCaller()
    {
        var result = await Controller().WithAnonymousUser().SetProfilePicture(new SetProfilePictureRequest { ProfilePicture = ProfilePictureChoice.GamerPicture }, CancellationToken.None);

        result.AssertProblem(StatusCodes.Status401Unauthorized, "You must be signed in");
    }

    [Test]
    public async Task SetProfilePicture_SetsTheCallersChoice_AndRefusesAnUnknownOne()
    {
        var user = await AddUser(discordUserId: 1, gamertag: "Grifter1");
        var controller = Controller().WithUser(user.Id.ToString());

        var saved = await controller.SetProfilePicture(new SetProfilePictureRequest { ProfilePicture = ProfilePictureChoice.GamerPicture }, CancellationToken.None);
        var refused = await controller.SetProfilePicture(new SetProfilePictureRequest { ProfilePicture = (ProfilePictureChoice)9 }, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControllerTestHelpers.StatusOf(saved), Is.EqualTo(StatusCodes.Status204NoContent));
            Assert.That(await Db.Users.Where(u => u.Id == user.Id).Select(u => u.ProfilePicture).SingleAsync(), Is.EqualTo(ProfilePictureChoice.GamerPicture));
            refused.AssertProblem(StatusCodes.Status400BadRequest, "There is no such profile picture");
        }
    }

    [Test]
    public async Task SetProfilePicture_RepliesNotFound_ForASignedInUserWhoIsGone()
    {
        var result = await Controller().WithUser("987654").SetProfilePicture(new SetProfilePictureRequest { ProfilePicture = ProfilePictureChoice.GamerPicture }, CancellationToken.None);

        result.AssertProblem(StatusCodes.Status404NotFound, "There is no such user");
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
