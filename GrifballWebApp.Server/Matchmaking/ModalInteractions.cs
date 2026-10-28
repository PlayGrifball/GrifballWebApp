using DiscordInterfaces;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Matchmaking;

/// <summary>
/// NetCord entry point for the Set Gamertag modal: wraps the context and hands off to <see cref="DiscordSetGamertag"/>.
/// Keep logic out of here, it is excluded from coverage (coverlet.runsettings).
/// </summary>
public class ModalInteractions : ComponentInteractionModule<ModalInteractionContext>
{
    private readonly DiscordSetGamertag _discordSetGamertag;
    public ModalInteractions(DiscordSetGamertag discordSetGamertag)
    {
        _discordSetGamertag = discordSetGamertag;
    }

    [ComponentInteraction(DiscordModalsContants.SetGamertag)]
    public async Task SetGamertag()
    {
        await _discordSetGamertag.SetGamertag(Context.ToDiscordContext());
    }
}
