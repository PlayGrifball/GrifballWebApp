using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Spartan;
using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.XboxAccounts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Server.Test;

/// <summary>The Spartan endpoints of the profile controller (a user's own Spartan and its files) and the Spartan headshot the
/// profile picture endpoints give: over a temp-file SQLite database and a fake render service catalog.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SpartanEndpointsUnitTests : SqliteGrifballContextFixture
{
    private static readonly DateTimeOffset Start = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Head = [0x89, 0x50, 0x4E, 0x47, 9];
    private static readonly byte[] Emblem = [0x89, 0x50, 0x4E, 0x47, 1];
    private static readonly SpartanCatalog Catalog = new(
        [new SpartanCatalogCore("Mark VII", [new SpartanCatalogRegion("REGION", "helmet", 100, [new(1, "Aviator")])])], ["111"], []);

    private sealed class FakeCatalogs(SpartanCatalog? catalog) : ISpartanCatalogSource
    {
        public Task<SpartanCatalog?> Get(CancellationToken ct = default) => Task.FromResult(catalog);
    }

    private sealed class VanishingHeadshots(GrifballWebApp.Application.Database.IApplicationDbContext db) : XboxImageService(db)
    {
        public override Task<XboxImageContent?> GetSpartanHeadshotAsync(int userID, CancellationToken ct = default)
            => Task.FromResult<XboxImageContent?>(null);
    }

    private SpartanService Spartans(bool catalogUp = true)
        => new(Db, new FakeCatalogs(catalogUp ? Catalog : null), new FakeTimeProvider(Start));

    private MyProfileController Profile(int? userId, bool catalogUp = true)
    {
        var controller = new MyProfileController(new RegionService(Db), new ProfilePictureService(Db), null!, Spartans(catalogUp));
        return userId is { } id ? controller.WithUser(id.ToString()) : controller.WithAnonymousUser();
    }

    private XboxAccountsController Pictures() => new(new XboxAccountService(Db))
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
    };

    private async Task<User> UserAsync(string name = "grif", long? xuid = null)
    {
        var user = User.Seed(userName: name, xboxUser: xuid is { } x ? XboxUser.FromXbox(x, name) : null);
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        return user;
    }

    private async Task StoreHeadshotAsync(int userId, DateTime renderedAt, string name = "neutral-head")
    {
        if (await Db.UserSpartanRenders.FindAsync(userId) is null)
            Db.UserSpartanRenders.Add(new UserSpartanRender { UserId = userId, State = UserSpartanRenderState.Done, RequestedAt = renderedAt, RenderedAt = renderedAt });
        Db.UserSpartanFiles.Add(new UserSpartanFile { UserId = userId, Name = name, ContentType = "image/png", Content = Head });
        await Db.SaveChangesAsync();
    }

    private async Task StoreEmblemAsync(long xuid)
    {
        Db.XboxUserImages.Add(new XboxUserImage
        {
            XboxUserID = xuid, Kind = XboxUserImageKind.HaloEmblem, Status = XboxUserImageStatus.Found,
            Content = Emblem, ContentType = "image/png", CheckedAt = Start.UtcDateTime,
        });
        await Db.SaveChangesAsync();
    }

    private static SpartanChoice Mk7() => new("Mark VII", new Dictionary<string, string> { ["helmet"] = "Aviator" }, null, "111");

    // --- MyProfileController ---

    [Test]
    public async Task TheOwnSpartanEndpoints_RefuseAnUnknownCaller()
    {
        var anonymous = Profile(null);

        var get = await anonymous.MySpartan(CancellationToken.None);
        var set = await anonymous.SetSpartan(Mk7(), CancellationToken.None);
        var clear = await anonymous.ClearSpartan(CancellationToken.None);
        var render = await anonymous.RenderSpartan(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            get.AssertProblem(StatusCodes.Status401Unauthorized, "You must be signed in");
            set.AssertProblem(StatusCodes.Status401Unauthorized, "You must be signed in");
            clear.AssertProblem(StatusCodes.Status401Unauthorized, "You must be signed in");
            render.AssertProblem(StatusCodes.Status401Unauthorized, "You must be signed in");
        }
    }

    [Test]
    public async Task TheOwnSpartanEndpoints_AreNotFound_ForASignedInIdThatIsNoUser()
    {
        var ghost = Profile(9999);

        var get = await ghost.MySpartan(CancellationToken.None);
        var set = await ghost.SetSpartan(Mk7(), CancellationToken.None);
        var clear = await ghost.ClearSpartan(CancellationToken.None);
        var render = await ghost.RenderSpartan(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            get.AssertProblem(StatusCodes.Status404NotFound, "There is no such user");
            set.AssertProblem(StatusCodes.Status404NotFound, "There is no such user");
            clear.AssertProblem(StatusCodes.Status404NotFound, "There is no such user");
            render.AssertProblem(StatusCodes.Status404NotFound, "There is no such user");
        }
    }

    [Test]
    public async Task MySpartan_GivesTheCallersChoiceAndRender_AfterSettingIt()
    {
        var user = await UserAsync();
        var controller = Profile(user.Id);

        var set = await controller.SetSpartan(Mk7(), CancellationToken.None);
        var got = await controller.MySpartan(CancellationToken.None);

        var dto = got.Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControllerTestHelpers.StatusOf(set), Is.EqualTo(StatusCodes.Status204NoContent));
            Assert.That(dto.Choice!.Core, Is.EqualTo("Mark VII"));
            Assert.That(dto.RenderState, Is.EqualTo("Requested"));
        }
    }

    [Test]
    public async Task GetSpartanCatalog_GivesTheCatalog_Or503WhenTheRenderServiceIsDown()
    {
        var up = await Profile(null).CatalogOfSpartans(CancellationToken.None);
        var down = await Profile(null, catalogUp: false).CatalogOfSpartans(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(up.Value, Is.SameAs(Catalog));
            down.AssertProblem(StatusCodes.Status503ServiceUnavailable, "The Spartan render service is not available right now");
        }
    }

    [Test]
    public async Task SetSpartan_RefusesWhatTheCatalogDoesNotAllow_WithItsProblemsJoined_AndKeepsNothing()
    {
        var user = await UserAsync();

        var result = await Profile(user.Id).SetSpartan(new SpartanChoice("Mark VII", null, null, "999"), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            result.AssertProblem(StatusCodes.Status400BadRequest, "There is no coating '999'");
            Assert.That(await Db.UserSpartans.AnyAsync(), Is.False);
        }
    }

    [Test]
    public async Task SetSpartan_Says503_WhenTheRenderServiceIsDown()
    {
        var user = await UserAsync();

        var result = await Profile(user.Id, catalogUp: false).SetSpartan(Mk7(), CancellationToken.None);

        result.AssertProblem(StatusCodes.Status503ServiceUnavailable, "The Spartan render service is not available right now");
    }

    [Test]
    public async Task ClearSpartan_GoesBackToTheEquippedSet_AndRenderSpartan_AsksForTheRender()
    {
        var user = await UserAsync();
        var controller = Profile(user.Id);
        await controller.SetSpartan(Mk7(), CancellationToken.None);

        var cleared = await controller.ClearSpartan(CancellationToken.None);
        var rendered = await controller.RenderSpartan(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControllerTestHelpers.StatusOf(cleared), Is.EqualTo(StatusCodes.Status204NoContent));
            Assert.That(await Db.UserSpartans.AnyAsync(), Is.False);
            Assert.That(ControllerTestHelpers.StatusOf(rendered), Is.EqualTo(StatusCodes.Status202Accepted));
        }
    }

    [Test]
    public async Task SpartanFile_GivesARenderedFile_CachedForFiveMinutes()
    {
        var user = await UserAsync();
        await StoreHeadshotAsync(user.Id, Start.UtcDateTime, "neutral-body");
        var controller = Profile(null);

        var result = await controller.SpartanFile(user.Id, "neutral-body", CancellationToken.None);

        var file = (FileContentResult)result;
        using (Assert.EnterMultipleScope())
        {
            Assert.That((file.FileContents, file.ContentType), Is.EqualTo((Head, "image/png")));
            Assert.That(controller.Response.Headers.CacheControl.ToString(), Is.EqualTo("public, max-age=300"));
        }
    }

    [Test]
    public async Task SpartanFile_IsNotFound_ForANameItDoesNotKnow_OrAFileNotRenderedYet()
    {
        var user = await UserAsync();
        await StoreHeadshotAsync(user.Id, Start.UtcDateTime, "secret-name");

        var unknownName = await Profile(null).SpartanFile(user.Id, "secret-name", CancellationToken.None);
        var notRendered = await Profile(null).SpartanFile(user.Id, "stance-glb", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            unknownName.AssertProblem(StatusCodes.Status404NotFound, "There is no such file");
            notRendered.AssertProblem(StatusCodes.Status404NotFound, "There is no such file");
        }
    }

    // --- The profile picture's Spartan headshot ---

    [Test]
    public async Task GetUserSpartanHeadshot_GivesTheNeutralHead_CachedForFiveMinutes_WithAnETag()
    {
        var user = await UserAsync();
        await StoreHeadshotAsync(user.Id, Start.UtcDateTime);
        var controller = Pictures();

        var result = await controller.GetUserSpartanHeadshot(user.Id, new XboxImageService(Db), CancellationToken.None);

        var file = (FileContentResult)result;
        using (Assert.EnterMultipleScope())
        {
            Assert.That((file.FileContents, file.ContentType), Is.EqualTo((Head, "image/png")));
            Assert.That(file.EntityTag, Is.Not.Null);
            Assert.That(file.LastModified, Is.EqualTo(new DateTimeOffset(Start.UtcDateTime)));
            Assert.That(controller.Response.Headers.CacheControl.ToString(), Is.EqualTo("public, max-age=300"));
        }
    }

    [Test]
    public async Task GetUserSpartanHeadshot_WithItsETag_Answers304()
    {
        var user = await UserAsync();
        await StoreHeadshotAsync(user.Id, Start.UtcDateTime);
        var first = (FileContentResult)await Pictures().GetUserSpartanHeadshot(user.Id, new XboxImageService(Db), CancellationToken.None);
        var controller = Pictures();
        controller.Request.Headers.IfNoneMatch = first.EntityTag!.ToString();

        var result = await controller.GetUserSpartanHeadshot(user.Id, new XboxImageService(Db), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(((StatusCodeResult)result).StatusCode, Is.EqualTo(StatusCodes.Status304NotModified));
            Assert.That(controller.Response.Headers.ETag.ToString(), Is.EqualTo(first.EntityTag.ToString()));
        }
    }

    [Test]
    public async Task GetUserSpartanHeadshot_WhenAnotherFileIsRenderedOnly_OrTheHeadshotVanishes_IsNotFound()
    {
        var user = await UserAsync();
        await StoreHeadshotAsync(user.Id, Start.UtcDateTime, "neutral-body");
        var other = await UserAsync("other");
        await StoreHeadshotAsync(other.Id, Start.UtcDateTime);

        var none = await Pictures().GetUserSpartanHeadshot(user.Id, new XboxImageService(Db), CancellationToken.None);
        var vanished = await Pictures().GetUserSpartanHeadshot(other.Id, new VanishingHeadshots(Db), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(none, Is.TypeOf<NotFoundResult>());
            Assert.That(vanished, Is.TypeOf<NotFoundResult>());
        }
    }

    [Test]
    public async Task GetUserProfilePicture_GivesTheSpartanHeadshot_ByDefault_EvenWhenTheEmblemIsStored()
    {
        var user = await UserAsync(xuid: 5);
        await StoreEmblemAsync(5);
        await StoreHeadshotAsync(user.Id, Start.UtcDateTime);

        var result = await Pictures().GetUserProfilePicture(user.Id, new XboxImageService(Db), CancellationToken.None);

        Assert.That(((FileContentResult)result).FileContents, Is.EqualTo(Head));
    }

    [Test]
    public async Task GetUserProfilePicture_FallsBackToTheEmblem_UntilTheSpartanIsRendered()
    {
        var user = await UserAsync(xuid: 5);
        await StoreEmblemAsync(5);

        var result = await Pictures().GetUserProfilePicture(user.Id, new XboxImageService(Db), CancellationToken.None);

        Assert.That(((FileContentResult)result).FileContents, Is.EqualTo(Emblem));
    }

    [Test]
    public async Task GetUserProfilePicture_IgnoresTheHeadshot_WhenTheUserChoseAnotherImage()
    {
        var user = await UserAsync(xuid: 5);
        await StoreEmblemAsync(5);
        await StoreHeadshotAsync(user.Id, Start.UtcDateTime);
        user.ChooseProfilePicture(ProfilePictureChoice.Emblem);
        await Db.SaveChangesAsync();

        var result = await Pictures().GetUserProfilePicture(user.Id, new XboxImageService(Db), CancellationToken.None);

        Assert.That(((FileContentResult)result).FileContents, Is.EqualTo(Emblem));
    }

    [Test]
    public async Task GetUserProfilePicture_ForAUserWithNoXboxAccount_GivesTheHeadshot_OrIsNotFound()
    {
        var withHead = await UserAsync("withhead");
        await StoreHeadshotAsync(withHead.Id, Start.UtcDateTime);
        var without = await UserAsync("without");

        var head = await Pictures().GetUserProfilePicture(withHead.Id, new XboxImageService(Db), CancellationToken.None);
        var none = await Pictures().GetUserProfilePicture(without.Id, new XboxImageService(Db), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(((FileContentResult)head).FileContents, Is.EqualTo(Head));
            Assert.That(none, Is.TypeOf<NotFoundResult>());
        }
    }

    [Test]
    public async Task GetProfilePicture_OfAnAccount_GivesItsUsersSpartanHeadshot_ByDefault()
    {
        var user = await UserAsync(xuid: 5);
        await StoreEmblemAsync(5);
        await StoreHeadshotAsync(user.Id, Start.UtcDateTime);

        var result = await Pictures().GetProfilePicture(5, new XboxImageService(Db), CancellationToken.None);

        Assert.That(((FileContentResult)result).FileContents, Is.EqualTo(Head));
    }

    [Test]
    public async Task GetProfilePicture_OfAnAccountWithNoUser_GivesTheEmblem()
    {
        Db.XboxUsers.Add(XboxUser.FromXbox(7, "Nobody"));
        await Db.SaveChangesAsync();
        await StoreEmblemAsync(7);

        var result = await Pictures().GetProfilePicture(7, new XboxImageService(Db), CancellationToken.None);

        Assert.That(((FileContentResult)result).FileContents, Is.EqualTo(Emblem));
    }
}
