namespace GrifballWebApp.Application.StatFlags;

/// <summary>
/// Every fixed number <see cref="StatFlagService"/>'s rules use that isn't configurable (<see cref="StatFlagOptions"/>
/// is, since the fourth review), in one place (listed in #548's PR): conservative, so a flag means staff should look,
/// not that the game is necessarily wrong.
/// </summary>
public static class StatFlagThresholds
{
    /// <summary>How far into the future a hand-entered game's PlayedAt is still let go (third review): a clock a
    /// little fast, or a commissioner entering it moments after the game ended, shouldn't flag a game just played.</summary>
    public static readonly TimeSpan FutureMargin = TimeSpan.FromHours(1);
}
