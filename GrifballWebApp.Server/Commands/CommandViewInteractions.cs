using DiscordInterfaces;
using GrifballWebApp.Infrastructure.Discord;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// NetCord entry point for a list's ◀ ▶ buttons: wraps the context and hands off to <see cref="CommandViewHandler"/>. Keep logic out of here,
/// it is only tested end to end (DiscordTransportTests).
/// </summary>
public class CommandViewButtonInteractions : ComponentInteractionModule<ButtonInteractionContext>
{
    private readonly CommandViewHandler _handler;

    public CommandViewButtonInteractions(CommandViewHandler handler)
    {
        _handler = handler;
    }

    [ComponentInteraction(DiscordButtonContants.Page)]
    public Task Page(string view, string key, int page) => _handler.Page(Context.ToDiscordContext(), view, key, page);
}

/// <summary>
/// NetCord entry point for the season, stat and player-view menus under a reply: wraps the context and hands off to
/// <see cref="CommandViewHandler"/>. Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
public class CommandViewStringMenuInteractions : ComponentInteractionModule<StringMenuInteractionContext>
{
    private readonly CommandViewHandler _handler;

    public CommandViewStringMenuInteractions(CommandViewHandler handler)
    {
        _handler = handler;
    }

    [ComponentInteraction(DiscordStringMenuContants.SeasonView)]
    public Task Season(string view) => _handler.PickSeason(Context.ToDiscordContext(), view);

    [ComponentInteraction(DiscordStringMenuContants.SeasonStat)]
    public Task Stat(int seasonId) => _handler.PickStat(Context.ToDiscordContext(), seasonId);

    [ComponentInteraction(DiscordStringMenuContants.PlayerView)]
    public Task Player(int userId) => _handler.PickPlayerView(Context.ToDiscordContext(), userId);
}
