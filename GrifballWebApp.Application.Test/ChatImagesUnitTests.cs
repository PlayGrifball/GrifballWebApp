using GrifballWebApp.Application.Chat;

namespace GrifballWebApp.Application.Test;

/// <summary>The pictures a chat reply names (<see cref="ChatImages"/>): only the site's own picture addresses, each once.</summary>
[TestFixture]
internal sealed class ChatImagesUnitTests
{
    [Test]
    public void In_FindsEachOfAPlayersPictures_AsPathOrWholeAddress_OnceEach()
    {
        var found = ChatImages.In("A /api/users/3/spartan-headshot, https://grif.test/api/users/3/halo-emblem and /api/users/4/gamer-picture, again /api/users/3/spartan-headshot");

        Assert.That(found, Is.EqualTo(new[]
        {
            new ChatImage(3, PlayerImageKind.SpartanHeadshot),
            new ChatImage(3, PlayerImageKind.HaloEmblem),
            new ChatImage(4, PlayerImageKind.GamerPicture),
        }));
    }

    [TestCase("/api/users/3/profile-picture")]
    [TestCase("/api/users/x/halo-emblem")]
    [TestCase("/api/users/3/halo-emblem/more")]
    [TestCase("/api/users/3/halo-emblems")]
    [TestCase("x/api/users/3/halo-emblem")]
    [TestCase("/api/seasons")]
    [TestCase("/pictures/halo-emblem")]
    public void In_IgnoresEveryOtherAddress(string text)
        => Assert.That(ChatImages.In(text), Is.Empty);

    [Test]
    public void Without_DropsThePictureAddresses_AndTidiesTheBlanks()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatImages.Without("Here: /api/users/3/halo-emblem  and https://grif.test/api/users/3/gamer-picture ok"), Is.EqualTo("Here: and ok"));
            Assert.That(ChatImages.Without("/api/users/3/halo-emblem"), Is.Empty);
            Assert.That(ChatImages.Without("No pictures, see /season/12."), Is.EqualTo("No pictures, see /season/12."));
        }
    }

    [TestCase(PlayerImageKind.SpartanHeadshot, "spartan-headshot")]
    [TestCase(PlayerImageKind.HaloEmblem, "halo-emblem")]
    [TestCase(PlayerImageKind.GamerPicture, "gamer-picture")]
    public void Slug_IsTheApiEndpointsName(PlayerImageKind kind, string slug)
        => Assert.That(ChatImages.Slug(kind), Is.EqualTo(slug));
}
