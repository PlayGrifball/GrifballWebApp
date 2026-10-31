using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Historical;

[Route("[controller]/[action]")]
[ApiController]
public class HistoricalController : ControllerBase
{
    private readonly GrifballContext _context;

    public HistoricalController(GrifballContext context)
    {
        _context = context;
    }

    [HttpGet("{seasonID:int}", Name = "GetHistoricalSeason")]
    public async Task<ActionResult<HistoricalSeasonDto>> Season([FromRoute] int seasonID, CancellationToken ct)
    {
        var season = await _context.Seasons
            .AsNoTracking()
            .Include(s => s.League)
            .Include(s => s.GameVersion)
            .FirstOrDefaultAsync(s => s.SeasonID == seasonID, ct);

        if (season is null)
            return NotFound();

        // Championships for this season
        var championships = await _context.PlayerChampionships
            .AsNoTracking()
            .Where(c => c.SeasonID == seasonID)
            .Include(c => c.User)
                .ThenInclude(u => u.XboxUser)
            .ToListAsync(ct);

        var champGroups = championships
            .GroupBy(c => new { c.Tier, Team = c.TeamName ?? "Champions" })
            .Select(g => new HistoricalSeasonChampionDto
            {
                Tier = g.Key.Tier,
                TeamName = g.Key.Team,
                TeamSlug = HistoricalKeys.Slug(g.Key.Team),
                Roster = g.Select(c => new HistoricalChampionRosterDto
                {
                    UserID = c.UserID,
                    Gamertag = c.GamertagUsed ?? c.User?.XboxUser?.Gamertag ?? c.User?.DisplayName ?? "Unknown",
                    IsCaptain = c.IsCaptain
                }).ToList()
            })
            .ToList();

        var winningTeamSlugs = champGroups.Select(c => c.TeamSlug).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Player season stats for this season
        var stats = await _context.PlayerSeasonStats
            .AsNoTracking()
            .Where(s => s.SeasonID == seasonID)
            .OrderByDescending(s => s.Goals)
            .ThenByDescending(s => s.Kills)
            .ToListAsync(ct);

        var playerStats = stats.Select(s => new HistoricalSeasonPlayerStatDto
        {
            PlayerSeasonStatID = s.PlayerSeasonStatID,
            UserID = s.UserID,
            Gamertag = s.Gamertag,
            TeamName = s.TeamName,
            TeamSlug = string.IsNullOrWhiteSpace(s.TeamName) ? null : HistoricalKeys.Slug(s.TeamName),
            Division = s.Division,
            TradeNote = s.TradeNote,
            RoleNote = s.RoleNote,
            StintIndex = s.StintIndex,
            IsPlayoffs = s.IsPlayoffs,
            IsCombined = s.IsCombined,
            GamesPlayed = s.GamesPlayed,
            Wins = s.Wins,
            Losses = s.Losses,
            WinPercent = s.WinPercent ?? (s.GamesPlayed > 0 ? Math.Round((decimal)s.Wins / s.GamesPlayed * 100m, 1) : null),
            Goals = s.Goals,
            Kills = s.Kills,
            Deaths = s.Deaths,
            Assists = s.Assists,
            KdRatio = s.KdRatio ?? (s.Deaths > 0 ? Math.Round((decimal)s.Kills / s.Deaths, 2) : s.Kills),
            KdSpread = s.KdSpread ?? (s.Kills - s.Deaths),
            CarrierKills = s.CarrierKills,
            Sprees = s.Sprees,
            MultiKills = s.MultiKills,
            Exterminations = s.Exterminations
        }).ToList();

        // Aggregate teams in this season (using non-combined stint stats to avoid double counting)
        var teamGroups = stats
            .Where(s => !string.IsNullOrWhiteSpace(s.TeamName) && !s.IsCombined)
            .GroupBy(s => s.TeamName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var teamName = g.Key;
                var teamSlug = HistoricalKeys.Slug(teamName);

                var maxGp = g.Max(p => p.GamesPlayed);
                var maxWins = g.Max(p => p.Wins);
                var maxLosses = g.Max(p => p.Losses);
                var totalGoals = g.Sum(p => p.Goals);

                return new HistoricalSeasonTeamDto
                {
                    TeamName = teamName,
                    TeamSlug = teamSlug,
                    GamesPlayed = maxGp,
                    Wins = maxWins,
                    Losses = maxLosses,
                    WinPercent = maxGp > 0 ? Math.Round((decimal)maxWins / maxGp * 100m, 1) : null,
                    Goals = totalGoals,
                    WonChampionship = winningTeamSlugs.Contains(teamSlug),
                    Roster = g.Select(p => new HistoricalTeamPlayerDto
                    {
                        UserID = p.UserID,
                        Gamertag = p.Gamertag,
                        TradeNote = p.TradeNote,
                        RoleNote = p.RoleNote,
                        StintIndex = p.StintIndex,
                        GamesPlayed = p.GamesPlayed,
                        Goals = p.Goals,
                        Kills = p.Kills,
                        Deaths = p.Deaths,
                        KdRatio = p.KdRatio ?? (p.Deaths > 0 ? Math.Round((decimal)p.Kills / p.Deaths, 2) : p.Kills),
                        CarrierKills = p.CarrierKills
                    }).ToList()
                };
            })
            .OrderByDescending(t => t.WonChampionship)
            .ThenByDescending(t => t.Wins)
            .ThenByDescending(t => t.Goals)
            .ToList();

