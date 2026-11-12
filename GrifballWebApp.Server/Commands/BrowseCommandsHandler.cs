using DiscordInterface.Generated;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.Home;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.Search;
using GrifballWebApp.Domain.League;
using NetCord.Rest;

namespace GrifballWebApp.Server.Commands;

/// <summary>The board <c>/leaderboard top</c> shows.</summary>
public enum LeaderboardBoard
{
    Rings,
    Goals,
    Kills,
    Games,
    CarrierStops,
}

/// <summary>Which seasons the all-time boards count: every format, or one (<see cref="SeasonFormat"/>).</summary>
public enum LeaderboardFormat
{
    All,
    LeagueSeason,
    Tournament,
}

/// <summary>
/// /leaderboard, /lateleague, /events and /search: the all-time boards, Late League, the home page's calendar and the
/// site's search, open to everyone as they are on the site. Their NetCord modules only wrap their context and call these.
/// </summary>
public class BrowseCommandsHandler
{
    public const string NoSuchLateLeagueSeason = "I found no such Late League season.";
    private const int Top = 10;

    private readonly LeaderboardsService _leaderboards;
    private readonly MatchmakingStatsService _lateLeague;
    private readonly HomeEventsService _events;
    private readonly QuickSearchService _search;
    private readonly CommandLookups _lookups;
    private readonly UrlService _urls;

    public BrowseCommandsHandler(LeaderboardsService leaderboards, MatchmakingStatsService lateLeague, HomeEventsService events, QuickSearchService search,
        CommandLookups lookups, UrlService urls)
    {
        _leaderboards = leaderboards;
        _lateLeague = lateLeague;
        _events = events;
        _search = search;
        _lookups = lookups;
        _urls = urls;
    }

    public async Task Leaderboard(IDiscordInteractionContext context, LeaderboardBoard board, LeaderboardFormat format)
    {
        await context.Defer(ephemeral: false);
        var boards = await _leaderboards.GetAsync(SeasonFormatOf(format));
        List<string> lines = board switch
        {
            LeaderboardBoard.Rings => boards.RingLeaders.Take(Top)
                .Select(x => $"{x.Rank}. {x.Gamertag.LinkMarkdown(_urls.Profile(x.UserID))} · {x.RingsCount} rings").ToList(),
            _ => (board switch
            {
                LeaderboardBoard.Kills => boards.KillLeaders,
                LeaderboardBoard.Games => boards.GamesPlayedLeaders,
                LeaderboardBoard.CarrierStops => boards.CarrierStopLeaders,
                _ => boards.GoalLeaders,
            }).Take(Top).Select(x => $"{x.Rank}. {x.Gamertag.LinkMarkdown(_urls.Profile(x.UserID))} · {x.StatValue}").ToList(),
        };
        var title = $"All-time {board}" + (format is LeaderboardFormat.All ? "" : $" ({format})");
        await context.Reply(CommandReplies.Embed(title, _urls.Page("leaderboards"), CommandReplies.Lines(lines, "Nobody is ranked yet.")));
    }

    public async Task Rank(IDiscordInteractionContext context, string player, LeaderboardFormat format)
    {
        await context.Defer(ephemeral: false);
        var userID = await _lookups.Player(player);
        if (await _leaderboards.GetPlayerRankAsync(userID is null ? player : null, userID, SeasonFormatOf(format)) is not { } rank)
        {
            await context.Reply(PlayerCommandsHandler.NoSuchPlayer);
            return;
        }

        var embed = CommandReplies.Embed($"{rank.Gamertag}: all-time ranks", _urls.Profile(rank.UserID));
        embed.Fields =
        [
            RankField("Rings", rank.Rings),
            RankField("Goals", rank.Goals),
            RankField("Kills", rank.Kills),
            RankField("Games", rank.GamesPlayed),
            RankField("Carrier stops", rank.CarrierStops),
        ];
        await context.Reply(embed);
    }

    private static SeasonFormat? SeasonFormatOf(LeaderboardFormat format) => format switch
    {
        LeaderboardFormat.LeagueSeason => SeasonFormat.LeagueSeason,
        LeaderboardFormat.Tournament => SeasonFormat.Tournament,
        _ => null,
    };

    private static EmbedFieldProperties RankField(string name, MetricRankDetail detail)
        => new EmbedFieldProperties().WithName(name)
            .WithValue(detail.Rank is int rank ? $"#{rank} of {detail.TotalRanked} · {detail.StatValue}" : "Unranked")
            .WithInline(true);

