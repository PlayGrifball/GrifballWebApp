using DiscordInterface.Generated;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.Grades;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Application.TeamStandings;
using NetCord.Rest;

namespace GrifballWebApp.Server.Commands;

/// <summary>The stat <c>/season stats</c> ranks players by.</summary>
public enum SeasonStat
{
    Goals,
    Kills,
    Wins,
    KD,
}

/// <summary>
/// /season: what the season pages show, open to everyone as they are on the site. Each reads the controllers' services
/// and links to the site's page. <see cref="SeasonCommands"/> is the NetCord module; it only wraps its context and calls these.
/// </summary>
public class SeasonCommandsHandler
{
    public const string NoSuchSeason = "I found no such season.";
    private const int Top = 10;

    private readonly CommandLookups _lookups;
    private readonly SeasonService _seasons;
    private readonly TeamStandingsService _standings;
    private readonly MatchPlannerService _matchPlanner;
    private readonly GradesService _grades;
    private readonly SignupsService _signups;
    private readonly IBracketService _brackets;
    private readonly UrlService _urls;

    public SeasonCommandsHandler(CommandLookups lookups, SeasonService seasons, TeamStandingsService standings, MatchPlannerService matchPlanner,
        GradesService grades, SignupsService signups, IBracketService brackets, UrlService urls)
    {
        _lookups = lookups;
        _seasons = seasons;
        _standings = standings;
        _matchPlanner = matchPlanner;
        _grades = grades;
        _signups = signups;
        _brackets = brackets;
        _urls = urls;
    }

    /// <summary>The season asked for, with its name; null when there is none, the caller told so.</summary>
    private async Task<(int ID, string Name)?> SeasonFor(IDiscordInteractionContext context, string? season)
    {
        if (await _lookups.Season(season) is int id && await _seasons.GetSeasonName(id) is { } name)
            return (id, name);
        await context.Reply(NoSuchSeason);
        return null;
    }

    public async Task List(IDiscordInteractionContext context)
    {
        await context.Defer(ephemeral: false);
        var seasons = await _seasons.GetSeasons(new SeasonListFilter { PageNumber = 1, PageSize = 15 });
        var lines = seasons.Results
            .Select(x => $"{x.SeasonName.LinkMarkdown(_urls.Season(x.SeasonID))} · {x.LeagueName} · {x.Year}{(x.HasEnded ? "" : " · **running**")}")
            .ToList();
        var description = CommandReplies.Lines(lines, "There are no seasons yet.");
        if (seasons.TotalCount > lines.Count)
            description += "\n" + CommandReplies.More(seasons.TotalCount - lines.Count);
        await context.Reply(CommandReplies.Embed("Seasons", _urls.Page("seasons"), description));
    }

    public async Task Info(IDiscordInteractionContext context, string? season)
    {
        await context.Defer(ephemeral: false);
        if (await SeasonFor(context, season) is not { } s)
            return;
        var summary = (await _seasons.GetSeasonSummary(s.ID))!;
        var embed = CommandReplies.Embed(summary.SeasonName, _urls.Season(s.ID));
        embed.Fields =
        [
            new EmbedFieldProperties().WithName("League").WithValue(summary.LeagueName).WithInline(true),
            new EmbedFieldProperties().WithName("Format").WithValue(summary.Format.ToString()).WithInline(true),
            new EmbedFieldProperties().WithName("Dates").WithValue($"{summary.Start.DiscordTimeEmbed()} to {summary.End.DiscordTimeEmbed()}").WithInline(false),
            new EmbedFieldProperties().WithName("Teams").WithValue(summary.TeamCount.ToString()).WithInline(true),
            new EmbedFieldProperties().WithName("Signups").WithValue(summary.SignupCount.ToString()).WithInline(true),
            new EmbedFieldProperties().WithName("Matches").WithValue((summary.MatchCount + summary.BracketMatchCount).ToString()).WithInline(true),
            .. summary.Champions.Select(c => new EmbedFieldProperties()
                .WithName($"{c.Tier} champion")
                .WithValue($"{c.TeamName}: {string.Join(", ", c.Roster.Select(p => p.Gamertag))}")
                .WithInline(false)),
        ];
        await context.Reply(embed);
    }

    public async Task Standings(IDiscordInteractionContext context, string? season)
    {
        await context.Defer(ephemeral: false);
        if (await SeasonFor(context, season) is not { } s)
            return;
        var standings = await _standings.GetTeamStandings(s.ID);
        var lines = standings
            .Select((x, i) => $"{i + 1}. {x.TeamName.LinkMarkdown(_urls.Team(s.ID, x.TeamID))} · {x.Wins}-{x.Losses}")
            .ToList();
        await context.Reply(CommandReplies.Embed($"{s.Name} standings", _urls.Season(s.ID), CommandReplies.Lines(lines, "No teams yet.")));
    }

