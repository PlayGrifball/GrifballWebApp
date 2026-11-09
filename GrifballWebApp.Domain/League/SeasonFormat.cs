using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Shared;
namespace GrifballWebApp.Domain.League;

/// <summary>What kind of competition a season is: which sections its page has. Late League is not a season at all but
/// matchmaking (<see cref="MatchmakingSeason"/>).</summary>
public enum SeasonFormat
{
    /// <summary>A schedule, standings and a playoff bracket (every season before formats existed).</summary>
    LeagueSeason = 1,
    /// <summary>A bracket only: no schedule or standings (e.g. Respawn).</summary>
    Tournament = 2,
}
