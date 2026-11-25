using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Server.XboxAccounts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Test;

/// <summary>An Xbox account's stored gamer picture and emblem, as the two image endpoints give them.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class XboxImageEndpointsUnitTests : SqliteGrifballContextFixture
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1];

    private XboxAccountsController Controller() => new(new XboxAccountService(Db))
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
    };

    private async Task StoreAsync(long xuid, XboxUserImageKind kind, XboxUserImageStatus status = XboxUserImageStatus.Found)
    {
        Db.XboxUsers.Add(XboxUser.FromXbox(xuid, "Has Images"));
        Db.XboxUserImages.Add(new XboxUserImage
        {
            XboxUserID = xuid,
            Kind = kind,
            Status = status,
            Content = status == XboxUserImageStatus.Found ? Png : null,
            ContentType = status == XboxUserImageStatus.Found ? "image/png" : null,
            CheckedAt = DateTime.UtcNow,
        });
        await Db.SaveChangesAsync();
    }

    [Test]
    public async Task GetGamerPicture_GivesTheStoredImage_CachedForADay()
    {
        await StoreAsync(5, XboxUserImageKind.GamerPicture);
        var controller = Controller();

        var result = await controller.GetGamerPicture(5, new XboxImageService(Db), CancellationToken.None);

        var file = (FileContentResult)result;
        using (Assert.EnterMultipleScope())
        {
            Assert.That((file.FileContents, file.ContentType), Is.EqualTo((Png, "image/png")));
            Assert.That(controller.Response.Headers.CacheControl.ToString(), Is.EqualTo("public, max-age=86400"));
        }
    }

    [Test]
    public async Task GetGamerPicture_CarriesWhenItWasChecked_AsLastModified()
    {
        await StoreAsync(5, XboxUserImageKind.GamerPicture);

        var file = (FileContentResult)await Controller().GetGamerPicture(5, new XboxImageService(Db), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(file.LastModified, Is.Not.Null);
            Assert.That(file.EntityTag, Is.Not.Null);
        }
    }

    [Test]
    public async Task GetGamerPicture_WithItsETag_Answers304_AndNoBody()
    {
        await StoreAsync(5, XboxUserImageKind.GamerPicture);
        var first = (FileContentResult)await Controller().GetGamerPicture(5, new XboxImageService(Db), CancellationToken.None);
        var controller = Controller();
        controller.Request.Headers.IfNoneMatch = first.EntityTag!.ToString();

        var result = await controller.GetGamerPicture(5, new XboxImageService(Db), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(((StatusCodeResult)result).StatusCode, Is.EqualTo(StatusCodes.Status304NotModified));
            Assert.That(controller.Response.Headers.ETag.ToString(), Is.EqualTo(first.EntityTag.ToString()));
        }
    }

    [Test]
    public async Task GetGamerPicture_WithAnotherETag_GivesTheImage()
    {
        await StoreAsync(5, XboxUserImageKind.GamerPicture);
        var controller = Controller();
        controller.Request.Headers.IfNoneMatch = "\"1\"";

        var result = await controller.GetGamerPicture(5, new XboxImageService(Db), CancellationToken.None);

        Assert.That(result, Is.TypeOf<FileContentResult>());
    }

    [Test]
    public async Task GetGamerPicture_WhenTheImageIsGoneByTheTimeItIsRead_IsNotFound()
    {
        await StoreAsync(5, XboxUserImageKind.GamerPicture);

        var result = await Controller().GetGamerPicture(5, new VanishingImages(Db), CancellationToken.None);

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    private sealed class VanishingImages(GrifballWebApp.Application.Database.IApplicationDbContext db) : XboxImageService(db)
    {
        public override Task<XboxImageContent?> GetAsync(long xboxUserID, XboxUserImageKind kind, CancellationToken ct = default)
            => Task.FromResult<XboxImageContent?>(null);
    }

    [Test]
    public async Task GetUserGamerPicture_GivesTheImageOfTheUsersXboxAccount()
    {
        await StoreAsync(5, XboxUserImageKind.GamerPicture);
        var user = User.Seed(userName: "linked", xboxUserID: 5);
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        var result = await Controller().GetUserGamerPicture(user.Id, new XboxImageService(Db), CancellationToken.None);

        Assert.That(((FileContentResult)result).FileContents, Is.EqualTo(Png));
    }

    [Test]
    public async Task GetUserHaloEmblem_GivesTheEmblem_AndIsNotFoundForAUserWithNoXboxAccount()
    {
        await StoreAsync(5, XboxUserImageKind.HaloEmblem);
        var linked = User.Seed(userName: "linked", xboxUserID: 5);
        var unlinked = User.Seed(userName: "unlinked");
        Db.Users.AddRange(linked, unlinked);
        await Db.SaveChangesAsync();

        var emblem = await Controller().GetUserHaloEmblem(linked.Id, new XboxImageService(Db), CancellationToken.None);
        var none = await Controller().GetUserHaloEmblem(unlinked.Id, new XboxImageService(Db), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(emblem, Is.TypeOf<FileContentResult>());
            Assert.That(none, Is.TypeOf<NotFoundResult>());
        }
    }

    [Test]
    public async Task TheUserImageEndpoints_KeepAUsersImageForMinutes_NotADay()
    {
        await StoreAsync(5, XboxUserImageKind.GamerPicture);
        var user = User.Seed(userName: "linked", xboxUserID: 5);
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        var byUser = Controller();
        var byAccount = Controller();

        await byUser.GetUserGamerPicture(user.Id, new XboxImageService(Db), CancellationToken.None);
        await byAccount.GetGamerPicture(5, new XboxImageService(Db), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(byUser.Response.Headers.CacheControl.ToString(), Is.EqualTo("public, max-age=300"));
            Assert.That(byAccount.Response.Headers.CacheControl.ToString(), Is.EqualTo("public, max-age=86400"));
        }
    }

    [Test]
    public async Task GetUserGamerPicture_WithItsETag_Answers304()
    {
        await StoreAsync(5, XboxUserImageKind.GamerPicture);
        var user = User.Seed(userName: "linked", xboxUserID: 5);
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        var first = (FileContentResult)await Controller().GetUserGamerPicture(user.Id, new XboxImageService(Db), CancellationToken.None);
        var controller = Controller();
        controller.Request.Headers.IfNoneMatch = first.EntityTag!.ToString();

        var result = await controller.GetUserGamerPicture(user.Id, new XboxImageService(Db), CancellationToken.None);

        Assert.That(((StatusCodeResult)result).StatusCode, Is.EqualTo(StatusCodes.Status304NotModified));
    }

    [Test]
    public async Task GetUserGamerPicture_IsNotFound_AndBrieflyCached_ForAUserWhoseAccountHasNoImage()
    {
        Db.XboxUsers.Add(XboxUser.FromXbox(5, "No Images"));
        var user = User.Seed(userName: "linked", xboxUserID: 5);
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        var controller = Controller();

        var result = await controller.GetUserGamerPicture(user.Id, new XboxImageService(Db), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.TypeOf<NotFoundResult>());
            Assert.That(controller.Response.Headers.CacheControl.ToString(), Is.EqualTo("public, max-age=300"));
        }
    }

    [Test]
    public async Task GetHaloEmblem_GivesTheStoredEmblem_NotTheGamerPicture()
    {
        await StoreAsync(5, XboxUserImageKind.HaloEmblem);

        var emblem = await Controller().GetHaloEmblem(5, new XboxImageService(Db), CancellationToken.None);
        var picture = await Controller().GetGamerPicture(5, new XboxImageService(Db), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(emblem, Is.TypeOf<FileContentResult>());
            Assert.That(picture, Is.TypeOf<NotFoundResult>());
        }
    }

    [Test]
    public async Task TheImageEndpoints_AreNotFound_ForAnAccountWithNoImageStored()
    {
        await StoreAsync(5, XboxUserImageKind.GamerPicture, XboxUserImageStatus.NotFound);

        var none = await Controller().GetGamerPicture(5, new XboxImageService(Db), CancellationToken.None);
        var unknown = await Controller().GetHaloEmblem(404, new XboxImageService(Db), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(none, Is.TypeOf<NotFoundResult>());
            Assert.That(unknown, Is.TypeOf<NotFoundResult>());
        }
    }

    private async Task StoreBothAsync(long xuid)
    {
        await StoreAsync(xuid, XboxUserImageKind.HaloEmblem);
        Db.XboxUserImages.Add(new XboxUserImage { XboxUserID = xuid, Kind = XboxUserImageKind.GamerPicture, Status = XboxUserImageStatus.Found, Content = [0xFF, 0xD8, 0xFF, 1], ContentType = "image/jpeg", CheckedAt = DateTime.UtcNow });
        await Db.SaveChangesAsync();
    }

    [Test]
    public async Task GetUserProfilePicture_GivesTheEmblem_UntilTheUserChoosesTheGamerPicture()
    {
        await StoreBothAsync(5);
        var user = User.Seed(userName: "grif", xboxUser: await Db.XboxUsers.FindAsync(5L));
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        var emblem = (FileContentResult)await Controller().GetUserProfilePicture(user.Id, new XboxImageService(Db), CancellationToken.None);
        user.ChooseProfilePicture(ProfilePictureChoice.GamerPicture);
        await Db.SaveChangesAsync();
        var picture = (FileContentResult)await Controller().GetUserProfilePicture(user.Id, new XboxImageService(Db), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(emblem.ContentType, Is.EqualTo("image/png"));
            Assert.That(picture.ContentType, Is.EqualTo("image/jpeg"));
        }
    }

    [Test]
    public async Task GetUserProfilePicture_FallsBackToTheGamerPicture_WhenThereIsNoEmblem()
    {
        await StoreAsync(5, XboxUserImageKind.GamerPicture);
        var user = User.Seed(userName: "grif", xboxUser: await Db.XboxUsers.FindAsync(5L));
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        var result = await Controller().GetUserProfilePicture(user.Id, new XboxImageService(Db), CancellationToken.None);

        Assert.That(result, Is.TypeOf<FileContentResult>());
    }

    [Test]
    public async Task TheProfilePictureEndpoints_AreNotFound_ForNoAccountOrNoImage()
    {
        var user = User.Seed(userName: "nobody");
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        var noAccount = await Controller().GetUserProfilePicture(user.Id, new XboxImageService(Db), CancellationToken.None);
        var noImage = await Controller().GetProfilePicture(404, new XboxImageService(Db), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(noAccount, Is.TypeOf<NotFoundResult>());
            Assert.That(noImage, Is.TypeOf<NotFoundResult>());
        }
    }

    [Test]
    public async Task GetProfilePicture_OfAnAccount_FollowsItsUsersChoice()
    {
        await StoreBothAsync(5);
        var user = User.Seed(userName: "grif", xboxUser: await Db.XboxUsers.FindAsync(5L));
        user.ChooseProfilePicture(ProfilePictureChoice.GamerPicture);
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        var file = (FileContentResult)await Controller().GetProfilePicture(5, new XboxImageService(Db), CancellationToken.None);

        Assert.That(file.ContentType, Is.EqualTo("image/jpeg"));
    }

    [Test]
    public async Task ProfilePictureService_SavesTheChoice_OrSaysThereIsNoSuchUser()
    {
        var user = User.Seed(userName: "grif");
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        var service = new GrifballWebApp.Application.Profile.ProfilePictureService(Db);

        var saved = await service.Set(user.Id, ProfilePictureChoice.GamerPicture);
        var missing = await service.Set(99999, ProfilePictureChoice.GamerPicture);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved, Is.True);
            Assert.That((await Db.Users.FindAsync(user.Id))!.ProfilePicture, Is.EqualTo(ProfilePictureChoice.GamerPicture));
            Assert.That(missing, Is.False);
        }
    }
}
