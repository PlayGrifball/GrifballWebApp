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

    [HttpGet(Name = "GetHistoricalCatalog")]
    public async Task<ActionResult<List<HistoricalCatalogItemDto>>> Catalog(CancellationToken ct)
    {
        var seasons = await _context.Seasons
            .AsNoTracking()
            .Where(s => s.IsHistorical)
            .Include(s => s.League)
            .Include(s => s.GameVersion)
            .ToListAsync(ct);

        var seasonIds = seasons.Select(s => s.SeasonID).ToList();

        var championships = await _context.PlayerChampionships
            .AsNoTracking()
            .Where(c => seasonIds.Contains(c.SeasonID))
            .Include(c => c.User)
                .ThenInclude(u => u.XboxUser)
            .ToListAsync(ct);

        var champBySeason = championships
            .GroupBy(c => c.SeasonID)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(c => new { c.Tier, Team = c.TeamName ?? "Champions" })
                      .Select(tg => new HistoricalSeasonChampionDto
                      {
                          Tier = tg.Key.Tier,
                          TeamName = tg.Key.Team,
                          TeamSlug = HistoricalKeys.Slug(tg.Key.Team),
                          Roster = tg.Select(c => new HistoricalChampionRosterDto
                          {
                              UserID = c.UserID,
                              Gamertag = c.GamertagUsed ?? c.User?.XboxUser?.Gamertag ?? c.User?.DisplayName ?? "Unknown",
                              IsCaptain = c.IsCaptain
                          }).ToList()
                      }).ToList()
            );

        var stats = await _context.PlayerSeasonStats
            .AsNoTracking()
            .Where(s => seasonIds.Contains(s.SeasonID) && !s.IsCombined)
            .Select(s => new { s.SeasonID, s.TeamName, s.UserID })
            .ToListAsync(ct);

        var statsBySeason = stats
            .GroupBy(s => s.SeasonID)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    TeamCount = g.Where(x => !string.IsNullOrWhiteSpace(x.TeamName))
                                 .Select(x => HistoricalKeys.Slug(x.TeamName!))
                                 .Distinct()
                                 .Count(),
                    PlayerCount = g.Select(x => x.UserID).Distinct().Count()
                }
            );

        var catalog = seasons.Select(s =>
        {
            var year = s.SeasonStart.Year > 1990 ? s.SeasonStart.Year : HistoricalKeys.Year(s.SeasonName);
            champBySeason.TryGetValue(s.SeasonID, out var champs);
            statsBySeason.TryGetValue(s.SeasonID, out var st);

            return new HistoricalCatalogItemDto
            {
                SeasonID = s.SeasonID,
                SeasonName = s.SeasonName,
                Year = year,
                LeagueID = s.LeagueID,
                LeagueName = s.League?.LeagueName ?? "AGLA",
                LeagueSlug = s.League?.Slug ?? "agla",
                GameVersionID = s.GameVersionID,
                GameVersionName = s.GameVersion?.GameVersionName ?? "Halo",
                GameVersionSlug = s.GameVersion?.Slug ?? "halo3",
                Division = s.Division,
                HistoricalSourceUrl = s.HistoricalSourceUrl,
                TeamCount = st?.TeamCount ?? 0,
                PlayerCount = st?.PlayerCount ?? 0,
                Champions = champs ?? []
            };
        })
        .OrderByDescending(s => s.Year)
        .ThenByDescending(s => s.SeasonID)
        .ToList();

        return catalog;
    }

    [HttpGet(Name = "GetHistoricalLeaderboards")]
    public async Task<ActionResult<HistoricalLeaderboardsDto>> Leaderboards(CancellationToken ct)
    {
        var championships = await _context.PlayerChampionships
            .AsNoTracking()
            .Include(c => c.Season)
            .Include(c => c.User)
                .ThenInclude(u => u.XboxUser)
            .ToListAsync(ct);

        var ringLeaders = championships
            .GroupBy(c => c.UserID)
            .Select(g =>
            {
                var gamertag = g.OrderByDescending(c => c.PlayerChampionshipID)
                    .Select(c => c.GamertagUsed ?? c.User?.XboxUser?.Gamertag ?? c.User?.DisplayName)
                    .FirstOrDefault(gt => !string.IsNullOrWhiteSpace(gt)) ?? "Spartan";

                return new HistoricalPlayerRingLeaderDto
                {
                    UserID = g.Key,
                    Gamertag = gamertag,
                    RingsCount = g.Count(),
                    Titles = g.Select(c => $"{c.Season.SeasonName} ({c.Tier})").Distinct().ToList()
                };
            })
            .OrderByDescending(r => r.RingsCount)
            .Take(25)
            .ToList();

        var franchiseDynasties = championships
            .Where(c => !string.IsNullOrWhiteSpace(c.TeamName))
            .GroupBy(c => HistoricalKeys.Slug(c.TeamName!))
            .Select(g =>
            {
                var canonicalName = g.OrderByDescending(c => c.TeamName.Length).First().TeamName;
                var titlesCount = g.Select(c => new { c.SeasonID, c.Tier }).Distinct().Count();
                return new HistoricalTeamRingLeaderDto
                {
                    TeamName = canonicalName,
                    TeamSlug = g.Key,
                    ChampionshipsCount = titlesCount,
                    SeasonsActive = g.Select(c => c.SeasonID).Distinct().Count(),
                    Titles = g.Select(c => $"{c.Season.SeasonName} ({c.Tier})").Distinct().ToList()
                };
            })
            .OrderByDescending(t => t.ChampionshipsCount)
            .Take(20)
            .ToList();

        var stats = await _context.PlayerSeasonStats
            .AsNoTracking()
            .Where(s => !s.IsCombined)
            .Select(s => new
            {
                s.UserID,
                s.Gamertag,
                s.SeasonID,
                s.Goals,
                s.Kills,
                s.Deaths,
                s.GamesPlayed,
                s.CarrierKills
            })
            .ToListAsync(ct);

        var playerAggregates = stats
            .GroupBy(s => s.UserID)
            .Select(g =>
            {
                var gamertag = g.OrderByDescending(s => s.SeasonID).First().Gamertag;
                var gp = g.Sum(s => s.GamesPlayed);
                var goals = g.Sum(s => s.Goals);
                var kills = g.Sum(s => s.Kills);
                var deaths = g.Sum(s => s.Deaths);
                var stops = g.Sum(s => s.CarrierKills);
                var seasonsCount = g.Select(s => s.SeasonID).Distinct().Count();
                var kd = deaths > 0 ? Math.Round((decimal)kills / deaths, 2) : kills;

                return new
                {
                    UserID = g.Key,
                    Gamertag = gamertag,
                    GamesPlayed = gp,
                    Goals = goals,
                    Kills = kills,
                    Deaths = deaths,
                    CarrierKills = stops,
                    SeasonsCount = seasonsCount,
                    KdRatio = kd
                };
            })
            .ToList();

        var goalLeaders = playerAggregates
            .Where(p => p.Goals > 0)
            .OrderByDescending(p => p.Goals)
            .Take(25)
            .Select(p => new HistoricalPlayerMetricLeaderDto
            {
                UserID = p.UserID,
                Gamertag = p.Gamertag,
                StatValue = p.Goals,
                GamesPlayed = p.GamesPlayed,
                PerGame = p.GamesPlayed > 0 ? Math.Round((decimal)p.Goals / p.GamesPlayed, 2) : 0,
                SeasonsCount = p.SeasonsCount,
                SecondaryStat = $"{p.GamesPlayed} GP"
            })
            .ToList();

        var killLeaders = playerAggregates
            .Where(p => p.Kills > 0)
            .OrderByDescending(p => p.Kills)
            .Take(25)
            .Select(p => new HistoricalPlayerMetricLeaderDto
            {
                UserID = p.UserID,
                Gamertag = p.Gamertag,
                StatValue = p.Kills,
                GamesPlayed = p.GamesPlayed,
                PerGame = p.GamesPlayed > 0 ? Math.Round((decimal)p.Kills / p.GamesPlayed, 2) : 0,
                SeasonsCount = p.SeasonsCount,
                SecondaryStat = $"{p.KdRatio} K/D"
            })
            .ToList();

        var gamesPlayedLeaders = playerAggregates
            .Where(p => p.GamesPlayed > 0)
            .OrderByDescending(p => p.GamesPlayed)
            .Take(25)
            .Select(p => new HistoricalPlayerMetricLeaderDto
            {
                UserID = p.UserID,
                Gamertag = p.Gamertag,
                StatValue = p.GamesPlayed,
                GamesPlayed = p.GamesPlayed,
                PerGame = null,
                SeasonsCount = p.SeasonsCount,
                SecondaryStat = $"{p.SeasonsCount} Seasons"
            })
            .ToList();

        var carrierStopLeaders = playerAggregates
            .Where(p => p.CarrierKills > 0)
            .OrderByDescending(p => p.CarrierKills)
            .Take(25)
            .Select(p => new HistoricalPlayerMetricLeaderDto
            {
                UserID = p.UserID,
                Gamertag = p.Gamertag,
                StatValue = p.CarrierKills,
                GamesPlayed = p.GamesPlayed,
                PerGame = p.GamesPlayed > 0 ? Math.Round((decimal)p.CarrierKills / p.GamesPlayed, 2) : 0,
                SeasonsCount = p.SeasonsCount,
                SecondaryStat = $"{p.GamesPlayed} GP"
            })
            .ToList();

        return new HistoricalLeaderboardsDto
        {
            RingLeaders = ringLeaders,
            FranchiseDynasties = franchiseDynasties,
            GoalLeaders = goalLeaders,
            KillLeaders = killLeaders,
            GamesPlayedLeaders = gamesPlayedLeaders,
            CarrierStopLeaders = carrierStopLeaders
        };
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
