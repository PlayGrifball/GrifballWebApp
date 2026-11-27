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
/// The buttons of a chat reply: the token <c>offer_button</c> returns names one, and the reply reads it from that tool's result
/// (<see cref="ChatReply.Offered"/>), never from the reply's text. Discord's host shows each under the reply (the click opens the bot's
/// modal or flow, where the clicker is checked).
/// </summary>
public static partial class ChatButtons
{
    /// <summary>The most buttons one reply shows (Discord's row holds five).</summary>
    public const int MaxButtons = 5;

    private static readonly Dictionary<ChatButtonKind, string> Names = new()
    {
        [ChatButtonKind.SetGamertag] = "set_gamertag",
        [ChatButtonKind.SignupDetails] = "signup_details",
        [ChatButtonKind.Signup] = "signup",
        [ChatButtonKind.JoinQueue] = "join_queue",
    };

    /// <summary>The token a button is written as in <c>offer_button</c>'s result.</summary>
    public static string Token(ChatButton button)
        => button.SeasonID is { } season ? $"[button:{Names[button.Kind]}:{season}]" : $"[button:{Names[button.Kind]}]";

    [GeneratedRegex(@"\[button:(?<kind>[a-z_]+)(?::(?<season>\d{1,9}))?\]")]
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
}