        return new HistoricalSeasonDto
        {
            SeasonID = season.SeasonID,
            SeasonName = season.SeasonName,
            Year = season.SeasonStart.Year > 1990 ? season.SeasonStart.Year : HistoricalKeys.Year(season.SeasonName),
            LeagueID = season.LeagueID,
            LeagueName = season.League?.LeagueName ?? "AGLA",
            LeagueSlug = season.League?.Slug ?? "agla",
            GameVersionID = season.GameVersionID,
            GameVersionName = season.GameVersion?.GameVersionName ?? "Halo",
            GameVersionSlug = season.GameVersion?.Slug ?? "halo3",
            Division = season.Division,
            HistoricalSourceUrl = season.HistoricalSourceUrl,
            Champions = champGroups,
            Teams = teamGroups,
            PlayerStats = playerStats
        };
    }

    [HttpGet("{teamSlug}", Name = "GetHistoricalTeam")]
    public async Task<ActionResult<HistoricalTeamDetailDto>> Team([FromRoute] string teamSlug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(teamSlug))
            return BadRequest();

        // Match all variations of team names that slugify to teamSlug
        var statTeamNames = await _context.PlayerSeasonStats
            .AsNoTracking()
            .Where(s => s.TeamName != null && s.TeamName != "")
            .Select(s => s.TeamName)
            .Distinct()
            .ToListAsync(ct);

        var champTeamNames = await _context.PlayerChampionships
            .AsNoTracking()
            .Where(c => c.TeamName != null && c.TeamName != "")
            .Select(c => c.TeamName)
            .Distinct()
            .ToListAsync(ct);

        var matchedNames = statTeamNames.Concat(champTeamNames)
            .Distinct()
            .Where(t => HistoricalKeys.Slug(t) == teamSlug)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (matchedNames.Count == 0)
            return NotFound();

        // Best display name (prefer most common or capitalized)
        var canonicalName = matchedNames.OrderByDescending(n => n.Length).First();

        // Team championships
        var titles = await _context.PlayerChampionships
            .AsNoTracking()
            .Where(c => matchedNames.Contains(c.TeamName))
            .Include(c => c.Season)
            .Include(c => c.League)
            .Include(c => c.GameVersion)
            .Include(c => c.User)
                .ThenInclude(u => u.XboxUser)
            .ToListAsync(ct);

        var champDtos = titles
            .GroupBy(t => new { t.SeasonID, t.Season.SeasonName, t.League.LeagueName, Game = t.GameVersion != null ? t.GameVersion.GameVersionName : null, t.Tier })
            .Select(g => new HistoricalTeamChampionshipDto
            {
                SeasonID = g.Key.SeasonID,
                SeasonName = g.Key.SeasonName,
                Year = g.First().Season.SeasonStart.Year > 1990 ? g.First().Season.SeasonStart.Year : HistoricalKeys.Year(g.Key.SeasonName),
                LeagueName = g.Key.LeagueName,
                GameVersionName = g.Key.Game,
                Tier = g.Key.Tier,
                Roster = g.Select(c => new HistoricalChampionRosterDto
                {
                    UserID = c.UserID,
                    Gamertag = c.GamertagUsed ?? c.User?.XboxUser?.Gamertag ?? c.User?.DisplayName ?? "Unknown",
                    IsCaptain = c.IsCaptain
                }).ToList()
            })
            .OrderByDescending(c => c.Year)
            .ToList();

        var wonSeasonIds = titles.Select(c => c.SeasonID).ToHashSet();

        // Team appearances by season
        var stats = await _context.PlayerSeasonStats
            .AsNoTracking()
            .Where(s => matchedNames.Contains(s.TeamName) && !s.IsCombined)
            .Include(s => s.Season)
                .ThenInclude(sn => sn.League)
            .Include(s => s.GameVersion)
            .ToListAsync(ct);

        var seasonsAppearances = stats
            .GroupBy(s => s.SeasonID)
            .Select(g =>
            {
                var season = g.First().Season;
                var gv = g.First().GameVersion;
                var maxGp = g.Max(p => p.GamesPlayed);
                var maxWins = g.Max(p => p.Wins);
                var maxLosses = g.Max(p => p.Losses);
                var totalGoals = g.Sum(p => p.Goals);

                return new HistoricalTeamSeasonAppearanceDto
                {
                    SeasonID = g.Key,
                    SeasonName = season.SeasonName,
                    Year = season.SeasonStart.Year > 1990 ? season.SeasonStart.Year : HistoricalKeys.Year(season.SeasonName),
                    LeagueName = season.League?.LeagueName ?? "AGLA",
                    GameVersionName = gv?.GameVersionName ?? "Halo",
                    GamesPlayed = maxGp,
                    Wins = maxWins,
                    Losses = maxLosses,
                    Goals = totalGoals,
                    WonChampionship = wonSeasonIds.Contains(g.Key),
                    Roster = g.Select(p => new HistoricalTeamPlayerDto
                    {
                        UserID = p.UserID,
                        Gamertag = p.Gamertag,
                        TradeNote = p.TradeNote,
                        RoleNote = p.RoleNote,
                        StintIndex = p.StintIndex,
                        GamesPlayed = p.GamesPlayed,
                        Goals = p.Goals,
                        Kills = p.Kills,
                        Deaths = p.Deaths,
                        KdRatio = p.KdRatio ?? (p.Deaths > 0 ? Math.Round((decimal)p.Kills / p.Deaths, 2) : p.Kills),
                        CarrierKills = p.CarrierKills
                    }).ToList()
                };
            })
            .OrderByDescending(s => s.Year)
            .ToList();

        // All-time players for this team franchise
        var allTimePlayers = stats
            .GroupBy(s => new { s.UserID, s.Gamertag })
            .Select(g =>
            {
                var seasonsCount = g.Select(p => p.SeasonID).Distinct().Count();
                var ringsCount = titles.Count(t => t.UserID == g.Key.UserID);
                return new HistoricalTeamAllTimePlayerDto
                {
                    UserID = g.Key.UserID,
                    Gamertag = g.Key.Gamertag,
                    SeasonsPlayed = seasonsCount,
                    GamesPlayed = g.Sum(p => p.GamesPlayed),
                    Goals = g.Sum(p => p.Goals),
                    ChampionshipsWon = ringsCount
                };
            })
            .OrderByDescending(p => p.ChampionshipsWon)
            .ThenByDescending(p => p.Goals)
            .ThenByDescending(p => p.GamesPlayed)
            .ToList();

        return new HistoricalTeamDetailDto
        {
            TeamName = canonicalName,
            TeamSlug = teamSlug,
            TotalChampionships = champDtos.Count,
            Championships = champDtos,
            Seasons = seasonsAppearances,
            AllTimePlayers = allTimePlayers
        };
    }
}
