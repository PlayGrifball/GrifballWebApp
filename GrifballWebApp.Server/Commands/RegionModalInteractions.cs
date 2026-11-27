using DiscordInterfaces;
using GrifballWebApp.Infrastructure.Discord;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// NetCord entry point for the region modal (/me region with no region): wraps the context and hands off to
/// <see cref="PersonalCommandsHandler"/>. Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
public class RegionModalInteractions : ComponentInteractionModule<ModalInteractionContext>
{
    private readonly PersonalCommandsHandler _handler;

    public RegionModalInteractions(PersonalCommandsHandler handler)
    {
        _handler = handler;
    }

    [ComponentInteraction(DiscordModalsContants.SetRegion)]
    public Task SetRegion() => _handler.SaveRegion(Context.ToDiscordContext());
}
