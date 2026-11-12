using DiscordInterfaces;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// NetCord entry point for /leaderboard: wraps the context and hands off to <see cref="BrowseCommandsHandler"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
[SlashCommand("leaderboard", "The all-time leaderboards")]
public class LeaderboardCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private const string FormatDescription = "Count only one format (default: all)";
    private readonly BrowseCommandsHandler _handler;

    public LeaderboardCommands(BrowseCommandsHandler handler)
    {
        _handler = handler;
    }

    [SubSlashCommand("top", "The top players on a board")]
    public Task Top(
        [SlashCommandParameter(Description = "The board")] LeaderboardBoard board = LeaderboardBoard.Rings,
        [SlashCommandParameter(Description = FormatDescription)] LeaderboardFormat format = LeaderboardFormat.All)
        => _handler.Leaderboard(Context.ToDiscordContext(), board, format);

    [SubSlashCommand("rank", "A player's all-time ranks")]
    public Task Rank(
        [SlashCommandParameter(Description = "The player", AutocompleteProviderType = typeof(PlayerAutocomplete))] string player,
        [SlashCommandParameter(Description = FormatDescription)] LeaderboardFormat format = LeaderboardFormat.All)
        => _handler.Rank(Context.ToDiscordContext(), player, format);
}

/// <summary>
/// NetCord entry point for /lateleague: wraps the context and hands off to <see cref="BrowseCommandsHandler"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
[SlashCommand("lateleague", "Late League seasons and standings")]
public class LateLeagueCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly BrowseCommandsHandler _handler;

    public LateLeagueCommands(BrowseCommandsHandler handler)
    {
        _handler = handler;
    }

    [SubSlashCommand("seasons", "The Late League seasons")]
    public Task Seasons() => _handler.LateLeagueSeasons(Context.ToDiscordContext());

    [SubSlashCommand("season", "A Late League season's top players")]
    public Task Season([SlashCommandParameter(Description = "The season's name or ID (default: the latest)")] string? season = null)
        => _handler.LateLeagueSeason(Context.ToDiscordContext(), season);
}

/// <summary>
/// NetCord entry point for /events and /search: wraps the context and hands off to <see cref="BrowseCommandsHandler"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
public class BrowseCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly BrowseCommandsHandler _handler;

    public BrowseCommands(BrowseCommandsHandler handler)
    {
        _handler = handler;
    }

    [SlashCommand("events", "Upcoming signups, drafts and seasons")]
    public Task Events() => _handler.Events(Context.ToDiscordContext());

    [SlashCommand("search", "Search players, teams and seasons")]
    public Task Search([SlashCommandParameter(Description = "A gamertag, team or season name")] string query)
        => _handler.Search(Context.ToDiscordContext(), query);
}
