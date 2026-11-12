using DiscordInterface.Generated;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.TeamPage;
using NetCord.Rest;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// /team: a team's page, open to everyone as it is on the site, through the team page's services.
/// <see cref="TeamCommands"/> is the NetCord module; it only wraps its context and calls these.
/// </summary>
public class TeamCommandsHandler
{
    public const string NoSuchTeam = "I found no such team.";

    private readonly CommandLookups _lookups;
    private readonly TeamPageService _teams;
    private readonly TeamHistoryService _history;
    private readonly VodService _vods;
    private readonly UrlService _urls;

    public TeamCommandsHandler(CommandLookups lookups, TeamPageService teams, TeamHistoryService history, VodService vods, UrlService urls)
    {
        _lookups = lookups;
        _teams = teams;
        _history = history;
        _vods = vods;
        _urls = urls;
    }

    /// <summary>The team asked for and its page; null when there is none, the caller told so.</summary>
    private async Task<(int TeamID, TeamDto Team, string Url)?> TeamFor(IDiscordInteractionContext context, string team)
    {
        if (await _lookups.Team(team) is { } found && await _teams.GetAsync(found.TeamID) is { } dto)
            return (found.TeamID, dto, _urls.Team(found.SeasonID, found.TeamID));
        await context.Reply(NoSuchTeam);
        return null;
    }

    public async Task Info(IDiscordInteractionContext context, string team)
    {
        await context.Defer(ephemeral: false);
        if (await TeamFor(context, team) is not { } t)
            return;
        var captain = t.Team.Players.FirstOrDefault(x => x.IsCaptain);
        var embed = CommandReplies.Embed(t.Team.TeamName, t.Url);
        embed.Fields =
        [
            new EmbedFieldProperties().WithName("Season").WithValue(t.Team.Season.SeasonName.LinkMarkdown(_urls.Season(t.Team.Season.SeasonID))).WithInline(true),
            new EmbedFieldProperties().WithName("Record").WithValue($"{t.Team.Wins}-{t.Team.Losses}" + (t.Team.TBD > 0 ? $" ({t.Team.TBD} to play)" : "")).WithInline(true),
            new EmbedFieldProperties().WithName("Captain").WithValue(captain?.Gamertag ?? "None").WithInline(true),
            new EmbedFieldProperties().WithName("Players").WithValue(t.Team.Players.Length.ToString()).WithInline(true),
            .. t.Team.Titles.Length > 0 ? [new EmbedFieldProperties().WithName("Titles").WithValue(string.Join(", ", t.Team.Titles)).WithInline(false)] : Array.Empty<EmbedFieldProperties>(),
        ];
        await context.Reply(embed);
    }

    public async Task Roster(IDiscordInteractionContext context, string team)
    {
        await context.Defer(ephemeral: false);
        if (await TeamFor(context, team) is not { } t)
            return;
        var lines = t.Team.Players
            .OrderByDescending(x => x.IsCaptain)
            .ThenBy(x => x.DraftRound ?? int.MaxValue)
            .Select(x => x.Gamertag.LinkMarkdown(_urls.Profile(x.UserID))
                + (x.IsCaptain ? " (captain)" : x.DraftRound is int round ? $" · round {round}" : "")
                + (x.Region is { } region ? $" · {region}" : ""))
            .ToList();
        await context.Reply(CommandReplies.Embed($"{t.Team.TeamName} roster", t.Url, CommandReplies.Lines(lines, "No players yet.")));
    }

    public async Task History(IDiscordInteractionContext context, string team)
    {
        await context.Defer(ephemeral: false);
        if (await TeamFor(context, team) is not { } t)
            return;
        // The team was found, so it has a history
        var history = (await _history.GetAsync(t.TeamID))!;
        var lines = history.Seasons
            .Select(x => $"{x.Season.SeasonName.LinkMarkdown(_urls.Team(x.Season.SeasonID, x.TeamID))} · {x.Wins}-{x.Losses}"
                + (x.Titles.Length > 0 ? $" · {string.Join(", ", x.Titles)}" : ""))
            .ToList();
        var description = $"{history.Appearances} seasons · {history.Wins}-{history.Losses} · {history.Championships} championships\n\n"
            + CommandReplies.Lines(lines, "No seasons yet.");
        await context.Reply(CommandReplies.Embed($"{history.TeamName} history", _urls.TeamHistory(t.TeamID), description));
    }

    public async Task Matches(IDiscordInteractionContext context, string team)
    {
        await context.Defer(ephemeral: false);
        if (await TeamFor(context, team) is not { } t)
            return;
        var matches = await _teams.GetMatchesAsync(t.TeamID);
        var lines = matches
            .Select(x => $"{(x.ScheduledTime is { } time ? time.DiscordTimeEmbed() : "Unscheduled")} · "
                + $"vs {x.OtherTeamName ?? "TBD"}".LinkMarkdown(_urls.SeasonMatch(x.SeasonMatchID))
                + (x.Result is { } result ? $" · {result} {x.Score}-{x.OtherScore}" : ""))
            .ToList();
        await context.Reply(CommandReplies.Embed($"{t.Team.TeamName} matches", t.Url, CommandReplies.Lines(lines, "No matches yet.")));
    }

    public async Task Vods(IDiscordInteractionContext context, string team)
    {
        await context.Defer(ephemeral: false);
        if (await TeamFor(context, team) is not { } t)
            return;
        var vods = await _vods.ForTeam(t.TeamID);
        await context.Reply(CommandReplies.Embed($"{t.Team.TeamName} VODs", t.Url, VodLines(vods)));
    }

    /// <summary>VODs, newest first, each a YouTube link.</summary>
    internal static string VodLines(IEnumerable<VodDto> vods)
        => CommandReplies.Lines(vods
            .OrderByDescending(x => x.PublishedAt)
            .Select(x => $"{x.Title.LinkMarkdown($"https://www.youtube.com/watch?v={x.YouTubeVideoID}")} · {x.PublishedAt.DiscordTimeEmbed()}")
            .ToList(), "No VODs yet.");
}
