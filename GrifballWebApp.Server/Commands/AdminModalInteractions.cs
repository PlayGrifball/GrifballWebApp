using DiscordInterfaces;
using GrifballWebApp.Infrastructure.Discord;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// NetCord entry point for the staff commands' modals (a yes/no option left out): wraps the context and hands off to
/// <see cref="AdminCommandsHandler"/>, which checks the caller again. Keep logic out of here, it is only tested end to end
/// (DiscordTransportTests).
/// </summary>
public class AdminModalInteractions : ComponentInteractionModule<ModalInteractionContext>
{
    private readonly AdminCommandsHandler _handler;

    public AdminModalInteractions(AdminCommandsHandler handler)
    {
        _handler = handler;
    }

    [ComponentInteraction(DiscordModalsContants.BracketCreate)]
    public Task BracketCreate(int seasonId, int participants, int bestOf)
        => _handler.SaveBracket(Context.ToDiscordContext(), seasonId, participants, bestOf);

    [ComponentInteraction(DiscordModalsContants.JobPause)]
    public Task JobPause(string job) => _handler.SavePause(Context.ToDiscordContext(), job);

    [ComponentInteraction(DiscordModalsContants.RescheduleDecide)]
    public Task RescheduleDecide(int request) => _handler.SaveDecision(Context.ToDiscordContext(), request);
}
