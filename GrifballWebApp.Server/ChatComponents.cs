using GrifballWebApp.Application.Chat;
using GrifballWebApp.Infrastructure.Discord;
using NetCord;
using NetCord.Rest;

namespace GrifballWebApp.Server;

/// <summary>The buttons under a chat reply: Confirm and Cancel for the change waiting for the person, and the ones the model offered.</summary>
public static class ChatComponents
{
    /// <summary>The button's custom id, and its label, in the bot's own flows (their handlers check the one who clicks).</summary>
    public static (string CustomId, string Label) Describe(ChatButton button) => button.Kind switch
    {
        ChatButtonKind.SetGamertag => (DiscordButtonContants.SetGamertag, "Set my gamertag"),
        ChatButtonKind.SignupDetails => (DiscordButtonContants.SignupDetailsWithParams(button.SeasonID!.Value), "Set my details and times"),
        ChatButtonKind.Signup => (DiscordButtonContants.SignupWithParams(button.SeasonID!.Value), "Sign up"),
        _ => (DiscordButtonContants.JoinQueue, "Join the queue"),
    };

    /// <summary>The rows of buttons for a reply, a held form's Fill in button last (<paramref name="form"/>: its ID and title); null when it has none.</summary>
    public static IReadOnlyList<ActionRowProperties>? For(IReadOnlyList<ChatButton> offered, ChatAction? waiting, (string ID, string Title)? form = null)
    {
        var rows = Rows(offered, waiting);
        if (form is { } held)
        {
            // A title is at most 45 characters (ChatForms.MaxTitle): the label fits Discord's 80
            rows.Add(new ActionRowProperties([new ButtonProperties(DiscordButtonContants.ChatFormWithParams(held.ID), $"Fill in: {held.Title}", ButtonStyle.Primary)]));
        }
        return rows.Count == 0 ? null : rows;
    }

    private static List<ActionRowProperties> Rows(IReadOnlyList<ChatButton> offered, ChatAction? waiting)
    {
        var rows = new List<ActionRowProperties>();
        if (waiting is not null)
        {
            rows.Add(new ActionRowProperties([
                new ButtonProperties(DiscordButtonContants.ChatConfirmWithParams(waiting.Id, true), "Confirm", ButtonStyle.Success),
                new ButtonProperties(DiscordButtonContants.ChatConfirmWithParams(waiting.Id, false), "Cancel", ButtonStyle.Danger),
            ]));
        }
        if (offered.Count > 0)
        {
            rows.Add(new ActionRowProperties([.. offered.Select(b =>
            {
                var (id, label) = Describe(b);
                return new ButtonProperties(id, label, ButtonStyle.Primary);
            })]));
        }
        return rows;
    }

    /// <summary>
    /// The rows of a reply once its Confirm and Cancel are answered (but those of <paramref name="keep"/>, a change still waiting): the
    /// offered buttons stay as they were. Only the bot's own buttons are kept, so a row with none of them goes (Discord refuses an empty row).
    /// </summary>
    public static IReadOnlyList<ActionRowProperties> WithoutConfirm(IEnumerable<IMessageComponent> components, string? keep = null)
        => [.. components.OfType<ActionRow>()
            .Select(row => row.Components.OfType<Button>().ToList())
            .Where(buttons => buttons.Count > 0 && !buttons.Exists(b => IsConfirm(b, keep)))
            .Select(buttons => new ActionRowProperties([.. buttons.Select(b => new ButtonProperties(b.CustomId, b.Label!, b.Style))]))];

    /// <summary>Whether a message's rows have Confirm and Cancel (<see cref="DiscordButtonContants.ChatConfirm"/>) of a change other than <paramref name="keep"/>.</summary>
    public static bool HasConfirm(IEnumerable<IMessageComponent> components, string? keep = null)
        => components.OfType<ActionRow>().Any(row => row.Components.OfType<Button>().Any(b => IsConfirm(b, keep)));

    private static bool IsConfirm(Button button, string? keep)
        => button.CustomId.StartsWith($"{DiscordButtonContants.ChatConfirm}:", StringComparison.Ordinal)
            && (keep is null || !button.CustomId.StartsWith($"{DiscordButtonContants.ChatConfirm}:{keep}:", StringComparison.Ordinal));
}
