using GrifballWebApp.Domain.Archive;

namespace GrifballWebApp.Application.StatFlags;

/// <summary>An open flag, for the staff list and the match page's marker.</summary>
public class StatFlagDto
{
    public int StatFlagID { get; set; }
    public StatFlagRule Rule { get; set; }
    public Guid? MatchID { get; set; }
    public int? RecordedGameID { get; set; }
    /// <summary>The season match the flagged game belongs to, when it belongs to one (a Halo pull not yet reported to
    /// a match has none): to link the staff list to the match page.</summary>
    public int? SeasonMatchID { get; set; }
    /// <summary>The flagged game's number in its series, when the flag is about one particular game of it; null for a
    /// flag about the series as a whole (StatFlagRule.SeriesDoesNotMatchGames).</summary>
    public int? GameNumber { get; set; }
    public int? PlayerUserID { get; set; }
    /// <summary>The name shown for <see cref="PlayerUserID"/> (UserNames.Pick); null when the rule is about the whole
    /// game, or the player has since been merged away.</summary>
    public string? PlayerName { get; set; }
    public required string Reason { get; set; }
    public DateTime FoundAt { get; set; }
    /// <summary>When the flagged game is a recorded game outside any series (an imported scrim, a Late League
    /// matchmaking game): there's no page to link it to, so staff are shown what it was instead - when it was played
    /// and its sides. Null otherwise (there's a page to link to: <see cref="SeasonMatchID"/>, or the Halo match's own
    /// page).</summary>
    public DateTime? GamePlayedAt { get; set; }
    /// <summary>The flagged game's sides, "Team A vs Team B", alongside <see cref="GamePlayedAt"/>.</summary>
    public string? GameTeams { get; set; }
}

/// <summary>Dismissing a flag that doesn't exist (already dismissed and removed is impossible: dismissing only sets a
/// column) or was never found.</summary>
public class StatFlagNotFoundException(int statFlagID) : Exception($"Stat flag {statFlagID} does not exist");
