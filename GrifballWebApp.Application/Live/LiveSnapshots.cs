using GrifballWebApp.Application.Home;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.TeamStandings;

namespace GrifballWebApp.Application.Live;

/// <summary>A season's draft as its page shows it: what its three reads give (the teams, the player pool, the lock).</summary>
public sealed record DraftLiveDto(int SeasonID, List<TeamResponseDto> Teams, List<PlayerDto> Pool, bool CaptainsLocked);

/// <summary>
/// A season's page as it shows it: its summary (header, champions, awards), standings, scheduled and unscheduled matches
/// (this week's are the scheduled ones), and its players' stats, the season's whole. Its bracket is read on its own.
/// </summary>
public sealed record SeasonLiveDto(int SeasonID, SeasonSummaryDto? Summary, TeamStandingDto[] Standings,
    List<ScheduledMatchDto> Scheduled, List<UnscheduledMatchDto> Unscheduled, List<SeasonPlayerStatDto> PlayerStats);

/// <summary>The home page's current and coming events, as its viewer may see them.</summary>
public sealed record SeasonsLiveDto(Event[] Events);

/// <summary>The matchmaking queue's size.</summary>
public sealed record QueueLiveDto(int Count);

/// <summary>
/// What is pushed with a change to the topics many watch at once (the draft, a season's page, the home page, the queue):
/// the data their pages show, built once a change for all their watchers, in place of a read from each. The same the API
/// gives each of those pages, from the same services. Built only for the topics someone watches (the LiveHub's process
/// knows its watchers), so an import's changes to seasons nobody is looking at build nothing. Every other topic is a
/// notice, and its pages read again.
/// </summary>
public class LiveSnapshots(TeamService teams, SeasonService seasons, TeamStandingsService standings, MatchPlannerService planner,
    HomeEventsService events, IQueueRepository queue)
{
    /// <summary>The payloads' types: the client's models have them too (the OpenAPI document's schemas).</summary>
    public static readonly Type[] Types = [typeof(DraftLiveDto), typeof(SeasonLiveDto), typeof(SeasonsLiveDto), typeof(QueueLiveDto)];

    /// <summary>Whether a change to <paramref name="topic"/> comes with its data.</summary>
    public static bool Pushes(string topic)
        => topic is LiveTopics.Seasons or LiveTopics.Queue || LiveTopics.Split(topic).Kind is LiveTopics.DraftKind or LiveTopics.SeasonKind;

    /// <summary>Whether <paramref name="topic"/>'s data differs by viewer (the home page's events: staff see hidden seasons').</summary>
    public static bool DependsOnViewer(string topic) => topic is LiveTopics.Seasons;

    /// <summary><paramref name="topic"/>'s data as <paramref name="viewer"/> sees it; null for a topic that is a notice.</summary>
    public virtual async Task<object?> For(string topic, SeasonViewer viewer, CancellationToken ct = default)
    {
        if (topic is LiveTopics.Seasons)
            return new SeasonsLiveDto(await events.CurrentAndFuture(viewer, ct));
        if (topic is LiveTopics.Queue)
            return new QueueLiveDto(await queue.Count(ct));
        var (kind, key) = LiveTopics.Split(topic);
        if (!int.TryParse(key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seasonID))
            return null;
        return kind switch
        {
            LiveTopics.DraftKind => new DraftLiveDto(seasonID, await teams.GetTeams(seasonID, ct), await teams.GetPlayerPool(seasonID, ct),
                await teams.AreCaptainsLocked(seasonID, ct)),
            LiveTopics.SeasonKind => new SeasonLiveDto(seasonID, await seasons.GetSeasonSummary(seasonID, ct), await standings.GetTeamStandings(seasonID, ct),
                await planner.GetScheduledMatches(seasonID, ct), await planner.GetUnscheduledMatches(seasonID, ct),
                await seasons.GetSeasonPlayerStats(seasonID, null, ct)),
            _ => null,
        };
    }
}
