using DiscordInterfaces;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// NetCord entry point for /season: wraps the context and hands off to <see cref="SeasonCommandsHandler"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
[SlashCommand("season", "Seasons: standings, schedule, stats and more")]
public class SeasonCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private const string SeasonDescription = "The season (default: the current one)";
    private readonly SeasonCommandsHandler _handler;

    public SeasonCommands(SeasonCommandsHandler handler)
    {
        _handler = handler;
    }

    [SubSlashCommand("list", "The latest seasons")]
    public Task List() => _handler.List(Context.ToDiscordContext());

    [SubSlashCommand("info", "A season's league, dates, teams and champions")]
    public Task Info([SlashCommandParameter(Description = SeasonDescription, AutocompleteProviderType = typeof(SeasonAutocomplete))] string? season = null)
        => _handler.Info(Context.ToDiscordContext(), season);

    [SubSlashCommand("standings", "A season's team standings")]
    public Task Standings([SlashCommandParameter(Description = SeasonDescription, AutocompleteProviderType = typeof(SeasonAutocomplete))] string? season = null)
        => _handler.Standings(Context.ToDiscordContext(), season);

    [SubSlashCommand("schedule", "A season's upcoming matches")]
    public Task Schedule([SlashCommandParameter(Description = SeasonDescription, AutocompleteProviderType = typeof(SeasonAutocomplete))] string? season = null)
        => _handler.Schedule(Context.ToDiscordContext(), season);

    [SubSlashCommand("stats", "A season's top players")]
    public Task Stats(
        [SlashCommandParameter(Description = "Rank players by")] SeasonStat stat = SeasonStat.Goals,
        [SlashCommandParameter(Description = SeasonDescription, AutocompleteProviderType = typeof(SeasonAutocomplete))] string? season = null)
        => _handler.Stats(Context.ToDiscordContext(), season, stat);

    [SubSlashCommand("grades", "A season's player grades")]
    public Task Grades([SlashCommandParameter(Description = SeasonDescription, AutocompleteProviderType = typeof(SeasonAutocomplete))] string? season = null)
        => _handler.Grades(Context.ToDiscordContext(), season);

    [SubSlashCommand("signups", "Who signed up for a season")]
    public Task Signups([SlashCommandParameter(Description = SeasonDescription, AutocompleteProviderType = typeof(SeasonAutocomplete))] string? season = null)
        => _handler.Signups(Context.ToDiscordContext(), season);

    [SubSlashCommand("bracket", "A season's playoff bracket")]
    public Task Bracket([SlashCommandParameter(Description = SeasonDescription, AutocompleteProviderType = typeof(SeasonAutocomplete))] string? season = null)
        => _handler.Bracket(Context.ToDiscordContext(), season);

    [SubSlashCommand("draft", "A link to a season's live draft board")]
    public Task Draft([SlashCommandParameter(Description = SeasonDescription, AutocompleteProviderType = typeof(SeasonAutocomplete))] string? season = null)
        => _handler.Draft(Context.ToDiscordContext(), season);
}
