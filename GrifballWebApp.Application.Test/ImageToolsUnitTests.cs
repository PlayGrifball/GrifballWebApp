using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Domain.Players;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>The pictures a chat gives (<see cref="ImageTools"/>): an address only for one the site has stored.</summary>
[TestFixture]
internal sealed class ImageToolsUnitTests : ApplicationDbContextTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
    private int _userID;
    private int _bareUserID;

    [SetUp]
    public async Task SetUp()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(500, "Grifter"));
        var user = User.Seed(userName: "grif", xboxUserID: 500);
        var bare = User.Seed(userName: "bare");
        Context.Users.AddRange(user, bare);
        Context.XboxUserImages.Add(new XboxUserImage { XboxUserID = 500, Kind = XboxUserImageKind.HaloEmblem, Status = XboxUserImageStatus.Found, ContentType = "image/png", Content = [1], CheckedAt = _time.GetUtcNow().UtcDateTime });
        Context.XboxUserImages.Add(new XboxUserImage { XboxUserID = 500, Kind = XboxUserImageKind.GamerPicture, Status = XboxUserImageStatus.NotFound, CheckedAt = _time.GetUtcNow().UtcDateTime });
        await Context.SaveChangesAsync();
        _userID = user.Id;
        _bareUserID = bare.Id;
    }

    private ImageTools Tools(bool relative = false)
        => new(new ToolCaller(null, []) { RelativeLinks = relative }, new UrlService("https://grif.test"), new XboxImageService(Context));

    [Test]
    public async Task GetPlayerImage_OfAStoredPicture_IsTheApiAddress_WholeOrAsAPath()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Tools().GetPlayerImage(_userID, PlayerImageKind.HaloEmblem), Is.EqualTo($"https://grif.test/api/users/{_userID}/halo-emblem"));
            Assert.That(await Tools(relative: true).GetPlayerImage(_userID, PlayerImageKind.HaloEmblem), Is.EqualTo($"/api/users/{_userID}/halo-emblem"));
        }
    }

    [TestCase(PlayerImageKind.GamerPicture)]
    [TestCase(PlayerImageKind.SpartanHeadshot)]
    public async Task GetPlayerImage_OfAPictureTheSiteHasNot_Refuses(PlayerImageKind kind)
        => await Assert.ThatAsync(() => Tools().GetPlayerImage(_userID, kind), Throws.InstanceOf<ToolException>());

    [Test]
    public async Task GetPlayerImage_OfAPlayerWithNoAccountOrNoSuchPlayer_Refuses()
    {
        using (Assert.EnterMultipleScope())
        {
            await Assert.ThatAsync(() => Tools().GetPlayerImage(_bareUserID, PlayerImageKind.HaloEmblem), Throws.InstanceOf<ToolException>());
            await Assert.ThatAsync(() => Tools().GetPlayerImage(9999, PlayerImageKind.HaloEmblem), Throws.InstanceOf<ToolException>());
        }
    }

    [Test]
    public async Task GetPlayerImage_Refusal_NamesThePictureByItsSlug_AndSaysWhyWhenItCan()
    {
        var noPicture = await Assert.ThrowsAsync<ToolException>(() => Tools().GetPlayerImage(_userID, PlayerImageKind.GamerPicture));
        var noAccount = await Assert.ThrowsAsync<ToolException>(() => Tools().GetPlayerImage(_bareUserID, PlayerImageKind.HaloEmblem));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(noPicture!.Message, Does.Contain("gamer-picture").And.Not.Contain("GamerPicture"));
            Assert.That(noAccount!.Message, Does.Contain("halo-emblem").And.Contain("no Xbox account").And.Not.Contain("HaloEmblem"));
        }
    }
}
