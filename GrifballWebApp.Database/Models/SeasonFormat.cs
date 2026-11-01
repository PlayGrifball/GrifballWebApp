namespace GrifballWebApp.Database.Models;

/// <summary>What kind of competition a season is: which sections its page has and whether it crowns a champion.</summary>
public enum SeasonFormat
{
    /// <summary>A schedule, standings and a playoff bracket (every season before formats existed).</summary>
    LeagueSeason = 1,
    /// <summary>A bracket only: no schedule or standings (e.g. Respawn).</summary>
    Tournament = 2,
    /// <summary>Late League: games and stats, no playoffs and never a champion.</summary>
    LateLeague = 3,
}
