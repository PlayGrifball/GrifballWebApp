using DiscordInterfaces;
using NetCord;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Commands;

/// <summary>NetCord entry point for the Set gamertag user command's modal (the target's Discord ID rides in its custom ID).</summary>
public class UserCommandModals : ComponentInteractionModule<ModalInteractionContext>
{
    private readonly UserCommandsHandler _handler;

    public UserCommandModals(UserCommandsHandler handler)
    {
        _handler = handler;
    }

    [ComponentInteraction(UserCommandsHandler.ModalPrefix)]
    public Task SetGamertag(ulong targetDiscordUserId, string targetUsername)
        => _handler.SetGamertag(Context.ToDiscordContext(), targetDiscordUserId, targetUsername, ((TextInput)((Label)Context.Components[0]).Component).Value);
}
