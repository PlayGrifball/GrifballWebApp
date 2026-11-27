using DiscordInterfaces;
using NetCord.Services.ComponentInteractions;
using GrifballWebApp.Infrastructure.Discord;
using GrifballWebApp.Server.Signups;

namespace GrifballWebApp.Server.Matchmaking;

/// <summary>
/// NetCord entry point for the Set Gamertag modal: wraps the context and hands off to <see cref="DiscordSetGamertag"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
public class ModalInteractions : ComponentInteractionModule<ModalInteractionContext>
{
    private readonly DiscordSetGamertag _discordSetGamertag;
    private readonly SignupButtonHandler _signups;
    public ModalInteractions(DiscordSetGamertag discordSetGamertag, SignupButtonHandler signups)
    {
        _discordSetGamertag = discordSetGamertag;
        _signups = signups;
    }

    [ComponentInteraction(DiscordModalsContants.SetGamertag)]
    public async Task SetGamertag()
    {
        await _discordSetGamertag.SetGamertag(Context.ToDiscordContext());
    }

    [ComponentInteraction(DiscordModalsContants.Signup)]
    public async Task Signup(int seasonId)
    {
        await _signups.SignupDetails(Context.ToDiscordContext(), seasonId);
    }
}
