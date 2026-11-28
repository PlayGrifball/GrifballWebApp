using DiscordInterface.Generated;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.Grades;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Infrastructure.Discord;
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
    public const string NoSuchSeason = CommandLookups.NoSuchSeason;

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

    /// <summary>
    /// The season asked for, with its name, or the one <paramref name="purpose"/> means; null when there is none, the caller told so.
    /// The reply is the channel's, so a season not announced yet is none, whoever asks (#435).
    /// </summary>
    private async Task<(int ID, string Name)?> SeasonFor(IDiscordInteractionContext context, string? season, SeasonPurpose purpose = SeasonPurpose.Current)
    {
        return await _lookups.SeasonFor(context, season, purpose) is int id ? (id, (await _seasons.GetSeasonName(id))!) : null;
    }

    public async Task List(IDiscordInteractionContext context)
    {
        await context.Defer(ephemeral: false);
        var seasons = await _seasons.GetSeasons(new SeasonListFilter { PageNumber = 1, PageSize = 15 });
        var lines = seasons.Results
            .Select(x => $"{x.SeasonName.LinkMarkdown(_urls.Season(x.SeasonID))} · {x.LeagueName} · {x.Year}{(x.HasEnded ? "" : " · **running**")}")
            .ToList();
        var description = CommandReplies.Lines(lines, "There are no seasons yet.",
            tail: seasons.TotalCount > lines.Count ? "\n" + CommandReplies.More(seasons.TotalCount - lines.Count) : "");
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

    /// <summary>The views /season's lists show, each paged and with a menu to switch season (<see cref="View"/>).</summary>
    public const string StandingsView = "standings";
    public const string ScheduleView = "schedule";
    public const string GradesView = "grades";
    public const string SignupsView = "signups";
    private const string StatsPrefix = "stats-";

    /// <summary>The view of the players ranked by <paramref name="stat"/> (<c>/season stats</c>).</summary>
    public static string StatsView(SeasonStat stat) => StatsPrefix + stat;

    public Task Standings(IDiscordInteractionContext context, string? season) => Reply(context, season, StandingsView);

    public Task Schedule(IDiscordInteractionContext context, string? season) => Reply(context, season, ScheduleView);

    public Task Stats(IDiscordInteractionContext context, string? season, SeasonStat stat) => Reply(context, season, StatsView(stat));

    public Task Grades(IDiscordInteractionContext context, string? season) => Reply(context, season, GradesView);

    public Task Signups(IDiscordInteractionContext context, string? season) => Reply(context, season, SignupsView, SeasonPurpose.Upcoming);

    /// <summary>A list's first page, for the season asked for or the one <paramref name="purpose"/> means.</summary>
    private async Task Reply(IDiscordInteractionContext context, string? season, string view, SeasonPurpose purpose = SeasonPurpose.Current)
    {
        await context.Defer(ephemeral: false);
        if (await SeasonFor(context, season, purpose) is { } s)
            await context.Reply((await View(view, s.ID, 0))!);
    }

    /// <summary>
    /// Page <paramref name="page"/> of season <paramref name="seasonID"/>'s list <paramref name="view"/>, with the menu to switch season (and stat,
    /// for the stats); null for a view there isn't. The season is the caller's to have checked: the reply is the channel's.
    /// </summary>
    public async Task<CommandView?> View(string view, int seasonID, int page)
    {
        var name = (await _seasons.GetSeasonName(seasonID))!;
        SeasonStat? stat = view.StartsWith(StatsPrefix, StringComparison.Ordinal) && Enum.TryParse<SeasonStat>(view[StatsPrefix.Length..], out var parsed) ? parsed : null;
        var list = stat is { } ranked ? await StatsList(seasonID, name, ranked) : view switch
        {
            StandingsView => await StandingsList(seasonID, name),
            ScheduleView => await ScheduleList(seasonID, name),
            GradesView => await GradesList(seasonID, name),
            SignupsView => await SignupsList(seasonID, name),
            _ => null,
        };
        if (list is null)
            return null;
        IMessageComponentProperties[] menus = stat is { } shown ? [await SeasonMenu(view, seasonID), StatMenu(seasonID, shown)] : [await SeasonMenu(view, seasonID)];
        return CommandViews.List(view, seasonID.ToString(System.Globalization.CultureInfo.InvariantCulture), page, list, menus);
    }

    /// <summary>The menu of the latest seasons (the ones anyone sees), this one picked: picking one shows the same list for it.</summary>
    private async Task<StringMenuProperties> SeasonMenu(string view, int seasonID)
    {
        var current = CommandLookups.ChoiceValue(seasonID);
        var seasons = await _lookups.SeasonChoices(null);
        return new StringMenuProperties(DiscordStringMenuContants.SeasonViewWithParams(view),
            [.. seasons.Select(c => new StringMenuSelectOptionProperties(c.Name, c.StringValue!) { Default = c.StringValue == current })])
        {
            Placeholder = "Another season",
        };
    }

    /// <summary>The menu of the stats the players can be ranked by, <paramref name="stat"/> picked.</summary>
    private static StringMenuProperties StatMenu(int seasonID, SeasonStat stat)
        => new(DiscordStringMenuContants.SeasonStatWithParams(seasonID),
            [.. Enum.GetValues<SeasonStat>().Select(s => new StringMenuSelectOptionProperties(s.ToString(), s.ToString()) { Default = s == stat })])
        {
            Placeholder = "Another stat",
        };

    private async Task<CommandList> StandingsList(int seasonID, string name)
    {
        var standings = await _standings.GetTeamStandings(seasonID);
        var lines = standings
            .Select((x, i) => $"{i + 1}. {x.TeamName.LinkMarkdown(_urls.Team(seasonID, x.TeamID))} · {x.Wins}-{x.Losses}")
            .ToList();
        return new($"{name} standings", _urls.Season(seasonID), lines, "No teams yet.");
    }

    private async Task<CommandList> ScheduleList(int seasonID, string name)
    {
        var scheduled = await _matchPlanner.GetScheduledMatches(seasonID);
        var unscheduled = await _matchPlanner.GetUnscheduledMatches(seasonID);
        var lines = scheduled
            .Where(x => !x.Complete)
            .OrderBy(x => x.Time)
            .Select(x => $"{x.Time.DiscordTimeEmbed()} · {$"{x.HomeTeam} vs {x.AwayTeam}".LinkMarkdown(_urls.SeasonMatch(x.SeasonMatchID))}")
            .ToList();
        return new($"{name} schedule", _urls.Season(seasonID), lines, "No upcoming matches are scheduled.",
            unscheduled.Count > 0 ? $"\n\n{unscheduled.Count} more {(unscheduled.Count == 1 ? "match is" : "matches are")} not scheduled yet." : "");
    }

    private async Task<CommandList> StatsList(int seasonID, string name, SeasonStat stat)
    {
        var stats = await _seasons.GetSeasonPlayerStats(seasonID);
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
            .Select((x, i) => $"{i + 1}. {x.Gamertag.LinkMarkdown(_urls.Profile(x.UserID))} · {by(x):0.##}")
            .ToList();
        return new($"{name}: top {stat}", _urls.Season(seasonID), lines, "No stats yet.");
    }

    private async Task<CommandList> GradesList(int seasonID, string name)
    {
        var grades = await _grades.GetGrades(seasonID, CancellationToken.None);
        var lines = grades.Totals
            .OrderByDescending(x => x.TotalGoals)
            .Select((x, i) => $"{i + 1}. {x.Gamertag} · {x.TotalGoals} goals · {x.TotalKills} kills")
            .ToList();
        return new($"{name} player grades", _urls.Grades(seasonID), lines, "No grades yet.");
    }

    private async Task<CommandList> SignupsList(int seasonID, string name)
    {
        var signups = await _signups.GetSignups(seasonID);
        var lines = signups
            .OrderBy(x => x.Timestamp)
            .Select(x => $"{x.PersonName}{(x.WillCaptain ? " (captain)" : "")}")
            .ToList();
        return new($"{name} signups: {signups.Count}", _urls.ViewSignups(seasonID), lines, "Nobody has signed up yet.");
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
        if (await SeasonFor(context, season, SeasonPurpose.Upcoming) is not { } s)
            return;
        await context.Reply(CommandReplies.Embed($"{s.Name} draft", _urls.Draft(s.ID), "The draft board is live on the site."));
    }
}
