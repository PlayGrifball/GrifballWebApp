using System.ComponentModel;
using GrifballWebApp.Application.Chat;

namespace GrifballWebApp.Application.Tools;

/// <summary>
/// A form the Discord bot's chat makes for answers no existing form asks for (<see cref="ChatForm"/>): shown behind a Fill in button that only
/// the one who asked can open, its answers coming back to the model as their next message. Never a tool run from the answers.
/// </summary>
public class FormTools
{
    private readonly ISlashCommandCatalog _catalog;

    public FormTools(ISlashCommandCatalog catalog)
    {
        _catalog = catalog;
    }

    [Description("Show the person a form under your reply when you need several answers from them at once: a Fill in button opens it, only for them, "
        + "and their answers come back to you as their next message. Not for what offer_button opens (gamertag, signup, queue). 1 to 5 fields, each "
        + "a short text, a paragraph or a choice. One form a reply: only the first is shown. The form is added for you: write nothing for it in your reply.")]
    public string OfferForm([Description("The form's title: at most 45 characters.")] string title, [Description("Its fields, 1 to 5.")] ChatFormField[] fields)
    {
        if (_catalog.Commands().Count == 0)
            throw new ToolException("Forms are only shown by the Discord bot.");
        var form = ChatForms.Trimmed(title, fields) ?? throw new ToolException(ChatForms.Incomplete);
        if (ChatForms.Refusal(form) is { } refusal)
            throw new ToolException(refusal);
        return $"Shown under your reply as a Fill in button: {ChatForms.Token(form)}";
    }
}
