#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>A player's totals for one stint of a season (regular season or playoffs). Unique per (SeasonID, UserID,
/// StintIndex, IsPlayoffs): a player traded mid-season has a row per team, StintIndex 1, 2, ...</summary>
public class PlayerSeasonStat : AuditableEntity
{
    public int PlayerSeasonStatID { get; set; }
    public int UserID { get; set; }
    public int SeasonID { get; set; }
    public int GameVersionID { get; set; }

    public string Gamertag { get; set; }
    /// <summary>The team the player was on for the stint; null when the sheet names none.</summary>
    public int? TeamID { get; set; }
    public string Division { get; set; }
    public string TradeNote { get; set; }
    public string RoleNote { get; set; }
    public int StintIndex { get; set; } = 1;
    public StatSource Source { get; set; } = StatSource.Manual;

    public bool IsPlayoffs { get; set; }
    public bool IsCombined { get; set; }

    // Core Metrics
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public decimal? WinPercent { get; set; }
    public int Goals { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public decimal? KdRatio { get; set; }
    public int? KdSpread { get; set; }
    public int CarrierKills { get; set; }

    // Durations & Averages
    public decimal? AverageLifeSeconds { get; set; }
    public int? TotalSecondsPlayed { get; set; }

    // Universal Medals
    public int Sprees { get; set; }
    public int MultiKills { get; set; }
    public int Exterminations { get; set; }
    public int Betrayals { get; set; }

    // Weapons
    public int? HammerKills { get; set; }
    public int? SwordKills { get; set; }
    public int? BallKills { get; set; }
    public int? Punches { get; set; }

    // Infinite
    public int? Score { get; set; }
    public int? DamageDealt { get; set; }
    public int? DamageTaken { get; set; }

    // Game-specific metrics (JSON serialized)
    public string ExtendedMetricsJson { get; set; }

    public virtual User User { get; set; }
    public virtual Season Season { get; set; }
    public virtual Team Team { get; set; }
    public virtual GameVersion GameVersion { get; set; }
}
