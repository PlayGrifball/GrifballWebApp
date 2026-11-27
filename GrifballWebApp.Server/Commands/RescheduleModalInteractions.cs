using DiscordInterfaces;
using GrifballWebApp.Infrastructure.Discord;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// NetCord entry point for /reschedule request's modal (its reason left out): wraps the context and hands off to
/// <see cref="PersonalCommandsHandler"/>, which checks the caller again. Keep logic out of here, it is only tested end to end
/// (DiscordTransportTests).
/// </summary>
public class RescheduleModalInteractions : ComponentInteractionModule<ModalInteractionContext>
{
    private readonly PersonalCommandsHandler _handler;

    public RescheduleModalInteractions(PersonalCommandsHandler handler)
    {
        _handler = handler;
    }

    [ComponentInteraction(DiscordModalsContants.RescheduleRequest)]
    public Task RescheduleRequest(int matchId) => _handler.SaveRescheduleRequest(Context.ToDiscordContext(), matchId);
}
