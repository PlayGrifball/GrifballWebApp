using GrifballWebApp.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Leaderboards;

/// <summary>All-time leaderboards over every season, imported from the historical archive or played in the app alike.</summary>
[Route("[controller]/[action]")]
[ApiController]
public class LeaderboardsController : ControllerBase
{
    private const int PlayersShown = 25;
    private const int TeamsShown = 20;

    private readonly GrifballContext _context;

    public LeaderboardsController(GrifballContext context)
    {
        _context = context;
    }

    [HttpGet(Name = "GetLeaderboards")]
    public async Task<LeaderboardsDto> GetLeaderboards(CancellationToken ct)
    {
        var titles = await _context.PlayerChampionships.AsNoTracking()
            .Select(c => new
            {
                c.PlayerChampionshipID,
                c.UserID,
                c.GamertagUsed,
                Current = c.User.XboxUser != null ? c.User.XboxUser.Gamertag : null,
                c.SeasonID,
                c.Season.SeasonName,
                c.Season.SeasonStart,
                c.Tier,
                c.TeamID,
                c.Team.TeamName,
            })
            .ToListAsync(ct);

        var ringLeaders = titles
            .GroupBy(c => c.UserID)
            .Select(g => new RingLeaderDto
            {
                UserID = g.Key,
                Gamertag = g.Select(c => c.Current).FirstOrDefault(n => n is not null) ?? g.MaxBy(c => c.SeasonStart)!.GamertagUsed,
                RingsCount = g.Count(),
                Titles = g.OrderBy(c => c.SeasonStart).Select(c => $"{c.SeasonName} ({c.Tier})").Distinct().ToList(),
            })
            .OrderByDescending(r => r.RingsCount)
            .ThenBy(r => r.Gamertag)
            .Take(PlayersShown)
            .ToList();

        var dynasties = titles
            .GroupBy(c => c.TeamName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var latest = g.MaxBy(c => c.SeasonStart)!;
                return new DynastyDto
                {
                    TeamName = latest.TeamName,
                    LatestTeamID = latest.TeamID,
                    LatestSeasonID = latest.SeasonID,
                    ChampionshipsCount = g.Select(c => (c.SeasonID, c.Tier)).Distinct().Count(),
                    SeasonsActive = g.Select(c => c.SeasonID).Distinct().Count(),
                    Titles = g.OrderBy(c => c.SeasonStart).Select(c => $"{c.SeasonName} ({c.Tier})").Distinct().ToList(),
                };
            })
            .OrderByDescending(t => t.ChampionshipsCount)
            .ThenBy(t => t.TeamName)
            .Take(TeamsShown)
            .ToList();

        // A player traded mid-season has a row per team and one combining them: the combined one is left out
        var stats = await _context.PlayerSeasonStats.AsNoTracking()
            .Where(s => !s.IsCombined)
            .Select(s => new
            {
                s.UserID,
                s.Gamertag,
                Current = s.User.XboxUser != null ? s.User.XboxUser.Gamertag : null,
                s.SeasonID,
                s.Season.SeasonStart,
                s.Goals,
                s.Kills,
                s.Deaths,
                s.GamesPlayed,
                s.CarrierKills,
            })
            .ToListAsync(ct);

        var players = stats
            .GroupBy(s => s.UserID)
            .Select(g =>
            {
                var kills = g.Sum(s => s.Kills);
                var deaths = g.Sum(s => s.Deaths);
                return new PlayerTotals(
                    g.Key,
                    g.Select(s => s.Current).FirstOrDefault(n => n is not null) ?? g.MaxBy(s => s.SeasonStart)!.Gamertag,
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
