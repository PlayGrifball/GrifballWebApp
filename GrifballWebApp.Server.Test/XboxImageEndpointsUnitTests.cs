using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Domain.Players;
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

        Assert.That((file.LastModified, file.EntityTag), Has.None.Null);
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
}