    public async Task Schedule(IDiscordInteractionContext context, string? season)
    {
        await context.Defer(ephemeral: false);
        if (await SeasonFor(context, season) is not { } s)
            return;
        var scheduled = await _matchPlanner.GetScheduledMatches(s.ID);
        var unscheduled = await _matchPlanner.GetUnscheduledMatches(s.ID);
        var lines = scheduled
            .Where(x => !x.Complete)
            .OrderBy(x => x.Time)
            .Select(x => $"{x.Time.DiscordTimeEmbed()} · {$"{x.HomeTeam} vs {x.AwayTeam}".LinkMarkdown(_urls.SeasonMatch(x.SeasonMatchID))}")
            .ToList();
        var description = CommandReplies.Lines(lines, "No upcoming matches are scheduled.");
        if (unscheduled.Count > 0)
            description += $"\n\n{unscheduled.Count} more {(unscheduled.Count == 1 ? "match is" : "matches are")} not scheduled yet.";
        await context.Reply(CommandReplies.Embed($"{s.Name} schedule", _urls.Season(s.ID), description));
    }

    public async Task Stats(IDiscordInteractionContext context, string? season, SeasonStat stat)
    {
        await context.Defer(ephemeral: false);
        if (await SeasonFor(context, season) is not { } s)
            return;
        var stats = await _seasons.GetSeasonPlayerStats(s.ID);
        Func<SeasonPlayerStatDto, decimal> by = stat switch
        {
            SeasonStat.Kills => x => x.Kills,
            SeasonStat.Wins => x => x.Wins,
            SeasonStat.KD => x => x.KdRatio ?? 0,
            _ => x => x.Goals,
        };
        var lines = stats
            .Where(x => !x.IsCombined)
            .OrderByDescending(by)
            .Take(Top)
            .Select((x, i) => $"{i + 1}. {x.Gamertag.LinkMarkdown(_urls.Profile(x.UserID))} · {by(x):0.##}")
            .ToList();
        await context.Reply(CommandReplies.Embed($"{s.Name}: top {stat}", _urls.Season(s.ID), CommandReplies.Lines(lines, "No stats yet.")));
    }

    public async Task Grades(IDiscordInteractionContext context, string? season)
    {
        await context.Defer(ephemeral: false);
        if (await SeasonFor(context, season) is not { } s)
            return;
        var grades = await _grades.GetGrades(s.ID, CancellationToken.None);
        var lines = grades.Totals
            .OrderByDescending(x => x.TotalGoals)
            .Take(Top)
            .Select((x, i) => $"{i + 1}. {x.Gamertag} · {x.TotalGoals} goals · {x.TotalKills} kills")
            .ToList();
        await context.Reply(CommandReplies.Embed($"{s.Name} player grades", _urls.Grades(s.ID), CommandReplies.Lines(lines, "No grades yet.")));
    }

    public async Task Signups(IDiscordInteractionContext context, string? season)
    {
        await context.Defer(ephemeral: false);
        if (await SeasonFor(context, season) is not { } s)
            return;
        var signups = await _signups.GetSignups(s.ID);
        var lines = signups
            .OrderBy(x => x.Timestamp)
            .Select(x => $"{x.PersonName}{(x.WillCaptain ? " (captain)" : "")}")
            .ToList();
        await context.Reply(CommandReplies.Embed($"{s.Name} signups: {signups.Count}", _urls.ViewSignups(s.ID), CommandReplies.Lines(lines, "Nobody has signed up yet.")));
    }

    public async Task Bracket(IDiscordInteractionContext context, string? season)
    {
        await context.Defer(ephemeral: false);
        if (await SeasonFor(context, season) is not { } s)
            return;
        var bracket = await _brackets.GetBracketsAsync(s.ID);
        var lines = bracket.WinnerRounds
            .Select(r => $"**Round {r.RoundNumber}**\n" + string.Join("\n", r.Matches.Select(m => $"{m.HomeTeam} vs {m.AwayTeam}")))
            .ToList();
        if (bracket.GrandFinal is { } final)
            lines.Add($"**Grand final**\n{final.HomeTeam} vs {final.AwayTeam}");
        await context.Reply(CommandReplies.Embed($"{s.Name} bracket", _urls.Season(s.ID), CommandReplies.Lines(lines, "This season has no bracket yet.")));
    }

    /// <summary>The draft board is live on the site (SignalR), so this links to it.</summary>
    public async Task Draft(IDiscordInteractionContext context, string? season)
    {
        await context.Defer(ephemeral: false);
        if (await SeasonFor(context, season) is not { } s)
            return;
        await context.Reply(CommandReplies.Embed($"{s.Name} draft", _urls.Draft(s.ID), "The draft board is live on the site."));
    }
}
