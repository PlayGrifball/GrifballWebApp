using GrifballWebApp.Database;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Historical;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GrifballWebApp.Server.Leaderboards;

/// <summary>All-time leaderboards over every season, imported from the historical archive or played in the app alike.</summary>
[Route("[controller]/[action]")]
[ApiController]
public class LeaderboardsController : ControllerBase
{
    private const int PlayersShown = 25;
    private const int TeamsShown = 20;

    private readonly GrifballContext _context;
    private readonly GrifballWebApp.Server.Stats.SeasonStatsService _stats;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "leaderboards";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    public LeaderboardsController(GrifballContext context, GrifballWebApp.Server.Stats.SeasonStatsService stats, IMemoryCache cache)
    {
        _context = context;
        _stats = stats;
        _cache = cache;
    }

    [HttpGet(Name = "GetLeaderboards")]
    public async Task<LeaderboardsDto> GetLeaderboards(CancellationToken ct)
    {
        // Every season's titles and stats change only when a result is reported or an import runs: a few minutes stale is fine
        return (await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            return await Compute(ct);
        }))!;
    }

    private async Task<LeaderboardsDto> Compute(CancellationToken ct)
    {
        // The names the app shows for users (gamertag, Discord name, display name)
        var names = (await _context.Users.AsNoTracking()
            .Select(u => new { u.Id, Gamertag = u.XboxUser != null ? u.XboxUser.Gamertag : null, Discord = u.DiscordUser != null ? u.DiscordUser.DiscordUsername : null, u.DisplayName, u.UserName })
            .ToListAsync(ct))
            .ToDictionary(u => u.Id, u => (string?)UserNames.Pick(u.Gamertag, u.Discord, u.DisplayName, u.UserName));

        // Who holds the most titles: counted in the database, the top players' titles then loaded
        var top = await _context.PlayerChampionships.AsNoTracking()
            .GroupBy(c => c.UserID)
            .Select(g => new { UserID = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.UserID)
            .Take(PlayersShown)
            .ToListAsync(ct);
        var topIDs = top.Select(t => t.UserID).ToList();
        var topTitles = await _context.PlayerChampionships.AsNoTracking()
            .Where(c => topIDs.Contains(c.UserID))
            .Select(c => new { c.UserID, c.GamertagUsed, c.Season.SeasonName, c.Season.SeasonStart, c.Tier })
            .ToListAsync(ct);
        var ringLeaders = top.Select(t =>
        {
            var held = topTitles.Where(c => c.UserID == t.UserID).OrderBy(c => c.SeasonStart).ToList();
            return new RingLeaderDto
            {
                UserID = t.UserID,
                Gamertag = names.GetValueOrDefault(t.UserID) ?? held.Last().GamertagUsed,
                RingsCount = t.Count,
                Titles = held.Select(c => $"{c.SeasonName} ({c.Tier})").Distinct().ToList(),
            };
        })
            .OrderByDescending(r => r.RingsCount)
            .ThenBy(r => r.Gamertag)
            .ToList();

        // A team is one season's: a dynasty is the teams of one league that carried a name from season to season. Only
        // title-winning teams are loaded.
        var champions = await _context.Teams.AsNoTracking()
            .Where(t => t.PlayerChampionships.Any())
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
                var titles = g.OrderBy(t => t.SeasonStart).SelectMany(t => t.Tiers.Select(tier => $"{t.SeasonName} ({tier})")).Distinct().ToList();
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

        // Every season's stats, from its games or its stored totals (never both for a player's phase of a season)
        var stats = await _stats.GetAsync(ct: ct);

        var players = stats
            .GroupBy(s => s.UserID)
            .Select(g =>
            {
                var kills = g.Sum(s => s.Kills);
                var deaths = g.Sum(s => s.Deaths);
                return new PlayerTotals(
                    g.Key,
                    names.GetValueOrDefault(g.Key) ?? g.MaxBy(s => s.SeasonStart)!.Gamertag,
                    g.Sum(s => s.GamesPlayed),
                    g.Sum(s => s.Goals),
                    kills,
                    g.Sum(s => s.CarrierKills),
                    g.Select(s => s.SeasonID).Distinct().Count(),
                    deaths > 0 ? Math.Round((decimal)kills / deaths, 2) : kills);
            })
            .ToList();

        List<MetricLeaderDto> Leaders(Func<PlayerTotals, int> value, Func<PlayerTotals, string> secondary, bool perGame = true) => players
            .Where(p => value(p) > 0)
            .OrderByDescending(p => value(p))
            .ThenBy(p => p.Gamertag)
            .Take(PlayersShown)
            .Select(p => new MetricLeaderDto
            {
                UserID = p.UserID,
                Gamertag = p.Gamertag,
                StatValue = value(p),
                GamesPlayed = p.GamesPlayed,
                PerGame = perGame && p.GamesPlayed > 0 ? Math.Round((decimal)value(p) / p.GamesPlayed, 2) : null,
                SeasonsCount = p.SeasonsCount,
                SecondaryStat = secondary(p),
            })
            .ToList();

        return new LeaderboardsDto
        {
            RingLeaders = ringLeaders,
            Dynasties = dynasties,
            GoalLeaders = Leaders(p => p.Goals, p => $"{p.GamesPlayed} GP"),
            KillLeaders = Leaders(p => p.Kills, p => $"{p.KdRatio} K/D"),
            GamesPlayedLeaders = Leaders(p => p.GamesPlayed, p => $"{p.SeasonsCount} Seasons", perGame: false),
            CarrierStopLeaders = Leaders(p => p.CarrierKills, p => $"{p.GamesPlayed} GP"),
        };
    }

    private sealed record PlayerTotals(int UserID, string Gamertag, int GamesPlayed, int Goals, int Kills, int CarrierKills, int SeasonsCount, decimal KdRatio);
}
