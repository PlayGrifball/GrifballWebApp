namespace GrifballWebApp.Application.StatFlags;

/// <summary>
/// Every fixed number <see cref="StatFlagService"/>'s rules use, in one place (listed in #548's PR): conservative, so a
/// flag means staff should look, not that the game is necessarily wrong.
/// </summary>
public static class StatFlagThresholds
{
    /// <summary>The fewest players a side a hand-entered game is expected to field: Grifball's own format (4v4). The
    /// season has no narrower number to read yet (left for later): a smaller season format would need its own.</summary>
    public const int MinPlayersPerTeam = 4;

    /// <summary>Over 100% accuracy, allowing a little for Halo's own float rounding.</summary>
    public const float MaxAccuracy = 100.5f;
}
