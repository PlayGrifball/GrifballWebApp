using DiscordInterfaces;
using GrifballWebApp.Infrastructure.Discord;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// NetCord entry point for /match pull's modal (back_to_season_start left out): wraps the context and hands off to
/// <see cref="MatchCommandsHandler"/>. Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
public class MatchModalInteractions : ComponentInteractionModule<ModalInteractionContext>
{
    private readonly MatchCommandsHandler _handler;

    public MatchModalInteractions(MatchCommandsHandler handler)
    {
        _handler = handler;
    }

    [ComponentInteraction(DiscordModalsContants.MatchPull)]
    public Task MatchPull(int matchId) => _handler.SavePull(Context.ToDiscordContext(), matchId);
}
