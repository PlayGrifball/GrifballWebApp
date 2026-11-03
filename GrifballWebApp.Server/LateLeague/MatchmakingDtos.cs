using System.Linq.Expressions;
using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Server.LateLeague;

/// <summary>A matchmaking season (a Late League season of the archive): its name, game and when its games were played.</summary>
public class MatchmakingSeasonDto
{
    public int MatchmakingSeasonID { get; set; }
    public string Name { get; set; } = "";
    public int? GameVersionID { get; set; }
    public string? GameVersionName { get; set; }
    public string? GameVersionSlug { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int GameCount { get; set; }
    public int PlayerCount { get; set; }
    /// <summary>Imported from the historical archive: <see cref="HistoricalSourceUrl"/> is its source spreadsheet.</summary>
    public bool IsHistorical { get; set; }
    public string? HistoricalSourceUrl { get; set; }

    public static readonly Expression<Func<MatchmakingSeason, MatchmakingSeasonDto>> Of = s => new MatchmakingSeasonDto
    {
        MatchmakingSeasonID = s.MatchmakingSeasonID,
        Name = s.Name,
        GameVersionID = s.GameVersionID,
        GameVersionName = s.GameVersion != null ? s.GameVersion.GameVersionName : null,
        GameVersionSlug = s.GameVersion != null ? s.GameVersion.Slug : null,
        StartsAt = s.StartsAt,
        EndsAt = s.EndsAt,
        GameCount = s.MatchedMatches.Count,
        IsHistorical = s.HistoricalSourceID != null,
        HistoricalSourceUrl = s.HistoricalSourceUrl,
    };
}

/// <summary>The Discord queue: Late League as it is played now, its reported games and each player's totals.</summary>
public class MatchmakingQueueDto
{
    public int GameCount { get; set; }
    public List<MatchmakingPlayerStatDto> Players { get; set; } = [];
}

public class MatchmakingSeasonDetailDto
{
    public MatchmakingSeasonDto Season { get; set; } = null!;
    public List<MatchmakingPlayerStatDto> Players { get; set; } = [];
}

/// <summary>A player's line of a matchmaking season, for its page and a profile.</summary>
public class MatchmakingPlayerStatDto
{
    public int? MatchmakingSeasonID { get; set; }
    public string? MatchmakingSeasonName { get; set; }
    public int UserID { get; set; }
    public string Gamertag { get; set; } = "";
    public int? GameVersionID { get; set; }
    public string? GameVersionName { get; set; }
    public string? GameVersionSlug { get; set; }
    /// <summary>Matches (totalled from its games) or Recorded (totals stored, e.g. a historical sheet's).</summary>
    public string Source { get; set; } = "";
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public decimal? WinPercent { get; set; }
    public long Goals { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public decimal? KdRatio { get; set; }
    public int? KdSpread { get; set; }
    public int CarrierKills { get; set; }

    public static MatchmakingPlayerStatDto From(MatchmakingStatLine line) => new()
    {
        MatchmakingSeasonID = line.MatchmakingSeasonID,
        MatchmakingSeasonName = line.MatchmakingSeasonName,
        UserID = line.UserID,
        Gamertag = line.Gamertag,
        GameVersionID = line.GameVersionID,
        GameVersionName = line.GameVersionName,
        GameVersionSlug = line.GameVersionSlug,
        Source = line.FromMatches ? "Matches" : "Recorded",
        GamesPlayed = line.GamesPlayed,
        Wins = line.Wins,
        Losses = line.Losses,
        WinPercent = line.WinPercent,
        Goals = line.Goals,
        Kills = line.Kills,
        Deaths = line.Deaths,
        Assists = line.Assists,
        KdRatio = line.KdRatio,
        KdSpread = line.KdSpread,
        CarrierKills = line.CarrierKills,
    };
}
