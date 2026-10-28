using DiscordInterfaces;
using GrifballWebApp.Server.Matchmaking;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server;

/// <summary>
/// NetCord entry point for the slash commands: wraps the context and hands off to <see cref="DiscordCommandsHandler"/>
/// and <see cref="DiscordSetGamertag"/>. Keep logic out of here, it is excluded from coverage (coverlet.runsettings).
/// </summary>
public class DiscordCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly DiscordCommandsHandler _handler;
    private readonly DiscordSetGamertag _discordSetGamertag;
    public DiscordCommands(DiscordCommandsHandler handler, DiscordSetGamertag discordSetGamertag)
    {
        _handler = handler;
        _discordSetGamertag = discordSetGamertag;
    }

    [SlashCommand("matches", "Get recent matches")]
    public async Task RecentMatches([SlashCommandParameter(Description = "Gamertag to search matches for", AutocompleteProviderType = typeof(GamertagAutocomplete))] string? gamertag = null)
    {
        await _handler.RecentMatches(Context.ToDiscordContext(), gamertag);
    }

    [SlashCommand("setgamertag", "Set your gamertag")]
    public async Task SetGamertag(string gamertag)
    {
        await _discordSetGamertag.SetGamertag(Context.ToDiscordContext(), gamertag);
    }
}
