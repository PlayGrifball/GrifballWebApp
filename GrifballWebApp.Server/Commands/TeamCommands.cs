using DiscordInterfaces;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// NetCord entry point for /team: wraps the context and hands off to <see cref="TeamCommandsHandler"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
[SlashCommand("team", "Teams: info, roster, history and matches")]
public class TeamCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private const string TeamDescription = "The team";
    private readonly TeamCommandsHandler _handler;

    public TeamCommands(TeamCommandsHandler handler)
    {
        _handler = handler;
    }

    [SubSlashCommand("info", "A team's season, record, captain and titles")]
    public Task Info([SlashCommandParameter(Description = TeamDescription, AutocompleteProviderType = typeof(TeamAutocomplete))] string team)
        => _handler.Info(Context.ToDiscordContext(), team);

    [SubSlashCommand("roster", "A team's players")]
    public Task Roster([SlashCommandParameter(Description = TeamDescription, AutocompleteProviderType = typeof(TeamAutocomplete))] string team)
        => _handler.Roster(Context.ToDiscordContext(), team);

    [SubSlashCommand("history", "A team's seasons across the league's history")]
    public Task History([SlashCommandParameter(Description = TeamDescription, AutocompleteProviderType = typeof(TeamAutocomplete))] string team)
        => _handler.History(Context.ToDiscordContext(), team);

    [SubSlashCommand("matches", "A team's matches, unplayed first")]
    public Task Matches([SlashCommandParameter(Description = TeamDescription, AutocompleteProviderType = typeof(TeamAutocomplete))] string team)
        => _handler.Matches(Context.ToDiscordContext(), team);

    [SubSlashCommand("vods", "A team's VODs")]
    public Task Vods([SlashCommandParameter(Description = TeamDescription, AutocompleteProviderType = typeof(TeamAutocomplete))] string team)
        => _handler.Vods(Context.ToDiscordContext(), team);
}
