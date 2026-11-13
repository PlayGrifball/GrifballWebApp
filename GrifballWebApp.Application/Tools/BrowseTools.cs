using System.ComponentModel;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.Search;

namespace GrifballWebApp.Application.Tools;

/// <summary>Finding things: search, the all-time leaderboards and Late League, open to everyone on the site.</summary>
public class BrowseTools
{
    private readonly QuickSearchService _search;
    private readonly LeaderboardsService _leaderboards;
    private readonly MatchmakingStatsService _lateLeague;

    public BrowseTools(QuickSearchService search, LeaderboardsService leaderboards, MatchmakingStatsService lateLeague)
    {
        _search = search;
        _leaderboards = leaderboards;
        _lateLeague = lateLeague;
    }

    [Description("Searches players (by gamertag, current or past, Discord or display name), teams and seasons (by name or a year they ran in): "
        + "up to 8 hits of each kind, each with the ID the other tools take. The query needs at least 2 characters.")]
    public Task<QuickSearchDto> Search([Description("What to look for: part of a gamertag, team or season name, or a year.")] string query,
        CancellationToken ct = default)
        => _search.SearchAsync(query, ct);

    [Description("The all-time leaderboards: most titles (ring leaders), dynasties, and the goal, kill, carrier-stop and games-played leaders.")]
    public Task<LeaderboardsDto> GetLeaderboards([Description("Count only seasons of this format, or Late League matchmaking; left out, all seasons (excluding Late League).")] LeaderboardFilter? format = null,
        CancellationToken ct = default)
        => _leaderboards.GetAsync(format, ct);

    [Description("Lists Late League's seasons (matchmaking, not drafted teams): each one's ID, name, dates and how many games and players it had.")]
    public Task<List<MatchmakingSeasonDto>> ListLateLeagueSeasons(CancellationToken ct = default)
        => _lateLeague.GetSeasonsAsync(ct);

    [Description("A Late League season and each player's stats in it.")]
    public async Task<MatchmakingSeasonDetailDto> GetLateLeagueSeason([Description("The Late League season's ID.")] int matchmakingSeasonID,
        CancellationToken ct = default)
        => await _lateLeague.GetSeasonAsync(matchmakingSeasonID, ct)
            ?? throw new ToolException($"There is no Late League season {matchmakingSeasonID}: list_late_league_seasons has their IDs.");
}
