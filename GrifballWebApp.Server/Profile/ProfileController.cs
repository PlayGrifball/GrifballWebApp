using GrifballWebApp.Database;
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

    public ProfileController(GrifballContext context, ISetGamertagService setGamertagService)
    {
        _context = context;
        _setGamertagService = setGamertagService;
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
            .FirstOrDefaultAsync(u => u.Id == userID, ct);

        if (user is null)
            return NotFound();

        var aliases = await _context.PlayerAliases
            .AsNoTracking()
            .Where(a => a.UserID == userID)
            .Select(a => a.Alias)
            .Distinct()
            .ToListAsync(ct);

        var championships = await _context.PlayerChampionships
            .AsNoTracking()
            .Where(c => c.UserID == userID)
            .Include(c => c.Season)
            .Include(c => c.League)
            .Include(c => c.GameVersion)
            .OrderByDescending(c => c.Season.SeasonStart)
            .ThenBy(c => c.Tier)
            .Select(c => new PlayerChampionshipDto
            {
                PlayerChampionshipID = c.PlayerChampionshipID,
                SeasonID = c.SeasonID,
                SeasonName = c.Season.SeasonName,
                LeagueID = c.LeagueID,
                LeagueName = c.League.LeagueName,
                LeagueSlug = c.League.Slug,
                GameVersionID = c.GameVersionID,
                GameVersionName = c.GameVersion != null ? c.GameVersion.GameVersionName : null,
                GameVersionSlug = c.GameVersion != null ? c.GameVersion.Slug : null,
                Tier = c.Tier,
                TeamName = c.TeamName,
                IsCaptain = c.IsCaptain,
                GamertagUsed = c.GamertagUsed,
                Year = c.Season.SeasonStart.Year
            })
            .ToListAsync(ct);

        var rawStats = await _context.PlayerSeasonStats
            .AsNoTracking()
            .Where(s => s.UserID == userID)
            .Include(s => s.Season)
                .ThenInclude(sn => sn.League)
            .Include(s => s.GameVersion)
            .OrderByDescending(s => s.Season.SeasonStart)
            .ThenBy(s => s.Season.SeasonName)
            .ThenBy(s => s.IsPlayoffs)
            .ThenBy(s => s.StintIndex)
            .ToListAsync(ct);

        var seasonDtos = rawStats.Select(s => new PlayerSeasonStatDetailDto
        {
            PlayerSeasonStatID = s.PlayerSeasonStatID,
            SeasonID = s.SeasonID,
            SeasonName = s.Season.SeasonName,
            LeagueID = s.Season.LeagueID,
            LeagueName = s.Season.League != null ? s.Season.League.LeagueName : "AGLA",
            LeagueSlug = s.Season.League != null ? s.Season.League.Slug : "agla",
            GameVersionID = s.GameVersionID,
            GameVersionName = s.GameVersion != null ? s.GameVersion.GameVersionName : "Unknown",
            GameVersionSlug = s.GameVersion != null ? s.GameVersion.Slug : "unknown",
            Division = s.Division,
            TeamName = s.TeamName,
            TradeNote = s.TradeNote,
            RoleNote = s.RoleNote,
            StintIndex = s.StintIndex,
            IsPlayoffs = s.IsPlayoffs,
            IsCombined = s.IsCombined,
            Source = s.Source.ToString(),
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
            Year = s.Season.SeasonStart.Year
        }).ToList();

        var careerByGame = rawStats
            .GroupBy(s => s.GameVersionID)
            .Select(g =>
            {
                var gv = g.First().GameVersion;
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
                    GameVersionName = gv != null ? gv.GameVersionName : "Unknown",
                    GameVersionSlug = gv != null ? gv.Slug : "unknown",
                    SeasonsCount = distinctSeasons,
                    GamesPlayed = gp,
                    Wins = w,
                    Losses = l,
                    WinPercent = gp > 0 ? Math.Round((decimal)w / gp * 100m, 1) : null,
                    Goals = goals,
                    GoalsPerGame = gp > 0 ? Math.Round((decimal)goals / gp, 2) : null,
                    Kills = k,
                    Deaths = d,
                    Assists = a,
                    KdRatio = d > 0 ? Math.Round((decimal)k / d, 2) : k,
                    KdSpread = k - d,
                    CarrierKills = stops,
                    StopsPerGame = gp > 0 ? Math.Round((decimal)stops / gp, 2) : null,
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

        GameCareerSummaryDto? overall = null;
        if (rawStats.Count > 0)
        {
            var gp = rawStats.Sum(s => s.GamesPlayed);
            var w = rawStats.Sum(s => s.Wins);
            var l = rawStats.Sum(s => s.Losses);
            var goals = rawStats.Sum(s => s.Goals);
            var k = rawStats.Sum(s => s.Kills);
            var d = rawStats.Sum(s => s.Deaths);
            var a = rawStats.Sum(s => s.Assists);
            var stops = rawStats.Sum(s => s.CarrierKills);

            overall = new GameCareerSummaryDto
            {
                GameVersionID = null,
                GameVersionName = "All Games",
                GameVersionSlug = "all",
                SeasonsCount = rawStats.Select(s => s.SeasonID).Distinct().Count(),
                GamesPlayed = gp,
                Wins = w,
                Losses = l,
                WinPercent = gp > 0 ? Math.Round((decimal)w / gp * 100m, 1) : null,
                Goals = goals,
                GoalsPerGame = gp > 0 ? Math.Round((decimal)goals / gp, 2) : null,
                Kills = k,
                Deaths = d,
                Assists = a,
                KdRatio = d > 0 ? Math.Round((decimal)k / d, 2) : k,
                KdSpread = k - d,
                CarrierKills = stops,
                StopsPerGame = gp > 0 ? Math.Round((decimal)stops / gp, 2) : null,
                Sprees = rawStats.Sum(s => s.Sprees),
                MultiKills = rawStats.Sum(s => s.MultiKills),
                Exterminations = rawStats.Sum(s => s.Exterminations),
                Betrayals = rawStats.Sum(s => s.Betrayals),
                HammerKills = rawStats.Sum(s => s.HammerKills),
                SwordKills = rawStats.Sum(s => s.SwordKills),
                BallKills = rawStats.Sum(s => s.BallKills),
                Punches = rawStats.Sum(s => s.Punches),
                DamageDealt = rawStats.Any(s => s.DamageDealt.HasValue) ? rawStats.Sum(s => (long)(s.DamageDealt ?? 0)) : null,
                DamageTaken = rawStats.Any(s => s.DamageTaken.HasValue) ? rawStats.Sum(s => (long)(s.DamageTaken ?? 0)) : null,
                Score = rawStats.Any(s => s.Score.HasValue) ? rawStats.Sum(s => s.Score ?? 0) : null
            };
        }

        return new ProfileStatsDto
        {
            UserID = user.Id,
            Gamertag = user.XboxUser?.Gamertag,
            DisplayName = user.DisplayName,
            IsDummyUser = user.IsDummyUser,
            Aliases = aliases,
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
}
