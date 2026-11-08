using GrifballWebApp.Database;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.LateLeague;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.Stats;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Profile;

/// <summary>A player's profile: who they are, their titles, their season and Late League lines and their career totals.</summary>
public class ProfileStatsService
{
    private readonly GrifballContext _context;
    private readonly TimeProvider _time;
    private readonly SeasonStatsService _stats;
    private readonly MatchmakingStatsService _matchmaking;

    public ProfileStatsService(GrifballContext context, SeasonStatsService stats, MatchmakingStatsService matchmaking, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _context = context;
        _stats = stats;
        _matchmaking = matchmaking;
    }

    /// <summary>The user's profile; null when there is no such user.</summary>
    public async Task<ProfileStatsDto?> GetAsync(int userID, CancellationToken ct = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.XboxUser)
            .Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == userID, ct);
        if (user is null)
            return null;

        // The gamertags the user's Xbox account had before (its renames), latest first, but not the one it has now (renamed
        // A to B and back to A, A is in its history too)
        var current = user.XboxUser is null ? null : HistoricalKeys.Normalize(user.XboxUser.Gamertag);
        var pastGamertags = user.XboxUserID is null
            ? []
            : (await _context.PastGamertags()
                .Where(h => h.XboxUserID == user.XboxUserID)
                .OrderBy(h => h.LastSeen == null)
                .ThenByDescending(h => h.LastSeen)
                .ThenBy(h => h.FirstSeen)
                .ThenBy(h => h.XboxGamertagHistoryID)
                .Select(h => new { h.Gamertag, h.NormalizedGamertag })
                .ToListAsync(ct))
                .Where(h => h.NormalizedGamertag != current)
                .Select(h => h.Gamertag)
                .ToList();

        var titles = await _context.Championships()
            .AsNoTracking()
            .Where(c => c.UserID == userID)
            .OrderByDescending(c => c.Season.SeasonStart)
            .ThenBy(c => c.Tier)
            .Select(c => new
            {
                c.PlayerChampionshipID,
                c.SeasonID,
                c.GameVersionID,
                GameVersionName = c.GameVersion != null ? c.GameVersion.GameVersionName : null,
                GameVersionSlug = c.GameVersion != null ? c.GameVersion.Slug : null,
                c.Tier,
                c.TeamID,
                c.Team.TeamName,
                c.IsCaptain,
                c.GamertagUsed,
            })
            .ToListAsync(ct);
        var seasons = await SeasonInfoDto.LoadAsync(_context, titles.Select(c => c.SeasonID), _time.GetUtcNow().UtcDateTime, ct);

        // Season stats: from the season's games where it has them, otherwise the season totals stored for it
        var lines = await _stats.GetAsync(userID: userID, ct: ct);
        // Late League: matchmaking, not seasons, but the player's games all the same
        var lateLeague = await _matchmaking.GetAsync(userID: userID, ct: ct);
        var career = lines.Concat<StatLine>(lateLeague).ToList();
        var serviceRecord = await ServiceRecord.LoadAsync(_context, user, lines, lateLeague, ct);

        return new ProfileStatsDto
        {
            UserID = user.Id,
            Gamertag = user.XboxUser?.Gamertag,
            DisplayName = user.DisplayName,
            IsDummyUser = user.IsDummyUser,
            RegionID = user.RegionID,
            RegionName = user.Region?.RegionName,
            PastGamertags = pastGamertags,
            Championships = titles.Select(c => new PlayerChampionshipDto
            {
                PlayerChampionshipID = c.PlayerChampionshipID,
                Season = seasons[c.SeasonID],
                GameVersionID = c.GameVersionID,
                GameVersionName = c.GameVersionName,
                GameVersionSlug = c.GameVersionSlug,
                Tier = c.Tier,
                TeamID = c.TeamID,
                TeamName = c.TeamName,
                IsCaptain = c.IsCaptain,
                GamertagUsed = c.GamertagUsed,
            }).ToList(),
            CareerByGame = CareerTotals.ByGame(career),
            OverallCareer = career.Count > 0 ? CareerTotals.AcrossGames(career) : null,
            Seasons = lines.Select(PlayerSeasonStatDetailDto.From).ToList(),
            LateLeague = lateLeague.Select(MatchmakingPlayerStatDto.From).ToList(),
            ServiceRecord = serviceRecord,
        };
    }
}
