using GrifballWebApp.Application.Tools;
using DiscordInterface.Generated;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.SeasonMatchPage;
using NetCord.Rest;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// /match: a season match's page and a Halo game's details, open to everyone as they are on the site.
/// <see cref="MatchCommands"/> is the NetCord module; it only wraps its context and calls these.
/// </summary>
public class MatchCommandsHandler
{
    public const string NoSuchMatch = CommandLookups.NoSuchMatch;
    public const string NoSuchGame = "I found no such game. Give its Halo match ID.";

    private readonly CommandLookups _lookups;
    private readonly SeasonMatchService _seasonMatches;
    private readonly MatchDetailsService _matchDetails;
    private readonly UrlService _urls;
    private readonly DiscordCaller _caller;
    private readonly PullToolsFor _pulls;

    public MatchCommandsHandler(CommandLookups lookups, SeasonMatchService seasonMatches, MatchDetailsService matchDetails, UrlService urls,
        DiscordCaller caller, PullToolsFor pulls)
    {
        _lookups = lookups;
        _seasonMatches = seasonMatches;
        _matchDetails = matchDetails;
        _urls = urls;
        _caller = caller;
        _pulls = pulls;
    }

    /// <summary>
    /// Queues the pull of a season match's players' recent games from Halo, as the match page's button does: the agents'
    /// pull_season_match_games, as the caller (players and staff).
    /// </summary>
    public async Task Pull(IDiscordInteractionContext context, string match, bool backToSeasonStart)
    {
        await context.Defer(ephemeral: true);
        if (await _caller.Require(context, GrifTools.RolesByName["pull_season_match_games"]) is not { } caller)
            return;
        if (await _lookups.SeasonMatchFor(context, match) is not int id)
            return;
        try
        {
            var status = await _pulls.As(caller).PullSeasonMatchGames(id, backToSeasonStart);
            await context.Reply($"The pull of this match's players' games {PullReplies.State(status)}. The match page shows the games it finds: {_urls.SeasonMatch(id)}");
        }
        catch (ToolException e)
        {
            await context.Reply(e.Message);
        }
    }

    public async Task Info(IDiscordInteractionContext context, string match)
    {
        await context.Defer(ephemeral: false);
        if (await _lookups.SeasonMatchFor(context, match) is not int id)
            return;
        var page = (await _seasonMatches.GetSeasonMatchPage(id))!;

        var home = page.HomeTeamName ?? "TBD";
        var away = page.AwayTeamName ?? "TBD";
        var embed = CommandReplies.Embed($"{home} vs {away}", _urls.SeasonMatch(id));
        var games = page.ReportedGames
            .OrderBy(x => x.MatchNumber)
            .Select(x => $"Game {x.MatchNumber}: {x.HomeScore?.ToString() ?? "-"}-{x.AwayScore?.ToString() ?? "-"}"
                + (x.Map is { } map ? $" · {map}" : "")
                + (x.MatchID is { } matchID ? $" · {"details".LinkMarkdown(_urls.Match(matchID))}" : ""))
            .ToList();
        embed.Fields =
        [
            new EmbedFieldProperties().WithName("Season").WithValue(page.SeasonName.LinkMarkdown(_urls.Season(page.SeasonID))).WithInline(true),
            new EmbedFieldProperties().WithName("When").WithValue(page.ScheduledTime?.DiscordTimeEmbed() ?? "Not scheduled").WithInline(true),
            new EmbedFieldProperties().WithName("Best of").WithValue(page.BestOf.ToString()).WithInline(true),
            new EmbedFieldProperties().WithName("Result")
                .WithValue(page.HomeTeamResult is SeasonMatchResultDto.TBD
                    ? "To be played"
                    : $"{home} {page.HomeTeamScore?.ToString() ?? page.HomeTeamResult.ToString()} - {page.AwayTeamScore?.ToString() ?? page.AwayTeamResult.ToString()} {away}")
                .WithInline(false),
            new EmbedFieldProperties().WithName("Games").WithValue(CommandReplies.FieldLines(games, "None reported yet.")).WithInline(false),
        ];
        await context.Reply(embed);
    }

    public async Task Game(IDiscordInteractionContext context, string matchID)
    {
        await context.Defer(ephemeral: false);
        if (!Guid.TryParse(matchID.Trim(), out var id) || await _matchDetails.GetAsync(id) is not { } details)
        {
            await context.Reply(NoSuchGame);
            return;
        }

        var embed = CommandReplies.Embed(details.SeasonMatch is { } sm ? $"{sm.SeasonName}: game {sm.GameNumber} of {sm.BestOf}" : "Halo Infinite match", _urls.Match(id));
        embed.Fields =
        [
            new EmbedFieldProperties().WithName("Played").WithValue(details.StartTime?.DiscordTimeEmbed() ?? "Unknown").WithInline(true),
            new EmbedFieldProperties().WithName("Duration").WithValue(details.Duration.ToString(@"m\:ss")).WithInline(true),
            .. details.Teams.Select(team => new EmbedFieldProperties()
                .WithName($"{team.SeasonTeamName ?? $"Team {team.TeamID}"}: {team.Score} ({team.Outcome})")
                .WithValue(CommandReplies.FieldLines(team.Players
                    .OrderBy(p => p.Rank)
                    .Select(p => $"{p.Gamertag} · {p.Goals ?? 0} goals · {p.Kills}/{p.Deaths}/{p.Assists}")
                    .ToList(), "No players."))
                .WithInline(false)),
        ];
        await context.Reply(embed);
    }
}
