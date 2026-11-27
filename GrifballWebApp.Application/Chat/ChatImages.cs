using System.Text.RegularExpressions;
using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Application.Chat;

/// <summary>The pictures of a player the site keeps and a chat may show: nothing else, so no reply can show an image from anywhere else.</summary>
public enum PlayerImageKind
{
    SpartanHeadshot,
    HaloEmblem,
    GamerPicture,
}

/// <summary>One player's picture a chat reply shows.</summary>
public sealed record ChatImage(int UserID, PlayerImageKind Kind);

/// <summary>
/// The pictures in a chat reply: the addresses <c>get_player_image</c> gives (UrlService's, to the API's own endpoints), found in its
/// text. The site's chat shows them where they are; Discord's gets them attached (<see cref="LoadAsync"/>) and the addresses left out.
/// </summary>
public static partial class ChatImages
{
    /// <summary>The part of an address that says which picture (and so its API endpoint).</summary>
    public static string Slug(PlayerImageKind kind) => kind switch
    {
        PlayerImageKind.SpartanHeadshot => "spartan-headshot",
        PlayerImageKind.HaloEmblem => "halo-emblem",
        PlayerImageKind.GamerPicture => "gamer-picture",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    // Not part of a longer word, address or path; the host is whatever the site's address was
    [GeneratedRegex(@"(?<![\w/.-])(?:https?://[^\s/]+)?/api/users/(?<id>\d{1,9})/(?<slug>spartan-headshot|halo-emblem|gamer-picture)(?![\w/-])")]
    private static partial Regex Address();

    /// <summary>The pictures <paramref name="text"/> names, each once, in order.</summary>
    public static IReadOnlyList<ChatImage> In(string text)
        => [.. Address().Matches(text).Select(m => new ChatImage(int.Parse(m.Groups["id"].Value), KindOf(m.Groups["slug"].Value))).Distinct()];

    /// <summary><paramref name="text"/> without the pictures' addresses, and the blanks they leave tidied.</summary>
    public static string Without(string text)
        => Blanks().Replace(Address().Replace(text, ""), " ").Trim();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex Blanks();

    private static PlayerImageKind KindOf(string slug)
        => Enum.GetValues<PlayerImageKind>().Single(k => Slug(k) == slug);

    /// <summary>Whether the site has the picture stored (no bytes read).</summary>
    public static async Task<bool> StoredAsync(XboxImageService images, ChatImage image, CancellationToken ct = default)
    {
        if (image.Kind == PlayerImageKind.SpartanHeadshot)
            return await images.GetSpartanHeadshotVersionAsync(image.UserID, ct) is not null;
        return await images.GetXboxUserIdOfAsync(image.UserID, ct) is { } xboxUserID
            && await images.GetCheckedAtAsync(xboxUserID, XboxKind(image.Kind), ct) is not null;
    }

    /// <summary>The picture's bytes and type; null when the site has none stored.</summary>
    public static async Task<XboxImageContent?> LoadAsync(XboxImageService images, ChatImage image, CancellationToken ct = default)
    {
        if (image.Kind == PlayerImageKind.SpartanHeadshot)
            return await images.GetSpartanHeadshotAsync(image.UserID, ct);
        return await images.GetXboxUserIdOfAsync(image.UserID, ct) is { } xboxUserID
            ? await images.GetAsync(xboxUserID, XboxKind(image.Kind), ct)
            : null;
    }

    private static XboxUserImageKind XboxKind(PlayerImageKind kind)
        => kind == PlayerImageKind.HaloEmblem ? XboxUserImageKind.HaloEmblem : XboxUserImageKind.GamerPicture;
}
