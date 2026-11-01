using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GrifballWebApp.Server.Profile;

[Route("[controller]/[action]")]
[ApiController]
public class ProfileController : ControllerBase
{
    private readonly GrifballContext _context;
    private readonly ISetGamertagService _setGamertagService;
    private readonly GrifballWebApp.Server.Stats.SeasonStatsService _stats;

    public ProfileController(GrifballContext context, ISetGamertagService setGamertagService, GrifballWebApp.Server.Stats.SeasonStatsService stats)
    {
        _context = context;
        _setGamertagService = setGamertagService;
        _stats = stats;
    }

    [HttpGet("{userID:int}", Name = "GetGamertag")]
    public Task<ProfileDto?> GetGamertag([FromRoute] int userID, CancellationToken ct)
    {
        return _context.Users
            .Where(x => x.Id == userID)
            .Select(x => new ProfileDto()
            {
                Gamertag = x.XboxUser.Gamertag
            })
            .FirstOrDefaultAsync(ct);
    }

    [HttpGet("{userID:int}", Name = "GetProfileStats")]
    public async Task<ActionResult<ProfileStatsDto>> GetProfileStats([FromRoute] int userID, CancellationToken ct)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.XboxUser)
            .Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == userID, ct);

        if (user is null)
            return NotFound();

        // The gamertags the user's Xbox account had before (its renames), latest first, but not the one it has now (renamed
        // A to B and back to A, A is in its history too)
        var current = user.XboxUser is null ? null : Historical.HistoricalKeys.Normalize(user.XboxUser.Gamertag);
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

        var championships = await _context.PlayerChampionships
            .AsNoTracking()
            .Where(c => c.UserID == userID)
            .OrderByDescending(c => c.Season.SeasonStart)
            .ThenBy(c => c.Tier)
            .Select(c => new PlayerChampionshipDto
            {
                PlayerChampionshipID = c.PlayerChampionshipID,
                SeasonID = c.SeasonID,
                SeasonName = c.Season.SeasonName,
                IsHistorical = c.Season.IsHistorical,
                SeasonSourceUrl = c.Season.HistoricalSourceUrl,
                LeagueID = c.Season.LeagueID,
                LeagueName = c.Season.League.LeagueName,
                LeagueSlug = c.Season.League.Slug,
                GameVersionID = c.GameVersionID,
                GameVersionName = c.GameVersion != null ? c.GameVersion.GameVersionName : null,
                GameVersionSlug = c.GameVersion != null ? c.GameVersion.Slug : null,
                Tier = c.Tier,
                TeamID = c.TeamID,
                TeamName = c.Team.TeamName,
                IsCaptain = c.IsCaptain,
                GamertagUsed = c.GamertagUsed,
                Year = c.Season.SeasonStart.Year
            })
            .ToListAsync(ct);

        // Season stats: from the season's games where it has them, otherwise the season totals stored for it
        var rawStats = await _stats.GetAsync(userID: userID, ct: ct);

        var seasonDtos = rawStats.Select(s => new PlayerSeasonStatDetailDto
        {
            PlayerSeasonStatID = s.PlayerSeasonStatID,
            SeasonID = s.SeasonID,
            SeasonName = s.SeasonName,
            IsHistorical = s.IsHistorical,
            SeasonSourceUrl = s.HistoricalSourceUrl,
            LeagueID = s.LeagueID,
            LeagueName = s.LeagueName,
            LeagueSlug = s.LeagueSlug,
            GameVersionID = s.GameVersionID,
            GameVersionName = s.GameVersionName,
            GameVersionSlug = s.GameVersionSlug,
            UserID = s.UserID,
            Gamertag = s.Gamertag,
            Division = s.Division,
            TeamID = s.TeamID,
            TeamName = s.TeamName,
            TradeNote = s.TradeNote,
            RoleNote = s.RoleNote,
            StintIndex = s.StintIndex,
            IsPlayoffs = s.IsPlayoffs,
            IsCombined = s.IsCombined,
            Source = s.FromMatches ? "Matches" : "Recorded",
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
            Exterminations = s.Exterminations,
            DamageDealt = s.DamageDealt,
            DamageTaken = s.DamageTaken,
            Score = s.Score,
            Year = s.SeasonStart.Year
        }).ToList();

        var counted = rawStats;
        var careerByGame = counted
            .GroupBy(s => s.GameVersionID)
            .Select(g =>
            {
                var gv = g.First();
                var distinctSeasons = g.Select(s => s.SeasonID).Distinct().Count();
                var gp = g.Sum(s => s.GamesPlayed);
                var w = g.Sum(s => s.Wins);
                var l = g.Sum(s => s.Losses);
                var goals = g.Sum(s => s.Goals);
                var k = g.Sum(s => s.Kills);
                var d = g.Sum(s => s.Deaths);
                var a = g.Sum(s => s.Assists);
                var stops = g.Sum(s => s.CarrierKills);

                return new GameCareerSummaryDto
                {
                    GameVersionID = g.Key,
                    GameVersionName = gv.GameVersionName,
                    GameVersionSlug = gv.GameVersionSlug,
                    SeasonsCount = distinctSeasons,
                    GamesPlayed = gp,
                    Wins = w,
                    Losses = l,
                    WinPercent = gp > 0 ? Math.Round((decimal)w / gp * 100m, 1) : null,
                    Goals = goals,
                    GoalsPerGame = PerGame(g, s => s.Goals),
                    Kills = k,
                    Deaths = d,
                    Assists = a,
                    KdRatio = d > 0 ? Math.Round((decimal)k / d, 2) : k,
                    KdSpread = k - d,
                    CarrierKills = stops,
                    StopsPerGame = PerGame(g, s => s.CarrierKills, carrierStops: true),
                    Sprees = g.Sum(s => s.Sprees),
                    MultiKills = g.Sum(s => s.MultiKills),
                    Exterminations = g.Sum(s => s.Exterminations),
                    Betrayals = g.Sum(s => s.Betrayals),
                    HammerKills = g.Sum(s => s.HammerKills),
                    SwordKills = g.Sum(s => s.SwordKills),
                    BallKills = g.Sum(s => s.BallKills),
                    Punches = g.Sum(s => s.Punches),
                    DamageDealt = g.Any(s => s.DamageDealt.HasValue) ? g.Sum(s => (long)(s.DamageDealt ?? 0)) : null,
                    DamageTaken = g.Any(s => s.DamageTaken.HasValue) ? g.Sum(s => (long)(s.DamageTaken ?? 0)) : null,
                    Score = g.Any(s => s.Score.HasValue) ? g.Sum(s => s.Score ?? 0) : null
                };
            })
            .OrderBy(c => c.GameVersionID)
            .ToList();

        // Across games only what every game counts alike: games, wins, kills, deaths and assists. Goals, carrier stops and
        // medals are counted differently in each game, so they are only totalled per game (above).
        GameCareerSummaryDto? overall = null;
        if (counted.Count > 0)
        {
            var gp = counted.Sum(s => s.GamesPlayed);
            var w = counted.Sum(s => s.Wins);
            var k = counted.Sum(s => s.Kills);
            var d = counted.Sum(s => s.Deaths);

            overall = new GameCareerSummaryDto
            {
                GameVersionID = null,
                GameVersionName = "All Games",
                GameVersionSlug = "all",
                SeasonsCount = counted.Select(s => s.SeasonID).Distinct().Count(),
                GamesPlayed = gp,
                Wins = w,
                Losses = counted.Sum(s => s.Losses),
                WinPercent = gp > 0 ? Math.Round((decimal)w / gp * 100m, 1) : null,
                Kills = k,
                Deaths = d,
                Assists = counted.Sum(s => s.Assists),
                KdRatio = d > 0 ? Math.Round((decimal)k / d, 2) : k,
                KdSpread = k - d,
            };
        }

        return new ProfileStatsDto
        {
            UserID = user.Id,
            Gamertag = user.XboxUser?.Gamertag,
            DisplayName = user.DisplayName,
            IsDummyUser = user.IsDummyUser,
            RegionID = user.RegionID,
            RegionName = user.Region?.RegionName,
            PastGamertags = pastGamertags,
            Championships = championships,
            CareerByGame = careerByGame,
            OverallCareer = overall,
            Seasons = seasonDtos
        };
    }

    [Authorize(Roles = "Player")]
    [HttpPost("{userID:int}", Name = "SetGamertag")]
    public async Task<IActionResult> SetGamertag([FromRoute] int userID, [FromQuery] string gamertag, CancellationToken ct)
    {
        var id = GetUserID();
        if (id != userID)
            return Forbid();

        var errorMessage = await _setGamertagService.SetGamertag(userID, gamertag, ct);

        if (errorMessage is not null)
            return BadRequest(errorMessage);

        return Ok();
    }

    private int? GetUserID()
    {
        var stringName = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (stringName == null)
            return null;
        var parsed = int.TryParse(stringName, out var id);
        return parsed ? id : null;
    }

    /// <summary>A stat per game over the lines that record it: goals are in both kinds, carrier stops only in the lines
    /// that record them (a game from Waypoint has none), so games without them don't lower the average.</summary>
    private static decimal? PerGame(IEnumerable<GrifballWebApp.Server.Stats.SeasonStatLine> lines, Func<GrifballWebApp.Server.Stats.SeasonStatLine, int> stat, bool carrierStops = false)
    {
        var recording = lines.Where(s => !carrierStops || s.RecordsCarrierKills).ToList();
        var games = recording.Sum(s => s.GamesPlayed);
        return games > 0 ? Math.Round((decimal)recording.Sum(stat) / games, 2) : null;
    }
}
