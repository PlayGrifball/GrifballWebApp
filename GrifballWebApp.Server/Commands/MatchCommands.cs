using DiscordInterfaces;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// NetCord entry point for /match: wraps the context and hands off to <see cref="MatchCommandsHandler"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
[SlashCommand("match", "Season matches and Halo games")]
public class MatchCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly MatchCommandsHandler _handler;

    public MatchCommands(MatchCommandsHandler handler)
    {
        _handler = handler;
    }

    [SubSlashCommand("info", "A season match's time, result and games")]
    public Task Info([SlashCommandParameter(Description = "The match (search by team)", AutocompleteProviderType = typeof(SeasonMatchAutocomplete))] string match)
        => _handler.Info(Context.ToDiscordContext(), match);

    [SubSlashCommand("pull", "Pull a season match's players' recent games from Halo (players and staff)")]
    public Task Pull([SlashCommandParameter(Description = "The match (search by team)", AutocompleteProviderType = typeof(SeasonMatchAutocomplete))] string match,
        [SlashCommandParameter(Name = "back_to_season_start", Description = "Back to the season's start, for an old match")] bool backToSeasonStart = false)
        => _handler.Pull(Context.ToDiscordContext(), match, backToSeasonStart);

    [SubSlashCommand("game", "A Halo game's teams and players")]
    public Task Game([SlashCommandParameter(Name = "match_id", Description = "The game's Halo match ID")] string matchID)
        => _handler.Game(Context.ToDiscordContext(), matchID);
}
