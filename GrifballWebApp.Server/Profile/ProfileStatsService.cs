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

        // The gamertags the user went by before, latest first, never the one shown now (renamed A to B and back to A, A is
        // in its history too). This endpoint is public: only gamertags, never a login name or an XUID.
        var shown = user.XboxUser?.Gamertag ?? (user.IsDummyUser ? user.DisplayName : null);
        var pastGamertags = new List<string>();
        var seenNormalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (shown is not null)
            seenNormalized.Add(HistoricalKeys.Normalize(shown));
        // What says which player of the alias registry the user is (see below)
        var registryEvidence = new List<string>();
        void Add(string tag)
        {
            if (seenNormalized.Add(HistoricalKeys.Normalize(tag)))
                pastGamertags.Add(tag);
        }

        // The Xbox account's renames: not the gamertag it has now, which says nothing about who had it before
        if (user.XboxUserID is not null)
        {
            var history = await _context.XboxGamertagHistory
                .AsNoTracking()
                .Where(h => h.XboxUserID == user.XboxUserID)
                .OrderBy(h => h.LastSeen == null)
                .ThenByDescending(h => h.LastSeen)
                .ThenBy(h => h.FirstSeen)
                .ThenBy(h => h.XboxGamertagHistoryID)
                .Select(h => h.Gamertag)
                .ToListAsync(ct);
            var renames = history.Where(t => HistoricalKeys.Normalize(t) != HistoricalKeys.Normalize(user.XboxUser!.Gamertag)).ToList();
            registryEvidence.AddRange(renames);
            foreach (var tag in renames)
                Add(tag);
        }

        // The gamertags of their historical stat rows, then titles, each latest season first (ordered here: SQL DISTINCT
        // keeps no order)
        var statTags = await _context.PlayerSeasonStats
            .AsNoTracking()
            .Where(s => s.UserID == userID && !string.IsNullOrWhiteSpace(s.Gamertag))
            .Select(s => new { s.Gamertag, s.Season.SeasonStart })
            .ToListAsync(ct);
        foreach (var tag in statTags.OrderByDescending(s => s.SeasonStart).Select(s => s.Gamertag))
            Add(tag);

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
        foreach (var title in titles.Where(t => !string.IsNullOrWhiteSpace(t.GamertagUsed)))
            Add(title.GamertagUsed);

        // Import placeholders merged into the user (not undone): their gamertag, without the XUID a placeholder of an Xbox
        // account is named after too. Any other account merged in is left out: its user name is a private login name.
        var mergedPlaceholders = await _context.UserMerges
            .AsNoTracking()
            .Where(m => m.TargetUserID == userID && m.UndoneAt == null && m.SourceUserName != null && m.SourceUserName.StartsWith(HistoricalKeys.DummyUserNamePrefix))
            .OrderByDescending(m => m.MergedAt)
            .Select(m => m.SourceUserName!)
            .ToListAsync(ct);
        foreach (var name in mergedPlaceholders)
        {
            registryEvidence.Add(PlaceholderGamertag(name));
            Add(PlaceholderGamertag(name));
        }

        // The alias registry's other gamertags, only of a player the user is by a decision: their Xbox account's own past
        // gamertags, a placeholder merged into them that wasn't undone, or the canonical placeholder they are. Never by a
        // gamertag only on their own rows or in their name (the import keeps two players of one handle apart, and an undone
        // merge restores a placeholder with its own rows), nor by a display name, login name or current gamertag, which
        // anyone can have. An alias whose placeholder is still a user of its own is that user's (kept apart, its merge
        // undone, or not consolidated yet), not this one's.
        if (user.IsDummyUser && user.UserName?.StartsWith(HistoricalKeys.DummyUserNamePrefix, StringComparison.OrdinalIgnoreCase) == true)
        {
            var own = PlaceholderGamertag(user.UserName);
            if (HistoricalKeys.Normalize(HistoricalAliases.CanonicalGamertag(own)) == HistoricalKeys.Normalize(own))
                registryEvidence.Add(own);
        }
        var aliases = registryEvidence
            .SelectMany(t => HistoricalAliases.GetAliasesFor(t).Prepend(HistoricalAliases.CanonicalGamertag(t)))
            .Where(a => !seenNormalized.Contains(HistoricalKeys.Normalize(a)))
            .DistinctBy(HistoricalKeys.Normalize)
            .ToList();
        if (aliases.Count > 0)
        {
            var names = aliases.Select(a => HistoricalKeys.DummyUserName(a).ToUpperInvariant()).ToList();
            var apart = (await _context.Users.AsNoTracking()
                    .Where(u => u.Id != userID && u.IsDummyUser && names.Contains(u.NormalizedUserName!))
                    .Select(u => u.UserName!)
                    .ToListAsync(ct))
                .Select(n => HistoricalKeys.Normalize(PlaceholderGamertag(n)))
                .ToHashSet();
            foreach (var alias in aliases.Where(a => !apart.Contains(HistoricalKeys.Normalize(a))))
            {
                seenNormalized.Add(HistoricalKeys.Normalize(alias));
                pastGamertags.Add(alias);
            }
        }

        var seasons = await SeasonInfoDto.LoadAsync(_context, titles.Select(c => c.SeasonID), ct);

        // Season stats: from the season's games where it has them, otherwise the season totals stored for it
        var lines = await _stats.GetAsync(userID: userID, ct: ct);
        // Late League: matchmaking, not seasons, but the player's games all the same
        var lateLeague = await _matchmaking.GetAsync(userID: userID, ct: ct);
        var career = lines.Concat<StatLine>(lateLeague).ToList();
        var serviceRecord = await ServiceRecord.LoadAsync(_context, user, lines, lateLeague, ct);

        return new ProfileStatsDto
        {
            UserID = user.Id,
            Gamertag = shown,
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

    /// <summary>The gamertag an import placeholder's user name ("historical:&lt;gamertag&gt;", or "historical:&lt;gamertag&gt;:&lt;XUID&gt;"
    /// for an Xbox account's) is of.</summary>
    private static string PlaceholderGamertag(string userName)
        => System.Text.RegularExpressions.Regex.Replace(userName[HistoricalKeys.DummyUserNamePrefix.Length..], ":[0-9]+$", "");
}
