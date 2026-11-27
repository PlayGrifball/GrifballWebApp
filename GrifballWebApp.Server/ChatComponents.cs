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

    /// <summary>The rows of buttons for a reply; null when it has none.</summary>
    public static IReadOnlyList<ActionRowProperties>? For(IReadOnlyList<ChatButton> offered, ChatAction? waiting)
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
        return rows.Count == 0 ? null : rows;
    }
}
