using DiscordInterfaces;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Matchmaking;

/// <summary>
/// NetCord entry point for the matchmaking string menus: wraps the context and hands off to <see cref="MatchmakingStringMenuHandler"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
public class StringMenuInteractions : ComponentInteractionModule<StringMenuInteractionContext>
{
    private readonly MatchmakingStringMenuHandler _handler;

    public StringMenuInteractions(MatchmakingStringMenuHandler handler)
    {
        _handler = handler;
    }

    [ComponentInteraction(DiscordStringMenuContants.VoteToKick)]
    public async Task VoteToKick(int matchId)
    {
        await _handler.VoteToKick(Context.ToDiscordContext(), matchId);
    }
}
