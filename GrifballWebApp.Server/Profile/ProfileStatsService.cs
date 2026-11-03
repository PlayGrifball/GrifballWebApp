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
    private readonly SeasonStatsService _stats;
    private readonly MatchmakingStatsService _matchmaking;

    public ProfileStatsService(GrifballContext context, SeasonStatsService stats, MatchmakingStatsService matchmaking)
    {
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
        var primaryNorm = user.DisplayName is null ? current : HistoricalKeys.Normalize(user.DisplayName);

        var pastGamertags = new List<string>();
        var seenNormalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (current != null)
            seenNormalized.Add(current);
        if (primaryNorm != null)
            seenNormalized.Add(primaryNorm);

        if (user.XboxUserID is not null)
        {
            var history = await _context.XboxGamertagHistory
                .AsNoTracking()
                .Where(h => h.XboxUserID == user.XboxUserID)
                .OrderBy(h => h.LastSeen == null)
                .ThenByDescending(h => h.LastSeen)
                .ThenBy(h => h.FirstSeen)
                .ThenBy(h => h.XboxGamertagHistoryID)
                .Select(h => new { h.Gamertag, h.NormalizedGamertag })
                .ToListAsync(ct);

            foreach (var h in history)
            {
                if (seenNormalized.Add(h.NormalizedGamertag))
                {
                    pastGamertags.Add(h.Gamertag);
                }
            }
        }

        // Also gather gamertags used in historical stats and championships (for dummy accounts or merged accounts)
        var statTags = await _context.PlayerSeasonStats
            .AsNoTracking()
            .Where(s => s.UserID == userID && !string.IsNullOrWhiteSpace(s.Gamertag))
            .OrderByDescending(s => s.Season.SeasonStart)
            .Select(s => s.Gamertag)
            .Distinct()
            .ToListAsync(ct);

        foreach (var tag in statTags)
        {
            var norm = HistoricalKeys.Normalize(tag);
            if (seenNormalized.Add(norm))
            {
                pastGamertags.Add(tag);
            }
        }

        var titleTags = await _context.PlayerChampionships
            .AsNoTracking()
            .Where(c => c.UserID == userID && !string.IsNullOrWhiteSpace(c.GamertagUsed))
            .OrderByDescending(c => c.Season.SeasonStart)
            .Select(c => c.GamertagUsed)
            .Distinct()
            .ToListAsync(ct);

        foreach (var tag in titleTags)
        {
            var norm = HistoricalKeys.Normalize(tag);
            if (seenNormalized.Add(norm))
            {
                pastGamertags.Add(tag);
            }
        }

        // Also include previous usernames from merges into this user
        var mergedSourceNames = await _context.UserMerges
            .AsNoTracking()
            .Where(m => m.TargetUserID == userID && m.SourceUserName != null)
            .OrderByDescending(m => m.MergedAt)
            .Select(m => m.SourceUserName!)
            .ToListAsync(ct);

        foreach (var sourceName in mergedSourceNames)
        {
            var cleanTag = sourceName.StartsWith("historical:", StringComparison.OrdinalIgnoreCase)
                ? sourceName["historical:".Length..]
                : sourceName;
            var norm = HistoricalKeys.Normalize(cleanTag);
            if (seenNormalized.Add(norm))
            {
                pastGamertags.Add(cleanTag);
            }
        }

        // Also include version-controlled historical aliases mapped to this player
        var candidateNames = new List<string?> { user.DisplayName, user.XboxUser?.Gamertag, user.UserName };
        foreach (var name in candidateNames)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            var cleanCandidate = name.StartsWith("historical:", StringComparison.OrdinalIgnoreCase) ? name["historical:".Length..] : name;
            foreach (var alias in HistoricalAliases.GetAliasesFor(cleanCandidate))
            {
                var norm = HistoricalKeys.Normalize(alias);
                if (seenNormalized.Add(norm))
                {
                    pastGamertags.Add(alias);
                }
            }
        }

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
        // Late League: matchmaking, not seasons, but the player's games all the same
        var lateLeague = await _matchmaking.GetAsync(userID: userID, ct: ct);
        var career = lines.Concat<StatLine>(lateLeague).ToList();

        return new ProfileStatsDto
        {
            UserID = user.Id,
            Gamertag = user.XboxUser?.Gamertag ?? (user.IsDummyUser ? user.DisplayName : null),
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
        };
    }
}
