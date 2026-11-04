using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.LateLeague;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Stats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GrifballWebApp.Server.Leaderboards;

/// <summary>All-time leaderboards over every season, imported from the historical archive or played in the app alike. The
/// career boards count Late League (matchmaking) games too; it has no titles.</summary>
public class LeaderboardsService
{
    private const int PlayersShown = 100;
    private const int TeamsShown = 100;
    private const int SearchResultsShown = 10;
    private const string CacheKey = "leaderboards";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    private readonly GrifballContext _context;
    private readonly SeasonStatsService _stats;
    private readonly MatchmakingStatsService _matchmaking;
    private readonly IMemoryCache _cache;

    public LeaderboardsService(GrifballContext context, SeasonStatsService stats, MatchmakingStatsService matchmaking, IMemoryCache cache)
    {
        _context = context;
        _stats = stats;
        _matchmaking = matchmaking;
        _cache = cache;
    }

    /// <param name="format">Count only the titles of seasons of this format; null: every format.</param>
    public async Task<LeaderboardsDto> GetAsync(SeasonFormat? format, CancellationToken ct = default)
    {
        // Every season's titles and stats change only when a result is reported or an import runs: a few minutes stale is fine
        return (await _cache.GetOrCreateAsync((CacheKey, format), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            return await Compute(format, ct);
        }))!;
    }

    /// <summary>A player's rank on every board: the user with this ID, or else the player a gamertag (current or past) is of.</summary>
    /// <param name="format">Count only the titles of seasons of this format; null: every format.</param>
    public async Task<PlayerAllTimeRankDto?> GetPlayerRankAsync(string? query, int? userId, SeasonFormat? format, CancellationToken ct = default)
    {
        int? targetUserId = userId;
        string? matchedAlias = null;

        if (targetUserId == null)
        {
            if (string.IsNullOrWhiteSpace(query))
                return null;

            (targetUserId, matchedAlias) = await FindPlayer(query.Trim(), ct);
        }

        if (targetUserId == null)
            return null;

        var targetId = targetUserId.Value;

        var targetUser = await _context.Users.AsNoTracking()
            .Include(u => u.XboxUser)
            .Include(u => u.DiscordUser)
            .Where(u => u.Id == targetId)
            .FirstOrDefaultAsync(ct);

        if (targetUser == null)
            return null;

        var primaryGamertag = UserNames.Pick(targetUser.XboxUser?.Gamertag, targetUser.DiscordUser?.DiscordUsername, targetUser.DisplayName, targetUser.UserName);

        if (matchedAlias != null && matchedAlias.Equals(primaryGamertag, StringComparison.OrdinalIgnoreCase))
            matchedAlias = null;

        var pastGamertags = targetUser.XboxUserID == null ? [] :
            await _context.XboxGamertagHistory.AsNoTracking()
                .Where(h => h.XboxUserID == targetUser.XboxUserID && h.NormalizedGamertag != HistoricalKeys.Normalize(primaryGamertag))
                .Select(h => h.Gamertag)
                .Distinct()
                .ToListAsync(ct);

        // Ranked as the boards rank: goals and carrier stops by the player's best game (the boards list a player once per
        // game), kills and games played across games, and titles of the format asked for
        var careers = await GetCareersAsync(ct);
        var rings = await GetRingCountsAsync(format, ct);
        var career = careers.ByUser.GetValueOrDefault(targetId);
        var games = careers.GamesByUser[targetId].ToList();
        var bestGoals = games.MaxBy(g => g.Goals ?? 0);
        var bestStops = games.MaxBy(g => g.CarrierKills ?? 0);
        var kills = career?.Kills ?? 0;
        var playerRings = rings.Counts.GetValueOrDefault(targetId);

        return new PlayerAllTimeRankDto
        {
            UserID = targetId,
            Gamertag = primaryGamertag,
            MatchedAlias = matchedAlias,
            PastGamertags = pastGamertags,
            IsDummyUser = targetUser.IsDummyUser,
            Rings = RankOn(playerRings, null, $"{playerRings} Titles", rings.Ranked),
            Goals = RankOn(bestGoals?.Goals ?? 0, bestGoals?.GoalsPerGame, InGame(bestGoals, bestGoals?.GoalsPerGame), careers.GoalsRanked),
            Kills = RankOn(kills, StatMath.PerGame(kills, career?.GamesPlayed ?? 0), career?.KdRatio is decimal kd ? $"{kd} K/D" : null, careers.KillsRanked),
            GamesPlayed = RankOn(career?.GamesPlayed ?? 0, null, $"{career?.SeasonsCount ?? 0} Seasons", careers.GamesPlayedRanked),
            CarrierStops = RankOn(bestStops?.CarrierKills ?? 0, bestStops?.StopsPerGame, InGame(bestStops, bestStops?.StopsPerGame), careers.StopsRanked),
        };
    }

    /// <summary>The player a gamertag is of: a user's name, a past gamertag, a name on a season's stats or a title, or
    /// else the best of a search. The alias is the past name matched.</summary>
    private async Task<(int? UserID, string? Alias)> FindPlayer(string clean, CancellationToken ct)
    {
        var normalized = HistoricalKeys.Normalize(clean);

        var directUser = await _context.Users.AsNoTracking()
            .Where(u => (u.XboxUser != null && u.XboxUser.Gamertag == clean)
                     || u.DisplayName == clean
                     || u.UserName == clean
                     || u.UserName == HistoricalKeys.DummyUserName(clean))
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);
        if (directUser != null)
            return (directUser, null);

        var historyMatch = await _context.XboxGamertagHistory.AsNoTracking()
            .Where(h => (h.NormalizedGamertag == normalized || h.Gamertag == clean) && h.XboxUser.User != null)
            .Select(h => new { UserID = h.XboxUser.User!.Id, h.Gamertag })
            .FirstOrDefaultAsync(ct);
        if (historyMatch != null)
            return (historyMatch.UserID, historyMatch.Gamertag);

        var statMatch = await _context.PlayerSeasonStats.AsNoTracking()
            .Where(s => s.Gamertag == clean)
            .Select(s => new { s.UserID, s.Gamertag })
            .FirstOrDefaultAsync(ct);
        if (statMatch != null)
            return (statMatch.UserID, statMatch.Gamertag);

        var titleMatch = await _context.PlayerChampionships.AsNoTracking()
            .Where(c => c.GamertagUsed == clean)
            .Select(c => new { c.UserID, c.GamertagUsed })
            .FirstOrDefaultAsync(ct);
        if (titleMatch != null)
            return (titleMatch.UserID, titleMatch.GamertagUsed);

        var fuzzy = (await SearchPlayersAsync(clean, ct)).FirstOrDefault();
        return (fuzzy?.UserID, fuzzy?.MatchedAlias);
    }

    /// <summary>Players whose name, past gamertag or name on a season's stats contains the query (two characters or more).</summary>
    public async Task<List<PlayerSearchResultDto>> SearchPlayersAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return [];

        var clean = query.Trim();
        var normalized = HistoricalKeys.Normalize(clean);
        var results = new List<PlayerSearchResultDto>();
        var seenUsers = new HashSet<int>();
        const string dummyPrefix = HistoricalKeys.DummyUserNamePrefix;

        // An archived player's user name is the prefix and their gamertag: only the gamertag is searched, so a query
        // such as "is" or ":" doesn't match every archived player
        var users = await _context.Users.AsNoTracking()
            .Include(u => u.XboxUser)
            .Include(u => u.DiscordUser)
            .Where(u => (u.XboxUser != null && u.XboxUser.Gamertag.Contains(clean))
                     || (u.DisplayName != null && u.DisplayName.Contains(clean))
                     || (u.UserName != null && !u.UserName.StartsWith(dummyPrefix) && u.UserName.Contains(clean))
                     || (u.UserName != null && u.UserName.StartsWith(dummyPrefix) && u.UserName.Substring(dummyPrefix.Length).Contains(clean)))
            .OrderBy(u => u.XboxUser != null ? u.XboxUser.Gamertag.Length : (u.DisplayName ?? u.UserName)!.Length)
            .Take(SearchResultsShown)
            .ToListAsync(ct);

        foreach (var u in users)
        {
            var name = UserNames.Pick(u.XboxUser?.Gamertag, u.DiscordUser?.DiscordUsername, u.DisplayName, u.UserName);
            if (seenUsers.Add(u.Id))
                results.Add(new PlayerSearchResultDto { UserID = u.Id, Gamertag = name, MatchedAlias = null });
        }

        if (results.Count >= SearchResultsShown)
            return results;

        var historyMatches = await _context.XboxGamertagHistory.AsNoTracking()
            .Where(h => (h.Gamertag.Contains(clean) || h.NormalizedGamertag.Contains(normalized)) && h.XboxUser.User != null)
            .Select(h => new
            {
                UserID = h.XboxUser.User!.Id,
                CurrentGamertag = h.XboxUser.Gamertag,
                DisplayName = h.XboxUser.User.DisplayName,
                UserName = h.XboxUser.User.UserName,
                DiscordUsername = h.XboxUser.User.DiscordUser != null ? h.XboxUser.User.DiscordUser.DiscordUsername : null,
                Alias = h.Gamertag
            })
            .Take(SearchResultsShown)
            .ToListAsync(ct);

        foreach (var h in historyMatches)
        {
            if (seenUsers.Add(h.UserID))
            {
                var primary = UserNames.Pick(h.CurrentGamertag, h.DiscordUsername, h.DisplayName, h.UserName);
                results.Add(new PlayerSearchResultDto { UserID = h.UserID, Gamertag = primary, MatchedAlias = AliasOf(h.Alias, primary) });
            }
        }

        if (results.Count >= SearchResultsShown)
            return results;

        var statMatches = await _context.PlayerSeasonStats.AsNoTracking()
            .Where(s => s.Gamertag.Contains(clean))
            .Select(s => new { s.UserID, s.Gamertag })
            .Distinct()
            .Take(SearchResultsShown)
            .ToListAsync(ct);

        var neededUserIds = statMatches.Where(s => !seenUsers.Contains(s.UserID)).Select(s => s.UserID).Distinct().ToList();
        var userMap = neededUserIds.Count == 0 ? [] :
            await _context.Users.AsNoTracking()
                .Include(u => u.XboxUser)
                .Include(u => u.DiscordUser)
                .Where(u => neededUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, ct);

        foreach (var s in statMatches)
        {
            if (seenUsers.Add(s.UserID))
            {
                var u = userMap[s.UserID];
                var primary = UserNames.Pick(u.XboxUser?.Gamertag, u.DiscordUser?.DiscordUsername, u.DisplayName, u.UserName);
                results.Add(new PlayerSearchResultDto { UserID = s.UserID, Gamertag = primary, MatchedAlias = AliasOf(s.Gamertag, primary) });
            }
        }

        return results.Take(SearchResultsShown).ToList();
    }

    /// <summary>The name matched, when it isn't the one the player is shown by.</summary>
    private static string? AliasOf(string matched, string primary) => matched.Equals(primary, StringComparison.OrdinalIgnoreCase) ? null : matched;

    /// <summary>
    /// Every player's career totals, across games and in each game, shared by every format's boards and the spotlight: the
    /// format filters titles only.
    /// </summary>
    private async Task<Careers> GetCareersAsync(CancellationToken ct)
    {
        return (await _cache.GetOrCreateAsync((CacheKey, "careers"), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;

            // The names the app shows for users (gamertag, Discord name, display name)
            var names = (await _context.Users.AsNoTracking()
                .Select(u => new { u.Id, Gamertag = u.XboxUser != null ? u.XboxUser.Gamertag : null, Discord = u.DiscordUser != null ? u.DiscordUser.DiscordUsername : null, u.DisplayName, u.UserName })
                .ToListAsync(ct))
                .ToDictionary(u => u.Id, u => (string?)UserNames.Pick(u.Gamertag, u.Discord, u.DisplayName, u.UserName));

            // Every season's stats, from its games or its stored totals (never both for a player's phase of a season), and Late
            // League's, totalled as a profile totals them: goals and carrier stops in one game, kills and games played across games
            var stats = (await _stats.GetAsync(ct: ct)).Concat<StatLine>(await _matchmaking.GetAsync(ct: ct)).ToList();
            var careers = new List<PlayerCareer>();
            var inOneGame = new List<PlayerCareer>();
            foreach (var player in stats.GroupBy(s => s.UserID))
            {
                var name = names.GetValueOrDefault(player.Key) ?? player.First().Gamertag;
                careers.Add(new PlayerCareer(player.Key, name, CareerTotals.AcrossGames(player), false));
                inOneGame.AddRange(CareerTotals.ByGame(player).Select(game => new PlayerCareer(player.Key, name, game, true)));
            }

            return new Careers(names, careers, inOneGame);
        }))!;
    }

    /// <summary>How many titles each player holds, of seasons of this format (null: every format).</summary>
    private async Task<RingCounts> GetRingCountsAsync(SeasonFormat? format, CancellationToken ct)
    {
        return (await _cache.GetOrCreateAsync((CacheKey, "rings", format), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            var counts = await _context.PlayerChampionships.AsNoTracking()
                .Where(c => format == null || c.Season.Format == format)
                .GroupBy(c => c.UserID)
                .Select(g => new { UserID = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.UserID, g => g.Count, ct);
            return new RingCounts(counts, Ranked(counts.Values.Select(v => (long)v)));
        }))!;
    }

    /// <summary>
    /// A player's rank on a board, ranked as the boards rank it: one more than the number ahead (ties share a
    /// rank), of everyone on the board. A player with none of the stat isn't on the board, so has no rank.
    /// </summary>
    private static MetricRankDetail RankOn(long playerValue, decimal? perGame, string? secondary, IReadOnlyList<long> ranked) => new()
    {
        Rank = playerValue > 0 ? ranked.Count(v => v > playerValue) + 1 : null,
        TotalRanked = ranked.Count,
        StatValue = playerValue,
        PerGame = perGame,
        SecondaryText = playerValue > 0 ? secondary : null,
    };

    /// <summary>The game a total in one game is of, with its per-game figure.</summary>
    private static string? InGame(GameCareerSummaryDto? game, decimal? perGame) =>
        game == null ? null : perGame.HasValue ? $"{perGame} / G in {game.GameVersionName}" : $"in {game.GameVersionName}";

    /// <summary>The values on a board, most first: only a value above zero is on it.</summary>
    private static List<long> Ranked(IEnumerable<long> values) => values.Where(v => v > 0).OrderByDescending(v => v).ToList();

    private async Task<LeaderboardsDto> Compute(SeasonFormat? format, CancellationToken ct)
    {
        var careers = await GetCareersAsync(ct);

        // The titles counted: of the format asked for
        var titles = _context.PlayerChampionships.AsNoTracking()
            .Where(c => format == null || c.Season.Format == format);

        // Who holds the most titles: counted in the database, the top players' titles then loaded
        var top = await titles
            .GroupBy(c => c.UserID)
            .Select(g => new { UserID = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.UserID)
            .Take(PlayersShown)
            .ToListAsync(ct);
        var topIDs = top.Select(t => t.UserID).ToList();
        var topTitles = await titles
            .Where(c => topIDs.Contains(c.UserID))
            .Select(c => new { c.UserID, c.GamertagUsed, c.SeasonID, c.Season.SeasonName, c.Season.SeasonStart, c.Tier, c.TeamID })
            .ToListAsync(ct);
        var ringLeaders = top.Select(t =>
        {
            var held = topTitles.Where(c => c.UserID == t.UserID).OrderBy(c => c.SeasonStart).ToList();
            return new RingLeaderDto
            {
                UserID = t.UserID,
                Gamertag = careers.Names.GetValueOrDefault(t.UserID) ?? held.Last().GamertagUsed,
                RingsCount = t.Count,
                Titles = held.DistinctBy(c => (c.SeasonID, c.Tier)).Select(c => new TitleDto { SeasonID = c.SeasonID, SeasonName = c.SeasonName, Tier = c.Tier, TeamID = c.TeamID }).ToList(),
            };
        })
            .OrderByDescending(r => r.RingsCount)
            .ThenBy(r => r.Gamertag)
            .ToList();
        AssignRanks(ringLeaders, r => r.RingsCount);

        // A team is one season's: a dynasty is the teams of one league that carried a name from season to season. Only
        // title-winning teams are loaded.
        var champions = await _context.Teams.AsNoTracking()
            .Where(t => (format == null || t.Season.Format == format) && t.PlayerChampionships.Any())
            .Select(t => new
            {
                t.TeamID,
                t.TeamName,
                t.SeasonID,
                t.Season.LeagueID,
                t.Season.SeasonName,
                t.Season.SeasonStart,
                Tiers = t.PlayerChampionships.Select(c => c.Tier).Distinct().ToList(),
            })
            .ToListAsync(ct);
        var dynasties = champions
            .GroupBy(t => (t.LeagueID, Name: HistoricalKeys.Normalize(t.TeamName)))
            .Select(g =>
            {
                var latest = g.MaxBy(t => t.SeasonStart)!;
                var titles = g.OrderBy(t => t.SeasonStart).SelectMany(t => t.Tiers.Select(tier => new TitleDto { SeasonID = t.SeasonID, SeasonName = t.SeasonName, Tier = tier, TeamID = t.TeamID }))
                    .DistinctBy(t => (t.SeasonID, t.Tier)).ToList();
                return new DynastyDto
                {
                    TeamName = latest.TeamName,
                    LatestTeamID = latest.TeamID,
                    LatestSeasonID = latest.SeasonID,
                    ChampionshipsCount = titles.Count,
                    SeasonsActive = g.Select(t => t.SeasonID).Distinct().Count(),
                    Titles = titles,
                };
            })
            .OrderByDescending(t => t.ChampionshipsCount)
            .ThenBy(t => t.TeamName)
            .Take(TeamsShown)
            .ToList();
        AssignRanks(dynasties, d => d.ChampionshipsCount);

        return new LeaderboardsDto
        {
            RingLeaders = ringLeaders,
            Dynasties = dynasties,
            GoalLeaders = Leaders(careers.InOneGame, c => c.Goals ?? 0, c => c.GoalsPerGame, c => $"{c.GamesPlayed} GP"),
            KillLeaders = Leaders(careers.AcrossGames, c => c.Kills, c => StatMath.PerGame(c.Kills, c.GamesPlayed), c => c.KdRatio is decimal kd ? $"{kd} K/D" : null),
            GamesPlayedLeaders = Leaders(careers.AcrossGames, c => c.GamesPlayed, _ => null, c => $"{c.SeasonsCount} Seasons"),
            CarrierStopLeaders = Leaders(careers.InOneGame, c => c.CarrierKills ?? 0, c => c.StopsPerGame, c => $"{c.GamesPlayed} GP"),
        };
    }

    /// <summary>Ranks a board sorted most first, in standard competition ranking: ties share a rank, and the next rank skips
    /// as many places as were tied (1, 1, 3).</summary>
    private static void AssignRanks<T>(List<T> list, Func<T, long> getValue) where T : IRanked
    {
        var rank = 1;
        for (var i = 0; i < list.Count; i++)
        {
            if (i > 0 && getValue(list[i]) < getValue(list[i - 1]))
                rank = i + 1;
            list[i].Rank = rank;
        }
    }

    private static List<MetricLeaderDto> Leaders(List<PlayerCareer> players, Func<GameCareerSummaryDto, long> value,
        Func<GameCareerSummaryDto, decimal?> perGame, Func<GameCareerSummaryDto, string?> secondary)
    {
        var leaders = players
            .Where(p => value(p.Totals) > 0)
            .OrderByDescending(p => value(p.Totals))
            .ThenBy(p => p.Gamertag)
            .Take(PlayersShown)
            .Select(p => new MetricLeaderDto
            {
                UserID = p.UserID,
                Gamertag = p.Gamertag,
                GameVersionName = p.InOneGame ? p.Totals.GameVersionName : null,
                StatValue = value(p.Totals),
                GamesPlayed = p.Totals.GamesPlayed,
                PerGame = perGame(p.Totals),
                SeasonsCount = p.Totals.SeasonsCount,
                SecondaryStat = secondary(p.Totals),
            })
            .ToList();
        AssignRanks(leaders, m => m.StatValue);
        return leaders;
    }

    private sealed record PlayerCareer(int UserID, string Gamertag, GameCareerSummaryDto Totals, bool InOneGame);

    /// <summary>Every player's totals across games and in each game, and each board's values for ranking a player on it.</summary>
    private sealed class Careers(Dictionary<int, string?> names, List<PlayerCareer> acrossGames, List<PlayerCareer> inOneGame)
    {
        public Dictionary<int, string?> Names { get; } = names;
        public List<PlayerCareer> AcrossGames { get; } = acrossGames;
        public List<PlayerCareer> InOneGame { get; } = inOneGame;
        public Dictionary<int, GameCareerSummaryDto> ByUser { get; } = acrossGames.ToDictionary(c => c.UserID, c => c.Totals);
        public ILookup<int, GameCareerSummaryDto> GamesByUser { get; } = inOneGame.ToLookup(c => c.UserID, c => c.Totals);
        public List<long> GoalsRanked { get; } = Ranked(inOneGame.Select(c => (long)(c.Totals.Goals ?? 0)));
        public List<long> KillsRanked { get; } = Ranked(acrossGames.Select(c => (long)c.Totals.Kills));
        public List<long> GamesPlayedRanked { get; } = Ranked(acrossGames.Select(c => (long)c.Totals.GamesPlayed));
        public List<long> StopsRanked { get; } = Ranked(inOneGame.Select(c => (long)(c.Totals.CarrierKills ?? 0)));
    }

    private sealed record RingCounts(Dictionary<int, int> Counts, List<long> Ranked);
}
