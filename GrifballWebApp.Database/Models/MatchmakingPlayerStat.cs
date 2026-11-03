#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>A player's totals for a matchmaking season, as its stats sheet gives them: one row per (MatchmakingSeasonID,
/// UserID). A player with games of the season in the app (<see cref="MatchedMatch"/>) has their totals from those instead.</summary>
public class MatchmakingPlayerStat : PlayerStatTotals
{
    public int MatchmakingPlayerStatID { get; set; }
    public int MatchmakingSeasonID { get; set; }

    public virtual MatchmakingSeason MatchmakingSeason { get; set; }
}
