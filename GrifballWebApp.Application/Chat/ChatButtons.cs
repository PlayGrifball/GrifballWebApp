using System.Text.RegularExpressions;

namespace GrifballWebApp.Application.Chat;

/// <summary>What a button offered in a Discord chat reply opens: the bot's own flows, never anything the model makes up.</summary>
public enum ChatButtonKind
{
    SetGamertag,
    SignupDetails,
    Signup,
    JoinQueue,
}

/// <summary>One button of a chat reply: what it opens, and the season it is about (the signup ones).</summary>
public sealed record ChatButton(ChatButtonKind Kind, int? SeasonID)
{
    /// <summary>Whether the button is about a season (<see cref="SeasonID"/> is given).</summary>
    public static bool NeedsSeason(ChatButtonKind kind) => kind is ChatButtonKind.Signup or ChatButtonKind.SignupDetails;
}

/// <summary>
/// The buttons in a chat reply: the tokens <c>offer_button</c> gives, found in its text. Discord's host turns each into a button under the
/// reply (the click opens the bot's modal or flow, where the clicker is checked) and leaves the token out of the text.
/// </summary>
public static partial class ChatButtons
{
    /// <summary>The most buttons one reply shows (Discord's row holds five).</summary>
    public const int MaxButtons = 5;

    /// <summary>The token a button is written as in a reply.</summary>
    public static string Token(ChatButton button)
        => button.SeasonID is { } season ? $"[button:{Names[button.Kind]}:{season}]" : $"[button:{Names[button.Kind]}]";

    private static readonly Dictionary<ChatButtonKind, string> Names = new()
    {
        [ChatButtonKind.SetGamertag] = "set_gamertag",
        [ChatButtonKind.SignupDetails] = "signup_details",
        [ChatButtonKind.Signup] = "signup",
        [ChatButtonKind.JoinQueue] = "join_queue",
    };

    // The token with the blanks around it: they go with it
    [GeneratedRegex(@"[ \t]*\[button:(?<kind>[a-z_]+)(?::(?<season>\d{1,9}))?\][ \t]*")]
    private static partial Regex Pattern();

    private static ChatButton? Parse(Match match)
    {
        foreach (var (kind, name) in Names)
        {
            if (name != match.Groups["kind"].Value)
                continue;
            var season = match.Groups["season"].Success ? int.Parse(match.Groups["season"].Value) : (int?)null;
            return ChatButton.NeedsSeason(kind) == (season is not null) ? new ChatButton(kind, season) : null;
        }
        return null;
    }

    /// <summary>The buttons <paramref name="text"/> names, each once, in order, at most <see cref="MaxButtons"/>.</summary>
    public static IReadOnlyList<ChatButton> In(string text)
        => [.. Pattern().Matches(text).Select(Parse).OfType<ChatButton>().Distinct().Take(MaxButtons)];

    /// <summary><paramref name="text"/> without the tokens (as <see cref="In"/> finds them); the rest is as it was.</summary>
    public static string Without(string text)
    {
        var removed = false;
        var result = Pattern().Replace(text, m =>
        {
            if (Parse(m) is null)
                return m.Value;
            removed = true;
            var end = m.Index + m.Length;
            var blanks = m.Value.StartsWith(' ') || m.Value.StartsWith('\t') || m.Value.EndsWith(' ') || m.Value.EndsWith('\t');
            var before = m.Index == 0 ? '\n' : text[m.Index - 1];
            var after = end >= text.Length ? '\n' : text[end];
            return blanks && before != '\n' && after != '\n' && !",.;:!?)".Contains(after) ? " " : "";
        });
        return removed ? result.Trim() : text;
    }
}
