using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Stats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GrifballWebApp.Server.Leaderboards;

/// <summary>All-time leaderboards over every season, imported from the historical archive or played in the app alike.</summary>
public class LeaderboardsService
{
    private const int PlayersShown = 25;
    private const int TeamsShown = 20;
    private const string CacheKey = "leaderboards";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    private readonly GrifballContext _context;
    private readonly SeasonStatsService _stats;
    private readonly IMemoryCache _cache;

    public LeaderboardsService(GrifballContext context, SeasonStatsService stats, IMemoryCache cache)
    {
        _context = context;
        _stats = stats;
        _cache = cache;
    }

    /// <param name="format">Count only the titles of seasons of this format; null: every format. A Late League season never
    /// counts (it has no champion).</param>
    public async Task<LeaderboardsDto> GetAsync(SeasonFormat? format, CancellationToken ct = default)
    {
        // Every season's titles and stats change only when a result is reported or an import runs: a few minutes stale is fine
        return (await _cache.GetOrCreateAsync((CacheKey, format), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            return await Compute(format, ct);
        }))!;
    }

    private async Task<LeaderboardsDto> Compute(SeasonFormat? format, CancellationToken ct)
    {
        // The names the app shows for users (gamertag, Discord name, display name)
        var names = (await _context.Users.AsNoTracking()
            .Select(u => new { u.Id, Gamertag = u.XboxUser != null ? u.XboxUser.Gamertag : null, Discord = u.DiscordUser != null ? u.DiscordUser.DiscordUsername : null, u.DisplayName, u.UserName })
            .ToListAsync(ct))
            .ToDictionary(u => u.Id, u => (string?)UserNames.Pick(u.Gamertag, u.Discord, u.DisplayName, u.UserName));

        // The titles counted: of the format asked for, never a Late League season's
        var titles = _context.PlayerChampionships.AsNoTracking()
            .Where(c => c.Season.Format != SeasonFormat.LateLeague && (format == null || c.Season.Format == format));

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
                Gamertag = names.GetValueOrDefault(t.UserID) ?? held.Last().GamertagUsed,
                RingsCount = t.Count,
                Titles = held.DistinctBy(c => (c.SeasonID, c.Tier)).Select(c => new TitleDto { SeasonID = c.SeasonID, SeasonName = c.SeasonName, Tier = c.Tier, TeamID = c.TeamID }).ToList(),
            };
        })
            .OrderByDescending(r => r.RingsCount)
            .ThenBy(r => r.Gamertag)
            .ToList();

        // A team is one season's: a dynasty is the teams of one league that carried a name from season to season. Only
        // title-winning teams are loaded.
        var champions = await _context.Teams.AsNoTracking()
            .Where(t => t.Season.Format != SeasonFormat.LateLeague && (format == null || t.Season.Format == format) && t.PlayerChampionships.Any())
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

        // Every season's stats, from its games or its stored totals (never both for a player's phase of a season), totalled
        // as a profile totals them: goals and carrier stops in one game, kills and games played across games
        var stats = await _stats.GetAsync(ct: ct);
        string NameOf(IGrouping<int, SeasonStatLine> player) => names.GetValueOrDefault(player.Key) ?? player.MaxBy(s => s.Season.Start)!.Gamertag;
        var careers = stats.GroupBy(s => s.UserID)
            .Select(p => new PlayerCareer(p.Key, NameOf(p), CareerTotals.AcrossGames(p), false))
            .ToList();
        var inOneGame = stats.GroupBy(s => s.UserID)
            .SelectMany(p => CareerTotals.ByGame(p).Select(game => new PlayerCareer(p.Key, NameOf(p), game, true)))
            .ToList();

        return new LeaderboardsDto
        {
            RingLeaders = ringLeaders,
            Dynasties = dynasties,
            GoalLeaders = Leaders(inOneGame, c => c.Goals ?? 0, c => c.GoalsPerGame, c => $"{c.GamesPlayed} GP"),
            KillLeaders = Leaders(careers, c => c.Kills, c => StatMath.PerGame(c.Kills, c.GamesPlayed), c => c.KdRatio is decimal kd ? $"{kd} K/D" : null),
            GamesPlayedLeaders = Leaders(careers, c => c.GamesPlayed, _ => null, c => $"{c.SeasonsCount} Seasons"),
            CarrierStopLeaders = Leaders(inOneGame, c => c.CarrierKills ?? 0, c => c.StopsPerGame, c => $"{c.GamesPlayed} GP"),
        };
    }

    private static List<MetricLeaderDto> Leaders(List<PlayerCareer> players, Func<GameCareerSummaryDto, int> value,
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
}
