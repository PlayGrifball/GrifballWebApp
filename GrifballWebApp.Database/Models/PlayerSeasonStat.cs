#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>A player's totals for one stint of a season (regular season or playoffs). Unique per (SeasonID, UserID,
/// StintIndex, IsPlayoffs): a player traded mid-season has a row per team, StintIndex 1, 2, ...</summary>
public class PlayerSeasonStat : PlayerStatTotals
{
    public int PlayerSeasonStatID { get; set; }
    public int SeasonID { get; set; }

    /// <summary>The team the player was on for the stint; null when the sheet names none.</summary>
    public int? TeamID { get; set; }
    public string TradeNote { get; set; }
    public string RoleNote { get; set; }
    public int StintIndex { get; set; } = 1;

    public bool IsPlayoffs { get; set; }
    public bool IsCombined { get; set; }

    public virtual Season Season { get; set; }
    public virtual Team Team { get; set; }
}
