using GrifballWebApp.Application.Brackets;
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
/// (this week's are the scheduled ones), and its players' stats, the season's whole. Its bracket, teams page and team
/// pages have topics of their own.
/// </summary>
public sealed record SeasonLiveDto(int SeasonID, SeasonSummaryDto? Summary, TeamStandingDto[] Standings,
    List<ScheduledMatchDto> Scheduled, List<UnscheduledMatchDto> Unscheduled, List<SeasonPlayerStatDto> PlayerStats);

/// <summary>A season's playoff bracket as brackets-viewer draws it; null for a season with none.</summary>
public sealed record BracketLiveDto(int SeasonID, ViewerDataDto? ViewerData);

/// <summary>A season's teams page: its banner (the summary) and its teams with their rosters and records.</summary>
public sealed record TeamsLiveDto(int SeasonID, SeasonSummaryDto? Summary, List<SeasonTeamDto>? Teams);

/// <summary>A team's page: the team (null once it is gone), its matches, and its players' stats for its season.</summary>
public sealed record TeamLiveDto(int TeamID, TeamPage.TeamDto? Team, TeamPage.MatchDto[] Matches, List<SeasonPlayerStatDto> PlayerStats);

/// <summary>The home page's current and coming events, as its viewer may see them.</summary>
public sealed record SeasonsLiveDto(Event[] Events);

/// <summary>The matchmaking queue's size.</summary>
public sealed record QueueLiveDto(int Count);

/// <summary>
/// What is pushed with a change to the topics many watch at once (the draft, a season's pages, a team's, the home page,
/// the queue): the data their pages show, built once a change for all their watchers, in place of a read from each.
/// The same the API gives each of those pages, from the same services. Built only for the topics someone watches (the LiveHub's process
/// knows its watchers), so an import's changes to seasons nobody is looking at build nothing. Every other topic is a
/// notice, and its pages read again.
/// </summary>
public class LiveSnapshots(TeamService teams, SeasonService seasons, TeamStandingsService standings, MatchPlannerService planner,
    HomeEventsService events, IQueueRepository queue, IBracketService brackets, TeamPage.TeamPageService teamPages)
{
    /// <summary>The payloads' types: the client's models have them too (the OpenAPI document's schemas).</summary>
    public static readonly Type[] Types = [typeof(DraftLiveDto), typeof(SeasonLiveDto), typeof(BracketLiveDto),
        typeof(TeamsLiveDto), typeof(TeamLiveDto), typeof(SeasonsLiveDto), typeof(QueueLiveDto)];

    /// <summary>Whether a change to <paramref name="topic"/> comes with its data.</summary>
    public static bool Pushes(string topic)
        => topic is LiveTopics.Seasons or LiveTopics.Queue || LiveTopics.Split(topic).Kind is LiveTopics.DraftKind or LiveTopics.SeasonKind
            or LiveTopics.BracketKind or LiveTopics.TeamsKind or LiveTopics.TeamKind;

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
        if (!int.TryParse(key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id))
            return null;
        return kind switch
        {
            LiveTopics.DraftKind => new DraftLiveDto(id, await teams.GetTeams(id, ct), await teams.GetPlayerPool(id, ct),
                await teams.AreCaptainsLocked(id, ct)),
            LiveTopics.SeasonKind => new SeasonLiveDto(id, await seasons.GetSeasonSummary(id, ct), await standings.GetTeamStandings(id, ct),
                await planner.GetScheduledMatches(id, ct), await planner.GetUnscheduledMatches(id, ct),
                await seasons.GetSeasonPlayerStats(id, null, ct)),
            LiveTopics.BracketKind => new BracketLiveDto(id, await brackets.GetViewerDataAsync(id, ct)),
            LiveTopics.TeamsKind => new TeamsLiveDto(id, await seasons.GetSeasonSummary(id, ct), await seasons.GetSeasonTeams(id, ct)),
            LiveTopics.TeamKind => await Team(id, ct),
            _ => null,
        };
    }

    /// <summary>A team's page: the team, its matches, and its players' stats for its season (none once the team is gone).</summary>
    private async Task<TeamLiveDto> Team(int teamID, CancellationToken ct)
    {
        var team = await teamPages.GetAsync(teamID, ct);
        List<SeasonPlayerStatDto> stats = team is null ? [] : await seasons.GetSeasonPlayerStats(team.Season.SeasonID, teamID, ct);
        return new TeamLiveDto(teamID, team, await teamPages.GetMatchesAsync(teamID, ct), stats);
    }
}
