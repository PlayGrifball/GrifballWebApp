using GrifballWebApp.Application.Chat;

namespace GrifballWebApp.Application.Test;

/// <summary>The pictures a chat reply names (<see cref="ChatImages"/>): only the site's own picture addresses, each once.</summary>
[TestFixture]
internal sealed class ChatImagesUnitTests
{
    private const string Site = "https://grif.test";

    [Test]
    public void In_FindsEachOfAPlayersPictures_AsPathOrTheSitesAddress_OnceEach()
    {
        var found = ChatImages.In("A /api/users/3/spartan-headshot, https://grif.test/api/users/3/halo-emblem and /api/users/4/gamer-picture, again /api/users/3/spartan-headshot", Site);

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
    [TestCase("https://evil.test/api/users/3/halo-emblem")]
    [TestCase("http://grif.test.evil.test/api/users/3/halo-emblem")]
    public void In_IgnoresEveryOtherAddress_AndEveryOtherHost(string text)
        => Assert.That(ChatImages.In(text, Site), Is.Empty);

    [Test]
    public void In_WithNoSiteAddress_FindsOnlyPaths()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatImages.In("/api/users/3/halo-emblem", ""), Has.Count.EqualTo(1));
            Assert.That(ChatImages.In("https://grif.test/api/users/3/halo-emblem", ""), Is.Empty);
        }
    }

    [Test]
    public void Without_DropsThePictureAddresses_AndTidiesTheBlanksAroundThem()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatImages.Without("Here: /api/users/3/halo-emblem  and https://grif.test/api/users/3/gamer-picture ok", Site), Is.EqualTo("Here: and ok"));
            Assert.That(ChatImages.Without("/api/users/3/halo-emblem", Site), Is.Empty);
            Assert.That(ChatImages.Without("No pictures, see /season/12.", Site), Is.EqualTo("No pictures, see /season/12."));
            Assert.That(ChatImages.Without("The emblem /api/users/3/halo-emblem, nice", Site), Is.EqualTo("The emblem, nice"));
        }
    }

    [Test]
    public void Without_KeepsTheRestOfTheTextAsItWas()
    {
        const string list = "- a\n    - nested  two spaces\n\n```\n    indented  code\n```\n/api/users/3/halo-emblem";
        Assert.That(ChatImages.Without(list, Site), Is.EqualTo("- a\n    - nested  two spaces\n\n```\n    indented  code\n```"));
    }

    [Test]
    public void Without_LeavesAnAddressOnAnotherHost()
        => Assert.That(ChatImages.Without("see https://evil.test/api/users/3/halo-emblem", Site), Is.EqualTo("see https://evil.test/api/users/3/halo-emblem"));

    [TestCase("Emblem: ![emblem](/api/users/3/halo-emblem) done", "Emblem: done")]
    [TestCase("Emblem: ![emblem](https://grif.test/api/users/3/halo-emblem) done", "Emblem: done")]
    [TestCase("Emblem: [here](/api/users/3/halo-emblem) done", "Emblem: done")]
    [TestCase("Emblem: </api/users/3/halo-emblem> done", "Emblem: done")]
    [TestCase("Emblem: `/api/users/3/halo-emblem` done", "Emblem: done")]
    public void Without_DropsTheEmptyMarkdownWrapperTheAddressWasIn(string text, string expected)
        => Assert.That(ChatImages.Without(text, Site), Is.EqualTo(expected));

    [TestCase(PlayerImageKind.SpartanHeadshot, "spartan-headshot")]
    [TestCase(PlayerImageKind.HaloEmblem, "halo-emblem")]
    [TestCase(PlayerImageKind.GamerPicture, "gamer-picture")]
    public void Slug_IsTheApiEndpointsName(PlayerImageKind kind, string slug)
        => Assert.That(ChatImages.Slug(kind), Is.EqualTo(slug));
}
