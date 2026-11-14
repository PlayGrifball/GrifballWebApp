using System.ComponentModel;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Queries;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Tools;

/// <summary>A team in a recent game: its score, outcome and players' gamertags.</summary>
public sealed record RecentGameTeamDto(int TeamID, int Score, Outcomes Outcome, List<string> Players);

/// <summary>A recent Halo game: its match ID (get_game has its details), when it started, its Halo Waypoint page and its teams.</summary>
public sealed record RecentGameDto(Guid MatchID, DateTime? StartTime, string WaypointUrl, List<RecentGameTeamDto> Teams);

/// <summary>
/// The Halo games the site has saved: read only, open to anyone. Saving new ones is a pull from Halo, which only the roles the
/// site lets pull may start (<see cref="HaloPullTools"/>, the season match page's buttons).
/// </summary>
public class HaloTools
{
    public const int MaxGames = 25;

    private readonly IApplicationDbContext _context;

    public HaloTools(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>A game's page on Halo Waypoint, as the player's (their gamertag escaped).</summary>
    public static string WaypointUrl(string gamertag, Guid matchID)
        => $"https://www.halowaypoint.com/halo-infinite/players/{Uri.EscapeDataString(gamertag)}/matches/{matchID}";

    /// <summary>
    /// The account a gamertag is: one whose gamertag it is now, else one that had it before (its history). Gamertags aren't
    /// unique (a row keeps the one its account renamed away from until it's looked up again), so of several, the one that
    /// played last. Text compares case-insensitively on every database (the context's collation).
    /// </summary>
    private async Task<(long XboxUserID, string Gamertag)?> AccountOf(string gamertag, CancellationToken ct)
    {
        var normalized = XboxGamertagHistory.Normalize(gamertag);
        var current = _context.XboxUsers.Where(x => x.Gamertag == gamertag).Select(x => x.XboxUserID);
        var past = _context.XboxUsers.PastGamertags().Where(h => h.NormalizedGamertag == normalized).Select(h => h.XboxUserID);
        foreach (var xuids in new[] { current, past })
        {
            var found = await _context.XboxUsers.AsNoTracking()
                .Where(x => xuids.Contains(x.XboxUserID))
                .Select(x => new
                {
                    x.XboxUserID,
                    x.Gamertag,
                    LastPlayed = _context.Matches.Where(m => m.MatchTeams.Any(t => t.MatchParticipants.Any(p => p.XboxUserID == x.XboxUserID))).Max(m => m.StartTime),
                })
                .OrderByDescending(x => x.LastPlayed != null)
                .ThenByDescending(x => x.LastPlayed)
                .ThenByDescending(x => x.XboxUserID)
                .FirstOrDefaultAsync(ct);
            if (found is not null)
                return (found.XboxUserID, found.Gamertag);
        }
        return null;
    }

    [Description("A player's latest Grifball games in Halo Infinite custom games, as the site last saved them (the league's games: matchmaking "
        + "and other modes aren't recorded): each one's start time, Halo Waypoint link and teams (score, outcome, players), newest first. "
        + "Newer games appear once a season match's games are pulled (pull_season_match_games).")]
    public async Task<List<RecentGameDto>> GetRecentGames(
        [Description("The player's Xbox gamertag.")] string gamertag,
        [Description("How many games, 1 to 25; left out, 5.")] int count = 5,
        CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, MaxGames);
        var trimmed = gamertag.Trim();
        var xboxUser = await AccountOf(trimmed, ct)
            ?? throw new ToolException($"The site has no games saved for the gamertag {trimmed}: search finds the players it knows.");
        var xuid = xboxUser.XboxUserID;

        var games = await _context.Matches
            .AsNoTracking()
            .Where(m => m.MatchTeams.Any(t => t.MatchParticipants.Any(p => p.XboxUserID == xuid)))
            // Undated games last: Postgres would put NULLs first
            .OrderBy(m => m.StartTime == null)
            .ThenByDescending(m => m.StartTime)
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
