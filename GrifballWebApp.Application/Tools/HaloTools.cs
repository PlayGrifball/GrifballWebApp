using System.ComponentModel;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Domain.Halo;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Tools;

/// <summary>A team in a recent game: its score, outcome and players' gamertags.</summary>
public sealed record RecentGameTeamDto(int TeamID, int Score, Outcomes Outcome, List<string> Players);

/// <summary>
/// A recent Halo game: its match ID (get_game has its details), when it started and how long it lasted, how it went for the player (their team's
/// outcome), its Halo Waypoint page and its teams.
/// </summary>
public sealed record RecentGameDto(Guid MatchID, DateTime? StartTime, TimeSpan Duration, Outcomes Outcome, string WaypointUrl, List<RecentGameTeamDto> Teams);

/// <summary>A player's recent games: the gamertag of the Xbox account they were found as, and the games.</summary>
public sealed record RecentGamesDto(string Gamertag, List<RecentGameDto> Games);

/// <summary>
/// The Halo games the site has saved: read only, open to anyone. Saving new ones is a pull from Halo, which only the roles the
/// site lets pull may start (<see cref="HaloPullTools"/>, the season match page's buttons).
/// </summary>
public class HaloTools
{
    public const int MaxGames = 25;

    private readonly IApplicationDbContext _context;
    private readonly XboxAccountService _accounts;

    public HaloTools(IApplicationDbContext context, XboxAccountService accounts)
    {
        _context = context;
        _accounts = accounts;
    }

    /// <summary>A game's page on Halo Waypoint, as the player's (their gamertag escaped).</summary>
    public static string WaypointUrl(string gamertag, Guid matchID)
        => $"https://www.halowaypoint.com/halo-infinite/players/{Uri.EscapeDataString(gamertag)}/matches/{matchID}";

    [Description("A player's latest Grifball games in Halo Infinite custom games, as the site last saved them (the league's games: matchmaking "
        + "and other modes aren't recorded): each one's start time, the player's outcome, Halo Waypoint link and teams (score, outcome, "
        + "players), newest first. Newer games appear once they are pulled (pull_xbox_account_games, pull_season_match_games).")]
    public async Task<RecentGamesDto> GetRecentGames(
        [Description("The player's Xbox gamertag.")] string gamertag,
        [Description("How many games, 1 to 25; left out, 5.")] int count = 5,
        CancellationToken ct = default)
    {
        var trimmed = gamertag.Trim();
        var xboxUser = await _accounts.FindByGamertagAsync(trimmed, ct)
            ?? throw new ToolException($"The site has no games saved for the gamertag {trimmed}: search finds the players it knows.");
        return await GetRecentGamesOf(xboxUser.XboxUserID, xboxUser.Gamertag, count, ct);
    }

    /// <summary>
    /// get_recent_games for an account already found (by its XUID: /matches, whose option picks one), named by
    /// <paramref name="gamertag"/>.
    /// </summary>
    public async Task<RecentGamesDto> GetRecentGamesOf(long xuid, string gamertag, int count = 5, CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, MaxGames);
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
                m.Duration,
                Outcome = m.MatchTeams.Where(t => t.MatchParticipants.Any(p => p.XboxUserID == xuid)).Select(t => t.Outcome).First(),
                Teams = m.MatchTeams.Select(t => new RecentGameTeamDto(t.TeamID, t.Score, t.Outcome, t.MatchParticipants.Select(p => p.XboxUser.Gamertag).ToList())).ToList(),
            })
            .ToListAsync(ct);
        return new RecentGamesDto(gamertag, games.Select(g => new RecentGameDto(g.MatchID, g.StartTime, g.Duration, g.Outcome, WaypointUrl(gamertag, g.MatchID), g.Teams)).ToList());
    }
}
