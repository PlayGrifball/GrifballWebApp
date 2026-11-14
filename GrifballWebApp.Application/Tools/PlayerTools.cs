using System.ComponentModel;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Application.Tools;

/// <summary>Who the caller is: their user ID, gamertag and site roles.</summary>
public sealed record WhoAmIDto(int UserID, string? Gamertag, IReadOnlyList<string> Roles);

/// <summary>Players: what a profile shows, open to everyone on the site. A player left out is the caller.</summary>
public class PlayerTools
{
    private readonly ToolCaller _caller;
    private readonly ProfileStatsService _profiles;
    private readonly ProfileCommendationsService _commendations;
    private readonly DeploymentHistoryService _history;
    private readonly VodService _vods;
    private readonly LeaderboardsService _leaderboards;

    public PlayerTools(ToolCaller caller, ProfileStatsService profiles, ProfileCommendationsService commendations, DeploymentHistoryService history,
        VodService vods, LeaderboardsService leaderboards)
    {
        _caller = caller;
        _profiles = profiles;
        _commendations = commendations;
        _history = history;
        _vods = vods;
        _leaderboards = leaderboards;
    }

    private static ToolException NoSuchPlayer(int userID) => new($"There is no player {userID}: search finds players by gamertag.");

    /// <summary>The player asked for, or the caller; a <see cref="ToolException"/> when there is none.</summary>
    private async Task<ProfileStatsDto> Profile(int? userID, CancellationToken ct)
    {
        var id = userID ?? _caller.UserID;
        return await _profiles.GetAsync(id, ct) ?? throw NoSuchPlayer(id);
    }

    [Description("Who you are signed in as: your user ID, gamertag and site roles (Player, Commissioner, Sysadmin).")]
    public async Task<WhoAmIDto> WhoAmI(CancellationToken ct = default)
        => new(_caller.UserID, (await _profiles.GetAsync(_caller.UserID, ct))?.Gamertag, _caller.Roles);

    [Description("A player's profile: gamertag, region, past gamertags, championships, career totals per game, each season's stats, "
        + "Late League stats and Halo Infinite service record.")]
    public Task<ProfileStatsDto> GetPlayer([Description("The player's user ID; left out, you.")] int? userID = null, CancellationToken ct = default)
        => Profile(userID, ct);

    [Description("A player's medals across their recorded games: totals and the ones they earn most.")]
    public async Task<ProfileCommendationsDto> GetPlayerCommendations([Description("The player's user ID; left out, you.")] int? userID = null, CancellationToken ct = default)
        => await _commendations.GetAsync((await Profile(userID, ct)).UserID, ct: ct);

    [Description("A player's teams: each season they played, the team and whether they captained it.")]
    public async Task<List<DeploymentHistoryDto>> GetPlayerHistory([Description("The player's user ID; left out, you.")] int? userID = null, CancellationToken ct = default)
    {
        // Null only for a user that doesn't exist
        var id = userID ?? _caller.UserID;
        return await _history.GetAsync(id, ct) ?? throw NoSuchPlayer(id);
    }

    [Description("Recorded videos (VODs) a player is in.")]
    public async Task<List<VodDto>> GetPlayerVods([Description("The player's user ID; left out, you.")] int? userID = null, CancellationToken ct = default)
        => await _vods.ForPlayer((await Profile(userID, ct)).UserID, ct);

    [Description("A player's all-time rank on each leaderboard (titles, goals, kills, games played and more).")]
    public async Task<PlayerAllTimeRankDto> GetPlayerRank(
        [Description("The player's user ID; left out with no gamertag, you.")] int? userID = null,
        [Description("A gamertag, current or past, instead of a user ID.")] string? gamertag = null,
        [Description("Count only titles of this season format; left out, every format.")] SeasonFormat? format = null,
        CancellationToken ct = default)
    {
        var id = gamertag is null ? userID ?? _caller.UserID : userID;
        return await _leaderboards.GetPlayerRankAsync(gamertag, id, format, ct)
            ?? throw new ToolException(gamertag is null ? $"Player {id} is on no leaderboard." : $"No player has the gamertag {gamertag}.");
    }
}
