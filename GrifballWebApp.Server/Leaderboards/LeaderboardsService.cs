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
        return (await _cache.GetOrCreateAsync((CacheKey, format), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            return await Compute(format, ct);
        }))!;
    }

    public async Task<PlayerAllTimeRankDto?> GetPlayerRankAsync(string? query, int? userId, SeasonFormat? format, CancellationToken ct = default)
    {
        int? targetUserId = userId;
        string? matchedAlias = null;

        if (targetUserId == null)
        {
            if (string.IsNullOrWhiteSpace(query))
                return null;

            var clean = query.Trim();
            var normalized = HistoricalKeys.Normalize(clean);

            var directUser = await _context.Users.AsNoTracking()
                .Include(u => u.XboxUser)
                .Where(u => (u.XboxUser != null && u.XboxUser.Gamertag == clean)
                         || u.DisplayName == clean
                         || u.UserName == clean
                         || u.UserName == HistoricalKeys.DummyUserName(clean))
                .FirstOrDefaultAsync(ct);

            if (directUser != null)
            {
                targetUserId = directUser.Id;
            }
            else
            {
                var historyMatch = await _context.XboxGamertagHistory.AsNoTracking()
                    .Where(h => (h.NormalizedGamertag == normalized || h.Gamertag == clean) && h.XboxUser.User != null)
                    .Select(h => new { UserID = h.XboxUser.User!.Id, h.Gamertag })
                    .FirstOrDefaultAsync(ct);

                if (historyMatch != null)
                {
                    targetUserId = historyMatch.UserID;
                    matchedAlias = historyMatch.Gamertag;
                }
                else
                {
                    var statMatch = await _context.PlayerSeasonStats.AsNoTracking()
                        .Where(s => s.Gamertag == clean)
                        .Select(s => new { s.UserID, s.Gamertag })
                        .FirstOrDefaultAsync(ct);

                    if (statMatch != null)
                    {
                        targetUserId = statMatch.UserID;
                        matchedAlias = statMatch.Gamertag;
                    }
                    else
                    {
                        var titleMatch = await _context.PlayerChampionships.AsNoTracking()
                            .Where(c => c.GamertagUsed == clean)
                            .Select(c => new { c.UserID, c.GamertagUsed })
                            .FirstOrDefaultAsync(ct);

                        if (titleMatch != null)
                        {
                            targetUserId = titleMatch.UserID;
                            matchedAlias = titleMatch.GamertagUsed;
                        }
                    }
                }
            }

            if (targetUserId == null)
            {
                var fuzzy = (await SearchPlayersAsync(clean, ct)).FirstOrDefault();
                if (fuzzy != null)
                {
                    targetUserId = fuzzy.UserID;
                    matchedAlias = fuzzy.MatchedAlias;
                }
            }
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

        var primaryGamertag = UserNames.Pick(targetUser.XboxUser?.Gamertag, targetUser.DiscordUser?.DiscordUsername, targetUser.DisplayName, targetUser.UserName)
            ?? targetUser.DisplayName
            ?? targetUser.UserName
            ?? query?.Trim()
            ?? "Player";

        if (matchedAlias != null && matchedAlias.Equals(primaryGamertag, StringComparison.OrdinalIgnoreCase))
            matchedAlias = null;

        var pastGamertags = targetUser.XboxUserID == null ? [] :
            await _context.XboxGamertagHistory.AsNoTracking()
                .Where(h => h.XboxUserID == targetUser.XboxUserID && h.NormalizedGamertag != HistoricalKeys.Normalize(primaryGamertag))
                .Select(h => h.Gamertag)
                .Distinct()
                .ToListAsync(ct);

        var fullData = await GetFullDataAsync(format, ct);

        var playerRings = fullData.RingsByUser.GetValueOrDefault(targetId, 0);
        var ringsDetail = ComputeRankDetail(playerRings, null, $"{playerRings} Titles", fullData.AllRingCounts, fullData.TotalPlayersCount);

        var playerGoals = fullData.GoalsByUser.GetValueOrDefault(targetId, 0);
        var playerGP = fullData.GamesPlayedByUser.GetValueOrDefault(targetId, 0);
        var goalsPerGame = StatMath.PerGame(playerGoals, playerGP);
        var goalsDetail = ComputeRankDetail(playerGoals, goalsPerGame, goalsPerGame.HasValue ? $"{goalsPerGame} / G" : null, fullData.AllGoalsCounts, fullData.TotalPlayersCount);

        var playerKills = fullData.KillsByUser.GetValueOrDefault(targetId, 0);
        var playerDeaths = fullData.DeathsByUser.GetValueOrDefault(targetId, 0);
        var kd = StatMath.KdRatio((int)Math.Min(playerKills, int.MaxValue), playerDeaths);
        var killsDetail = ComputeRankDetail(playerKills, StatMath.PerGame(playerKills, playerGP), kd.HasValue ? $"{kd} K/D" : null, fullData.AllKillsCounts, fullData.TotalPlayersCount);

        var seasonsCount = fullData.SeasonsByUser.GetValueOrDefault(targetId, 0);
        var gpDetail = ComputeRankDetail(playerGP, null, $"{seasonsCount} Seasons", fullData.AllGamesPlayedCounts, fullData.TotalPlayersCount);

        var playerStops = fullData.StopsByUser.GetValueOrDefault(targetId, 0);
        var stopsPerGame = StatMath.PerGame(playerStops, playerGP);
        var stopsDetail = ComputeRankDetail(playerStops, stopsPerGame, stopsPerGame.HasValue ? $"{stopsPerGame} / G" : null, fullData.AllStopsCounts, fullData.TotalPlayersCount);

        return new PlayerAllTimeRankDto
        {
            UserID = targetId,
            Gamertag = primaryGamertag,
            MatchedAlias = matchedAlias,
            PastGamertags = pastGamertags,
            IsDummyUser = targetUser.IsDummyUser,
            Rings = ringsDetail,
            Goals = goalsDetail,
            Kills = killsDetail,
            GamesPlayed = gpDetail,
            CarrierStops = stopsDetail,
        };
    }

    public async Task<List<PlayerSearchResultDto>> SearchPlayersAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return [];

        var clean = query.Trim();
        var normalized = HistoricalKeys.Normalize(clean);
        var results = new List<PlayerSearchResultDto>();
        var seenUsers = new HashSet<int>();

        var users = await _context.Users.AsNoTracking()
            .Include(u => u.XboxUser)
            .Include(u => u.DiscordUser)
            .Where(u => (u.XboxUser != null && u.XboxUser.Gamertag.Contains(clean))
                     || (u.DisplayName != null && u.DisplayName.Contains(clean))
                     || (u.UserName != null && u.UserName.Contains(clean)))
            .OrderBy(u => u.XboxUser != null ? u.XboxUser.Gamertag.Length : (u.DisplayName ?? u.UserName)!.Length)
            .Take(10)
            .ToListAsync(ct);

        foreach (var u in users)
        {
            var name = UserNames.Pick(u.XboxUser?.Gamertag, u.DiscordUser?.DiscordUsername, u.DisplayName, u.UserName);
            if (name != null && seenUsers.Add(u.Id))
            {
                results.Add(new PlayerSearchResultDto
                {
                    UserID = u.Id,
                    Gamertag = name,
                    MatchedAlias = null,
                });
            }
        }

        if (results.Count >= 10)
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
            .Take(10)
            .ToListAsync(ct);

        foreach (var h in historyMatches)
        {
            if (seenUsers.Add(h.UserID))
            {
                var primary = UserNames.Pick(h.CurrentGamertag, h.DiscordUsername, h.DisplayName, h.UserName) ?? h.CurrentGamertag;
                var isAlias = !h.Alias.Equals(primary, StringComparison.OrdinalIgnoreCase);
                results.Add(new PlayerSearchResultDto
                {
                    UserID = h.UserID,
                    Gamertag = primary,
                    MatchedAlias = isAlias ? h.Alias : null,
                });
            }
        }

        if (results.Count >= 10)
            return results;

        var statMatches = await _context.PlayerSeasonStats.AsNoTracking()
            .Where(s => s.Gamertag.Contains(clean))
            .Select(s => new { s.UserID, s.Gamertag })
            .Distinct()
            .Take(10)
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
                userMap.TryGetValue(s.UserID, out var u);
                var primary = u != null
                    ? (UserNames.Pick(u.XboxUser?.Gamertag, u.DiscordUser?.DiscordUsername, u.DisplayName, u.UserName) ?? s.Gamertag)
                    : s.Gamertag;
                var isAlias = !s.Gamertag.Equals(primary, StringComparison.OrdinalIgnoreCase);
                results.Add(new PlayerSearchResultDto
                {
                    UserID = s.UserID,
                    Gamertag = primary,
                    MatchedAlias = isAlias ? s.Gamertag : null,
                });
            }
        }

        return results.Take(10).ToList();
    }

    private async Task<FullLeaderboardData> GetFullDataAsync(SeasonFormat? format, CancellationToken ct)
    {
        return (await _cache.GetOrCreateAsync((CacheKey, "full", format), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;

            var titles = _context.PlayerChampionships.AsNoTracking()
                .Where(c => format == null || c.Season.Format == format);

            var titleGroups = await titles
                .GroupBy(c => c.UserID)
                .Select(g => new { UserID = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var ringsByUser = titleGroups.ToDictionary(g => g.UserID, g => g.Count);
            var allRingCounts = ringsByUser.Values.OrderByDescending(v => v).Select(v => (long)v).ToList();

            var stats = (await _stats.GetAsync(ct: ct)).Concat<StatLine>(await _matchmaking.GetAsync(ct: ct)).ToList();

            var totalPlayers = await _context.Users.AsNoTracking().CountAsync(ct);

            var goalsByUser = stats.GroupBy(s => s.UserID).ToDictionary(g => g.Key, g => (long)g.Sum(s => s.Goals));
            var allGoalsCounts = goalsByUser.Values.OrderByDescending(v => v).ToList();

            var killsByUser = stats.GroupBy(s => s.UserID).ToDictionary(g => g.Key, g => (long)g.Sum(s => s.Kills));
            var allKillsCounts = killsByUser.Values.OrderByDescending(v => v).ToList();

            var deathsByUser = stats.GroupBy(s => s.UserID).ToDictionary(g => g.Key, g => g.Sum(s => s.Deaths));

            var gpByUser = stats.GroupBy(s => s.UserID).ToDictionary(g => g.Key, g => g.Sum(s => s.GamesPlayed));
            var seasonsByUser = stats.GroupBy(s => s.UserID).ToDictionary(g => g.Key, g => g.Select(s => s.SeasonKey).Where(k => k != null).Distinct().Count());
            var allGpCounts = gpByUser.Values.OrderByDescending(v => v).Select(v => (long)v).ToList();

            var stopsByUser = stats.GroupBy(s => s.UserID).ToDictionary(g => g.Key, g => (long)g.Sum(s => s.CarrierKills));
            var allStopsCounts = stopsByUser.Values.OrderByDescending(v => v).ToList();

            return new FullLeaderboardData(
                TotalPlayersCount: totalPlayers,
                RingsByUser: ringsByUser,
                AllRingCounts: allRingCounts,
                GoalsByUser: goalsByUser,
                AllGoalsCounts: allGoalsCounts,
                KillsByUser: killsByUser,
                DeathsByUser: deathsByUser,
                AllKillsCounts: allKillsCounts,
                GamesPlayedByUser: gpByUser,
                SeasonsByUser: seasonsByUser,
                AllGamesPlayedCounts: allGpCounts,
                StopsByUser: stopsByUser,
                AllStopsCounts: allStopsCounts
            );
        }))!;
    }

    private static MetricRankDetail ComputeRankDetail(long playerValue, decimal? perGame, string? secondary, IReadOnlyList<long> allValues, int totalUniverse)
    {
        var strictlyHigher = 0;
        var nonZeroCount = 0;
        foreach (var val in allValues)
        {
            if (val > 0) nonZeroCount++;
            if (val > playerValue) strictlyHigher++;
        }

        int rank;
        if (playerValue <= 0)
        {
            rank = nonZeroCount + 1;
        }
        else
        {
            rank = strictlyHigher + 1;
        }

        return new MetricRankDetail
        {
            Rank = rank,
            TotalRanked = totalUniverse,
            StatValue = playerValue,
            PerGame = perGame,
            SecondaryText = secondary,
        };
    }

    private async Task<LeaderboardsDto> Compute(SeasonFormat? format, CancellationToken ct)
    {
        var names = (await _context.Users.AsNoTracking()
            .Select(u => new { u.Id, Gamertag = u.XboxUser != null ? u.XboxUser.Gamertag : null, Discord = u.DiscordUser != null ? u.DiscordUser.DiscordUsername : null, u.DisplayName, u.UserName })
            .ToListAsync(ct))
            .ToDictionary(u => u.Id, u => (string?)UserNames.Pick(u.Gamertag, u.Discord, u.DisplayName, u.UserName));

        // The titles counted: of the format asked for
        var titles = _context.PlayerChampionships.AsNoTracking()
            .Where(c => format == null || c.Season.Format == format);

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
                Gamertag = names.GetValueOrDefault(t.UserID) ?? held.Last().GamertagUsed,
                RingsCount = t.Count,
                Titles = held.DistinctBy(c => (c.SeasonID, c.Tier)).Select(c => new TitleDto { SeasonID = c.SeasonID, SeasonName = c.SeasonName, Tier = c.Tier, TeamID = c.TeamID }).ToList(),
            };
        })
            .OrderByDescending(r => r.RingsCount)
            .ThenBy(r => r.Gamertag)
            .ToList();

        AssignRanks(ringLeaders, r => r.RingsCount);

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

        // Every season's stats, from its games or its stored totals (never both for a player's phase of a season), and Late
        // League's, totalled as a profile totals them: goals and carrier stops in one game, kills and games played across games
        var stats = (await _stats.GetAsync(ct: ct)).Concat<StatLine>(await _matchmaking.GetAsync(ct: ct)).ToList();
        string NameOf(IGrouping<int, StatLine> player) => names.GetValueOrDefault(player.Key) ?? player.First().Gamertag;
        var careers = stats.GroupBy(s => s.UserID)
            .Select(p => new PlayerCareer(p.Key, NameOf(p), CareerTotals.AcrossGames(p), false))
            .ToList();

        var inOneGame = stats.GroupBy(s => s.UserID)
            .SelectMany(p => CareerTotals.ByGame(p).Select(game => new PlayerCareer(p.Key, NameOf(p), game, true)))
            .ToList();

        var goalLeaders = Leaders(inOneGame, c => c.Goals ?? 0, c => c.GoalsPerGame, c => $"{c.GamesPlayed} GP");
        AssignRanks(goalLeaders, m => m.StatValue);

        var killLeaders = Leaders(careers, c => c.Kills, c => StatMath.PerGame(c.Kills, c.GamesPlayed), c => c.KdRatio is decimal kd ? $"{kd} K/D" : null);
        AssignRanks(killLeaders, m => m.StatValue);

        var gamesPlayedLeaders = Leaders(careers, c => c.GamesPlayed, _ => null, c => $"{c.SeasonsCount} Seasons");
        AssignRanks(gamesPlayedLeaders, m => m.StatValue);

        var stopLeaders = Leaders(inOneGame, c => c.CarrierKills ?? 0, c => c.StopsPerGame, c => $"{c.GamesPlayed} GP");
        AssignRanks(stopLeaders, m => m.StatValue);

        return new LeaderboardsDto
        {
            RingLeaders = ringLeaders,
            Dynasties = dynasties,
            GoalLeaders = goalLeaders,
            KillLeaders = killLeaders,
            GamesPlayedLeaders = gamesPlayedLeaders,
            CarrierStopLeaders = stopLeaders,
        };
    }

    private static void AssignRanks<T>(List<T> list, Func<T, long> getValue)
    {
        var rank = 1;
        for (var i = 0; i < list.Count; i++)
        {
            if (i > 0 && getValue(list[i]) < getValue(list[i - 1]))
                rank = i + 1;

            if (list[i] is RingLeaderDto r) r.Rank = rank;
            else if (list[i] is DynastyDto d) d.Rank = rank;
            else if (list[i] is MetricLeaderDto m) m.Rank = rank;
        }
    }

    private static List<MetricLeaderDto> Leaders(List<PlayerCareer> players, Func<GameCareerSummaryDto, long> value,
        Func<GameCareerSummaryDto, decimal?> perGame, Func<GameCareerSummaryDto, string?> secondary) => players
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

    private sealed record PlayerCareer(int UserID, string Gamertag, GameCareerSummaryDto Totals, bool InOneGame);

    private sealed record FullLeaderboardData(
        int TotalPlayersCount,
        Dictionary<int, int> RingsByUser,
        List<long> AllRingCounts,
        Dictionary<int, long> GoalsByUser,
        List<long> AllGoalsCounts,
        Dictionary<int, long> KillsByUser,
        Dictionary<int, int> DeathsByUser,
        List<long> AllKillsCounts,
        Dictionary<int, int> GamesPlayedByUser,
        Dictionary<int, int> SeasonsByUser,
        List<long> AllGamesPlayedCounts,
        Dictionary<int, long> StopsByUser,
        List<long> AllStopsCounts
    );
}
