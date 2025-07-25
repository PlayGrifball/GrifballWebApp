﻿using DiscordInterfaces;
using NetCord;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Matchmaking;

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
        var gamertag = ((TextInput)Context.Components[0]).Value;
        await _discordSetGamertag.SetGamertag(Context.ToDiscordContext(), gamertag);
    }
}
