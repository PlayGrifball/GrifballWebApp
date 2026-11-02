#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>A player's stored totals over a run of games, as a stats sheet gives them: the columns a season's
/// (<see cref="PlayerSeasonStat"/>) and a matchmaking season's (<see cref="MatchmakingPlayerStat"/>) rows share.</summary>
public abstract class PlayerStatTotals : AuditableEntity
{
    public int UserID { get; set; }
    public int GameVersionID { get; set; }

    public string Gamertag { get; set; }

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
    public virtual GameVersion GameVersion { get; set; }
}
