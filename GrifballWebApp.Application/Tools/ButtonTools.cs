using System.ComponentModel;
using GrifballWebApp.Application.Chat;

namespace GrifballWebApp.Application.Tools;

/// <summary>Buttons the Discord bot's chat shows under its reply: the click opens the bot's own modal or flow for the person who clicks.</summary>
public class ButtonTools
{
    private readonly ISlashCommandCatalog _catalog;

    public ButtonTools(ISlashCommandCatalog catalog)
    {
        _catalog = catalog;
    }

    [Description("Offer the person a button under your reply that does something for them in Discord: set their gamertag (opens a form), sign up for a season, "
        + "set their signup details and times (opens a form), or join the matchmaking queue. Use it when they ask to do one of these, or you would otherwise tell them "
        + "to type a command. Give the token this returns in your reply exactly as it is, on its own: the chat turns it into the button. Never write a token of your own.")]
    public string OfferButton([Description("What the button does.")] ChatButtonKind kind,
        [Description("The season's ID, for signup and signup_details buttons (list_seasons gives it).")] int? seasonID = null)
    {
        if (_catalog.Commands().Count == 0)
            throw new ToolException("Buttons are only shown by the Discord bot.");
        if (ChatButton.NeedsSeason(kind) && seasonID is null)
            throw new ToolException($"A {kind} button needs the season's ID.");
        if (!ChatButton.NeedsSeason(kind) && seasonID is not null)
            throw new ToolException($"A {kind} button is not about a season: leave the season ID out.");
        return ChatButtons.Token(new ChatButton(kind, seasonID));
    }
}
