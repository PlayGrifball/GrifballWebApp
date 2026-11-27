using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Infrastructure.Discord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server;

/// <summary>
/// The Confirm and Cancel buttons of a DM reply that waits for the person's yes or no: a click is their answer, as typing yes or no
/// is (<see cref="MessageCreateHandler.HandleClickAsync"/>). The buttons go at once, so the answer is given once.
/// </summary>
public class ChatConfirmButtonHandler
{
    private readonly MessageCreateHandler _chat;

    public ChatConfirmButtonHandler(MessageCreateHandler chat)
    {
        _chat = chat;
    }

    public async Task Click(IDiscordButtonInteractionContext context, string actionId, bool approved)
    {
        await context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(x => x.WithComponents([])));
        await _chat.HandleClickAsync(context.Message.Original, context.User.Id, context.User.Username, new MessageCreateHandler.ChatClick(actionId, approved));
    }
}

/// <summary>
/// NetCord entry point for the chat's Confirm and Cancel buttons: wraps the context and hands off to <see cref="ChatConfirmButtonHandler"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
public class ChatConfirmButtonInteractions : ComponentInteractionModule<ButtonInteractionContext>
{
    private readonly ChatConfirmButtonHandler _handler;

    public ChatConfirmButtonInteractions(ChatConfirmButtonHandler handler)
    {
        _handler = handler;
    }

    [ComponentInteraction(DiscordButtonContants.ChatConfirm)]
    public async Task ChatConfirm(string actionId, string answer)
    {
        await _handler.Click(Context.ToDiscordContext(), actionId, answer == "yes");
    }
}
