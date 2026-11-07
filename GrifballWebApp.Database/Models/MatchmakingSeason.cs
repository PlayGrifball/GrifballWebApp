namespace GrifballWebApp.Database.Models;

/// <summary>
/// A run of matchmaking (Late League): pick-up games of random, skill-matched sides, so no teams, standings, playoffs or
/// champion, only players' games and totals. The archive's Late League seasons are one each per game ("Late League Season 8
/// (Halo 3)"); the queue's own matches are in none until it starts runs of its own.
/// </summary>
public class MatchmakingSeason : AuditableEntity
{
    public int MatchmakingSeasonID { get; set; }
    public string Name { get; set; } = null!;
    public int? GameVersionID { get; set; }
    public GameVersion? GameVersion { get; set; }
    /// <summary>The first and last day its games were played (UTC).</summary>
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    /// <summary>The archive's key for one imported from the historical archive, "{sourceWorkbook}/{sourceSheet}" as a
    /// season's (<see cref="Season.HistoricalSourceID"/>): what a re-run of the import finds it by. Unique where set.</summary>
    public string? HistoricalSourceID { get; set; }
    /// <summary>The source workbook's link, kept for provenance.</summary>
    public string? HistoricalSourceUrl { get; set; }

    /// <summary>Read-only: a match joins a season from its own side (<see cref="MatchedMatch.RecordArchived"/>).</summary>
    public IReadOnlyCollection<MatchedMatch> MatchedMatches => _matchedMatches;
    private readonly HashSet<MatchedMatch> _matchedMatches = [];
    public ICollection<MatchmakingPlayerStat> PlayerStats { get; set; } = new HashSet<MatchmakingPlayerStat>();
}
