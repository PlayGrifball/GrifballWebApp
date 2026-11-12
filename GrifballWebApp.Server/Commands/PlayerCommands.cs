using DiscordInterfaces;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// NetCord entry point for /player: wraps the context and hands off to <see cref="PlayerCommandsHandler"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
[SlashCommand("player", "Players: profile, stats, medals and history")]
public class PlayerCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private const string PlayerDescription = "The player (default: you)";
    private readonly PlayerCommandsHandler _handler;

    public PlayerCommands(PlayerCommandsHandler handler)
    {
        _handler = handler;
    }

    [SubSlashCommand("profile", "A player's region, record and championships")]
    public Task Profile([SlashCommandParameter(Description = PlayerDescription, AutocompleteProviderType = typeof(PlayerAutocomplete))] string? player = null)
        => _handler.Profile(Context.ToDiscordContext(), player);

    [SubSlashCommand("stats", "A player's career stats by game")]
    public Task Stats([SlashCommandParameter(Description = PlayerDescription, AutocompleteProviderType = typeof(PlayerAutocomplete))] string? player = null)
        => _handler.Stats(Context.ToDiscordContext(), player);

    [SubSlashCommand("commendations", "A player's medals")]
    public Task Commendations([SlashCommandParameter(Description = PlayerDescription, AutocompleteProviderType = typeof(PlayerAutocomplete))] string? player = null)
        => _handler.Commendations(Context.ToDiscordContext(), player);

    [SubSlashCommand("history", "A player's seasons and teams")]
    public Task History([SlashCommandParameter(Description = PlayerDescription, AutocompleteProviderType = typeof(PlayerAutocomplete))] string? player = null)
        => _handler.History(Context.ToDiscordContext(), player);

    [SubSlashCommand("vods", "A player's VODs")]
    public Task Vods([SlashCommandParameter(Description = PlayerDescription, AutocompleteProviderType = typeof(PlayerAutocomplete))] string? player = null)
        => _handler.Vods(Context.ToDiscordContext(), player);

    [SubSlashCommand("matches", "A player's recent Halo matches")]
    public Task Matches([SlashCommandParameter(Description = PlayerDescription, AutocompleteProviderType = typeof(PlayerAutocomplete))] string? player = null)
        => _handler.Matches(Context.ToDiscordContext(), player);
}
