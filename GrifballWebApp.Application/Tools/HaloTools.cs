using System.ComponentModel;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Domain.Halo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GrifballWebApp.Application.Tools;

/// <summary>A team in a recent game: its score, outcome and players' gamertags.</summary>
public sealed record RecentGameTeamDto(int TeamID, int Score, Outcomes Outcome, List<string> Players);

/// <summary>A recent Halo game: its match ID (get_game has its details), when it started, its Halo Waypoint page and its teams.</summary>
public sealed record RecentGameDto(Guid MatchID, DateTime? StartTime, string WaypointUrl, List<RecentGameTeamDto> Teams);

/// <summary>Halo games as they come in: pulled fresh from Halo and saved before they are read (<see cref="ToolEffect.FetchesFromHalo"/>).</summary>
public class HaloTools
{
    public const int MaxGames = 25;

    private readonly IApplicationDbContext _context;
    private readonly IGetsertXboxUserService _xboxUsers;
    private readonly IDataPullService _dataPull;
    private readonly ILogger<HaloTools> _logger;

    public HaloTools(IApplicationDbContext context, IGetsertXboxUserService xboxUsers, IDataPullService dataPull, ILogger<HaloTools> logger)
    {
        _context = context;
        _xboxUsers = xboxUsers;
        _dataPull = dataPull;
        _logger = logger;
    }

    /// <summary>A game's page on Halo Waypoint, as the player's (their gamertag escaped).</summary>
    public static string WaypointUrl(string gamertag, Guid matchID)
        => $"https://www.halowaypoint.com/halo-infinite/players/{Uri.EscapeDataString(gamertag)}/matches/{matchID}";

    [Description("A player's latest Halo Infinite games, any playlist: pulled fresh from Halo, then each one's start time, Halo Waypoint link "
        + "and teams (score, outcome, players). Newest first.")]
    public async Task<List<RecentGameDto>> GetRecentGames(
        [Description("The player's Xbox gamertag.")] string gamertag,
        [Description("How many games, 1 to 25; left out, 5.")] int count = 5,
        CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, MaxGames);
        var (xboxUser, reason) = await _xboxUsers.GetsertXboxUserByGamertag(gamertag, ct);
        if (xboxUser is null)
            throw new ToolException(reason ?? $"There is no Xbox gamertag {gamertag}.");

        try
        {
            await _dataPull.DownloadRecentMatchesForPlayers([xboxUser.XboxUserID], startPage: 0, endPage: 0, count, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Halo being down still leaves the games saved before
            _logger.LogError(ex, "Failed to fetch recent matches for {Gamertag}", xboxUser.Gamertag);
        }

        var games = await _context.Matches
            .AsNoTracking()
            .Where(m => m.MatchTeams.Any(t => t.MatchParticipants.Any(p => p.XboxUserID == xboxUser.XboxUserID)))
            .OrderByDescending(m => m.StartTime)
            .Take(count)
            .Select(m => new
            {
                m.MatchID,
                m.StartTime,
                Teams = m.MatchTeams.Select(t => new RecentGameTeamDto(t.TeamID, t.Score, t.Outcome, t.MatchParticipants.Select(p => p.XboxUser.Gamertag).ToList())).ToList(),
            })
            .ToListAsync(ct);
        return games.Select(g => new RecentGameDto(g.MatchID, g.StartTime, WaypointUrl(xboxUser.Gamertag, g.MatchID), g.Teams)).ToList();
    }
}
