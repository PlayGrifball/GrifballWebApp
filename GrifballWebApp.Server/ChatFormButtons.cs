using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Application.Chat;
using GrifballWebApp.Infrastructure.Discord;
using GrifballWebApp.Server.Commands;
using GrifballWebApp.Server.Extensions;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using RadioGroupOptionProperties = NetCord.Rest.RadioGroupOptionProperties;
using RadioGroupProperties = NetCord.Rest.RadioGroupProperties;

namespace GrifballWebApp.Server;

/// <summary>A chat form (<see cref="ChatForm"/>) as a Discord modal, and its submit's answers read back, one per field.</summary>
public static class ChatFormModals
{
    /// <summary>The custom ID of field <paramref name="index"/>'s input.</summary>
    public static string Input(int index) => $"field{index}";

    /// <summary>The modal of form <paramref name="formID"/>: a label per field around its text input or its radio group of choices.</summary>
    public static ModalProperties Build(string formID, ChatForm form)
        => new(DiscordModalsContants.ChatFormAnswersWithParams(formID), form.Title,
            [.. form.Fields.Select((field, i) => (IModalComponentProperties)new LabelProperties(field.Label, Component(field, i)))]);

    private static ILabelComponentProperties Component(ChatFormField field, int index) => field.Kind is ChatFormFieldKind.Choice
        ? new RadioGroupProperties(Input(index), [.. field.Choices!.Select(c => new RadioGroupOptionProperties(c, c))]) { Required = field.Required }
        : new TextInputProperties(Input(index), field.Kind is ChatFormFieldKind.Paragraph ? TextInputStyle.Paragraph : TextInputStyle.Short)
        {
            Required = field.Required,
            MaxLength = ChatForms.MaxLength(field),
            Placeholder = field.Placeholder,
        };

    /// <summary>The submitted answers, one per field of <paramref name="form"/>: null where none was given.</summary>
    public static IReadOnlyList<string?> Answers(ChatForm form, IEnumerable<IModalComponent> components)
        => [.. form.Fields.Select((field, i) => field.Kind is ChatFormFieldKind.Choice ? ModalAnswers.Picked(components, Input(i)) : ModalAnswers.Text(components, Input(i)))];
}

/// <summary>
/// A chat form's Fill in button and its modal's submit (<see cref="ChatForms"/>): only its asker opens it (the one the reply answered, as
/// <see cref="ChatLimits.Asker"/> names them), and its answers, checked against the form, are posted, then read by the model as their next turn
/// (<see cref="MessageCreateHandler.HandleFormAsync"/>). Nothing runs from the answers themselves. Off with <see cref="ChatForms.EnabledKey"/>.
/// </summary>
public class ChatFormHandler
{
    public const string Gone = "This form isn't open any more: ask again.";
    public const string NotYours = "This form is for the person it was made for.";
    public const string TurnedOff = "Forms are turned off.";

    private readonly IChatForms _forms;
    private readonly DiscordCaller _caller;
    private readonly MessageCreateHandler _chat;
    private readonly IConfiguration _configuration;

    public ChatFormHandler(IChatForms forms, DiscordCaller caller, MessageCreateHandler chat, IConfiguration configuration)
    {
        _forms = forms;
        _caller = caller;
        _chat = chat;
        _configuration = configuration;
    }

    /// <summary>The Fill in button: the form's modal, for its asker.</summary>
    public async Task Open(IDiscordInteractionContext context, string formID)
    {
        if (await Held(context, formID) is { } held)
            await context.Interaction.SendResponseAsync(InteractionCallback.Modal(ChatFormModals.Build(formID, held.Form)));
    }

    /// <summary>
    /// The modal's submit: answers that don't fit the form are refused (it stays open); else the form is taken (answered once), its answers
    /// posted as the response, and the model answers them in reply to that message.
    /// </summary>
    public async Task Submit(IDiscordModalInteractionContext context, string formID)
    {
        if (await Held(context, formID) is not { } held)
            return;
        var answers = ChatFormModals.Answers(held.Form, context.Components);
        if (ChatForms.Invalid(held.Form, answers) is { } invalid)
        {
            await context.EphemeralResponse(invalid);
            return;
        }
        if (await _forms.TakeAsync(formID) is null)
        {
            await context.EphemeralResponse(Gone);
            return;
        }
        // Posted by the bot, so nothing in it pings anyone or previews a link (ChatForms.Answers shows the text as typed)
        var text = ChatForms.Answers(held.Form, answers);
        await context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = text,
            AllowedMentions = AllowedMentionsProperties.None,
            Flags = MessageFlags.SuppressEmbeds,
        }));
        var posted = await context.Interaction.GetResponseAsync();
        // Refused (a reply under way, the limits): the form waits again, to be sent once the chat can take it
        await _chat.HandleFormAsync(posted.Original, context.Interaction.User.Id, context.Interaction.User.Username, text, held.Direct,
            () => _forms.SaveAsync(formID, held, ChatForms.Lasts));
    }

    /// <summary>Form <paramref name="formID"/> while forms are on and it waits for the one who clicked; null with them told why.</summary>
    private async Task<HeldForm?> Held(IDiscordInteractionContext context, string formID)
    {
        var refusal = Gone;
        HeldForm? held = null;
        if (!_configuration.GetValue(ChatForms.EnabledKey, true))
            refusal = TurnedOff;
        else if (await _forms.PeekAsync(formID) is { } found)
        {
            var user = context.Interaction.User.Id;
            if (found.Asker == ChatLimits.Asker(await _caller.AsToolCaller(user), ChatLimits.Discord(user)))
                held = found;
            else
                refusal = NotYours;
        }
        if (held is null)
            await context.EphemeralResponse(refusal);
        return held;
    }
}

/// <summary>
/// NetCord entry point for a chat form's Fill in button: wraps the context and hands off to <see cref="ChatFormHandler"/>. Keep logic out of
/// here, it is only tested end to end (DiscordTransportTests).
/// </summary>
public class ChatFormButtonInteractions : ComponentInteractionModule<ButtonInteractionContext>
{
    private readonly ChatFormHandler _handler;

    public ChatFormButtonInteractions(ChatFormHandler handler)
    {
        _handler = handler;
    }

    [ComponentInteraction(DiscordButtonContants.ChatForm)]
    public Task Open(string formId) => _handler.Open(Context.ToDiscordContext(), formId);
}

/// <summary>
/// NetCord entry point for a chat form's modal: wraps the context and hands off to <see cref="ChatFormHandler"/>. Keep logic out of here, it
/// is only tested end to end (DiscordTransportTests).
/// </summary>
public class ChatFormModalInteractions : ComponentInteractionModule<ModalInteractionContext>
{
    private readonly ChatFormHandler _handler;

    public ChatFormModalInteractions(ChatFormHandler handler)
    {
        _handler = handler;
    }

    [ComponentInteraction(DiscordModalsContants.ChatFormAnswers)]
    public Task Submit(string formId) => _handler.Submit(Context.ToDiscordContext(), formId);
}
