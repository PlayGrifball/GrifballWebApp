using GrifballWebApp.Database;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.Stats;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Profile;

/// <summary>A player's profile: who they are, their titles, their season lines and their career totals.</summary>
public class ProfileStatsService
{
    private readonly GrifballContext _context;
    private readonly SeasonStatsService _stats;

    public ProfileStatsService(GrifballContext context, SeasonStatsService stats)
    {
        _context = context;
        _stats = stats;
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
            : (await _context.XboxGamertagHistory
                .AsNoTracking()
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

        var titles = await _context.PlayerChampionships
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
        var seasons = await SeasonInfoDto.LoadAsync(_context, titles.Select(c => c.SeasonID), ct);

        // Season stats: from the season's games where it has them, otherwise the season totals stored for it
        var lines = await _stats.GetAsync(userID: userID, ct: ct);

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
            CareerByGame = CareerTotals.ByGame(lines),
            OverallCareer = lines.Count > 0 ? CareerTotals.AcrossGames(lines) : null,
            Seasons = lines.Select(PlayerSeasonStatDetailDto.From).ToList(),
        };
    }
}
