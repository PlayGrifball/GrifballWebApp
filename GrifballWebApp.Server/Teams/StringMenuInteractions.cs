using DiscordInterfaces;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Teams;

/// <summary>
/// NetCord entry point for the draft pick string menu: wraps the context and hands off to <see cref="DraftStringMenuHandler"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
public class StringMenuInteractions : ComponentInteractionModule<StringMenuInteractionContext>
{
    private readonly DraftStringMenuHandler _handler;

    public StringMenuInteractions(DraftStringMenuHandler handler)
    {
        _handler = handler;
    }

    // The last segment of the custom ID (the menu's index) only keeps the IDs unique; the pick doesn't need it
    [ComponentInteraction(DiscordStringMenuContants.DraftPick)]
    public async Task DraftPick(int seasonId, int captainId, int _)
    {
        await _handler.DraftPick(Context.ToDiscordContext(), seasonId, captainId);
    }
}
