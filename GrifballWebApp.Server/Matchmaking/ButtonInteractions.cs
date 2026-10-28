using DiscordInterfaces;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Matchmaking;

/// <summary>
/// NetCord entry point for the matchmaking buttons: wraps the context and hands off to <see cref="MatchmakingButtonHandler"/>.
/// Keep logic out of here, it is excluded from coverage (coverlet.runsettings).
/// </summary>
public class ButtonInteractions : ComponentInteractionModule<ButtonInteractionContext>
{
    private readonly MatchmakingButtonHandler _handler;

    public ButtonInteractions(MatchmakingButtonHandler handler)
    {
        _handler = handler;
    }

    [ComponentInteraction(DiscordButtonContants.SetGamertag)]
    public async Task SetGamertag()
    {
        await _handler.SetGamertag(Context.ToDiscordContext());
    }

    [ComponentInteraction(DiscordButtonContants.JoinQueue)]
    public async Task JoinQueue()
    {
        await _handler.JoinQueue(Context.ToDiscordContext());
    }

    [ComponentInteraction(DiscordButtonContants.LeaveQueue)]
    public async Task LeaveQueue()
    {
        await _handler.LeaveQueue(Context.ToDiscordContext());
    }

    [ComponentInteraction(DiscordButtonContants.VoteForWinner)]
    public async Task VoteForWinner(int matchId, string winner)
    {
        await _handler.VoteForWinner(Context.ToDiscordContext(), matchId, winner);
    }
}
