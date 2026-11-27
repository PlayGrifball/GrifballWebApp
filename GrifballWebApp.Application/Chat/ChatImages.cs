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

    // Not part of a longer word, address or path; the host (if any) is checked against the site's own, the slug against Slug
    private const string Core = @"(?<![\w/.-])(?<host>https?://[^\s/<>()`\]]+)?/api/users/(?<id>\d{1,9})/(?<slug>[a-z]+(?:-[a-z]+)*)(?![\w/-])";

    // The address with the blanks around it, and the Markdown it may sit in (an image, a link, <>, backticks): they go with it
    [GeneratedRegex(@"[ \t]*(?:!?\[[^\]\n]*\]\(<?" + Core + @">?\)|<" + Core + ">|`" + Core + "`|" + Core + @")[ \t]*")]
    private static partial Regex Address();

    /// <summary>The pictures <paramref name="text"/> names, each once, in order: only paths, and addresses on the site at <paramref name="baseUrl"/> (empty for none).</summary>
    public static IReadOnlyList<ChatImage> In(string text, string baseUrl)
        => [.. Address().Matches(text).Select(m => Own(m, baseUrl)).OfType<ChatImage>().Distinct()];

    /// <summary>
    /// <paramref name="text"/> without the pictures' addresses (as <see cref="In"/> finds them) and the Markdown left empty around them; only the
    /// blanks next to an address are tidied, the rest of the text is as it was.
    /// </summary>
    public static string Without(string text, string baseUrl)
    {
        var removed = false;
        var result = Address().Replace(text, m =>
        {
            if (Own(m, baseUrl) is null)
                return m.Value;
            removed = true;
            var end = m.Index + m.Length;
            var blanks = m.Value.StartsWith(' ') || m.Value.StartsWith('\t') || m.Value.EndsWith(' ') || m.Value.EndsWith('\t');
            var before = m.Index == 0 ? '\n' : text[m.Index - 1];
            var after = end >= text.Length ? '\n' : text[end];
            // Words either side stay apart by one space; a line's edge or punctuation closes up
            return blanks && before != '\n' && after != '\n' && !",.;:!?)".Contains(after) ? " " : "";
        });
        return removed ? result.Trim() : text;
    }

    /// <summary>The picture a match names, if it is one: a path or an address on the site, with a slug the site has.</summary>
    private static ChatImage? Own(Match match, string baseUrl)
    {
        if (match.Groups["host"] is { Success: true } host
            && !(Uri.TryCreate(baseUrl, UriKind.Absolute, out var site) && Uri.TryCreate(host.Value, UriKind.Absolute, out var given)
                && string.Equals(site.Authority, given.Authority, StringComparison.OrdinalIgnoreCase)))
            return null;
        var slug = match.Groups["slug"].Value;
        foreach (var kind in Enum.GetValues<PlayerImageKind>())
            if (Slug(kind) == slug)
                return new ChatImage(int.Parse(match.Groups["id"].Value), kind);
        return null;
    }

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