    public async Task LateLeagueSeasons(IDiscordInteractionContext context)
    {
        await context.Defer(ephemeral: false);
        var seasons = await _lateLeague.GetSeasonsAsync();
        var lines = seasons
            .OrderByDescending(x => x.StartsAt)
            .Select(x => $"{x.Name.LinkMarkdown(_urls.Page($"late-league/{x.MatchmakingSeasonID}"))} · {x.GameCount} games · {x.PlayerCount} players")
            .ToList();
        await context.Reply(CommandReplies.Embed("Late League seasons", _urls.Page("late-league"), CommandReplies.Lines(lines, "No Late League seasons yet.")));
    }

    /// <summary>A Late League season's top players; with none named, the latest season.</summary>
    public async Task LateLeagueSeason(IDiscordInteractionContext context, string? season)
    {
        await context.Defer(ephemeral: false);
        var seasons = await _lateLeague.GetSeasonsAsync();
        var picked = season is null
            ? seasons.MaxBy(x => x.StartsAt)
            : seasons.FirstOrDefault(x => x.MatchmakingSeasonID.ToString() == season.Trim())
                ?? seasons.Where(x => x.Name.Contains(season.Trim(), StringComparison.OrdinalIgnoreCase)).MaxBy(x => x.StartsAt);
        if (picked is null || await _lateLeague.GetSeasonAsync(picked.MatchmakingSeasonID) is not { } detail)
        {
            await context.Reply(NoSuchLateLeagueSeason);
            return;
        }

        var lines = detail.Players
            .OrderByDescending(x => x.Wins)
            .ThenByDescending(x => x.Goals)
            .Take(Top)
            .Select((x, i) => $"{i + 1}. {x.Gamertag.LinkMarkdown(_urls.Profile(x.UserID))} · {x.Wins}-{x.Losses} · {x.Goals} goals")
            .ToList();
        var description = CommandReplies.Lines(lines, "No games yet.",
            head: $"{detail.Season.StartsAt.DiscordTimeEmbed()} to {detail.Season.EndsAt.DiscordTimeEmbed()} · {detail.Season.GameCount} games\n\n");
        await context.Reply(CommandReplies.Embed(detail.Season.Name, _urls.Page($"late-league/{picked.MatchmakingSeasonID}"), description));
    }

    public async Task Events(IDiscordInteractionContext context)
    {
        await context.Defer(ephemeral: false);
        var events = await _events.CurrentAndFuture();
        var lines = events
            .Select(x => $"{x.Start.DiscordTimeEmbed()} · {x.Name.LinkMarkdown(_urls.Season(x.SeasonID))} {EventLabel(x.EventType)} until {x.End.DiscordTimeEmbed()}")
            .ToList();
        await context.Reply(CommandReplies.Embed("Upcoming events", _urls.Page(""), CommandReplies.Lines(lines, "Nothing is coming up.")));
    }

    private static string EventLabel(EventType type) => type switch
    {
        EventType.Signup => "signups",
        EventType.Draft => "draft",
        _ => "season",
    };

    public async Task Search(IDiscordInteractionContext context, string query)
    {
        await context.Defer(ephemeral: false);
        var results = await _search.SearchAsync(query);
        var embed = CommandReplies.Embed($"Search: {query}", _urls.Page(""));
        embed.Fields =
        [
            new EmbedFieldProperties().WithName("Players").WithValue(CommandReplies.FieldLines(results.Players
                .Select(x => x.Name.LinkMarkdown(_urls.Profile(x.UserID)) + (x.PastGamertag is { } past ? $" (was {past})" : "")).ToList(), "None")).WithInline(false),
            new EmbedFieldProperties().WithName("Teams").WithValue(CommandReplies.FieldLines(results.Teams
                .Select(x => $"{x.TeamName.LinkMarkdown(_urls.Team(x.SeasonID, x.TeamID))} ({x.SeasonName})").ToList(), "None")).WithInline(false),
            new EmbedFieldProperties().WithName("Seasons").WithValue(CommandReplies.FieldLines(results.Seasons
                .Select(x => $"{x.SeasonName.LinkMarkdown(_urls.Season(x.SeasonID))} ({x.Years})").ToList(), "None")).WithInline(false),
        ];
        await context.Reply(embed);
    }
}
