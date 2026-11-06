namespace GrifballWebApp.Database.Models;

/// <summary>
/// How a Halo Infinite match's custom game mode packs its stats into each player's Score and PersonalScore (#217), the
/// layouts the league's stats sheets decode. Each field is read right-aligned: (value / place) % 10^digits.
/// </summary>
public enum CustomScoreLayout
{
    /// <summary>Score is the player's goals (at most 20); PersonalScore is Halo's points.</summary>
    Plain = 1,
    /// <summary>PersonalScore (= Score): pass-ins ×1e8 (1 digit), goals ×1e7 (1), plate stops ×1e5 (2), sword kills ×1e3
    /// (2), ball held time ×1 (3, in units of 2 s).</summary>
    A = 2,
    /// <summary>PersonalScore: goals ×1e8, carrier kills ×1e6 (2), ball punches ×1e4 (2), ball held time ×1 (4, units of 2 s).
    /// Score differs from it in the last digits only.</summary>
    B = 3,
    /// <summary>PersonalScore: ball held time ×1e6 (half-seconds), ball punches ×1e4 (2), goals ×1e2 (2), and a small signed
    /// adjustment in the last 2 digits; Score: carrier kills ×1e6, sword kills ×1e4 (2), goal assists ×1e2 (2).</summary>
    C = 4,
    /// <summary>More than one layout fits the team scores (mostly 0-0 games) and the variant doesn't settle it: only the
    /// goals are kept, when every layout that fits gives each player the same.</summary>
    Ambiguous = 5,
    /// <summary>No layout fits: nothing is decoded, no goals are counted.</summary>
    Undecoded = 6,
}
