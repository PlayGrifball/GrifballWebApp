using System.Linq.Expressions;

namespace GrifballWebApp.Domain.Models;

/// <summary>
/// A run of matchmaking (Late League): pick-up games of random, skill-matched sides, so no teams, standings, playoffs or
/// champion, only players' games and totals. The archive's Late League seasons are one each per game ("Late League Season 8
/// (Halo 3)"); the queue's own matches are in none until it starts runs of its own.
/// <para>
/// An aggregate root of its own fields only: its games (<see cref="MatchedMatch"/>) and its sheet's rows
/// (<see cref="MatchmakingPlayerStat"/>) are roots of their own that name it. It begins as the archive's
/// (<see cref="Imported"/>), which only the import changes: its name, game and link (<see cref="UpdateArchived"/>) and the
/// days its games were played, from its opening to its close (<see cref="DateArchived"/>), which never run backwards
/// (CK_Matchmaking_MatchmakingSeasons_DatesInOrder). Nothing outside it sets its fields: only its own methods, and EF (which
/// uses the private setters).
/// </para>
/// </summary>
[AggregateRoot]
public class MatchmakingSeason : AuditableEntity
{
    /// <summary>For EF. A season begins through <see cref="Imported"/>.</summary>
    private MatchmakingSeason() { }

    /// <summary>
    /// A Late League season of the historical archive, found again by <paramref name="historicalSourceID"/> (its workbook and
    /// sheet): the import dates it (<see cref="DateArchived"/>) and keeps it as the archive says (<see cref="UpdateArchived"/>).
    /// </summary>
    public static MatchmakingSeason Imported(string historicalSourceID, string name, int? gameVersionID, string? historicalSourceUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(historicalSourceID);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new()
        {
            HistoricalSourceID = historicalSourceID,
            Name = name,
            GameVersionID = gameVersionID,
            HistoricalSourceUrl = historicalSourceUrl,
        };
    }

    /// <summary>A season of any kind, dated, for tests: with a key, or none of the archive's.</summary>
    internal static MatchmakingSeason Seed(string name, DateTime startsAt = default, DateTime endsAt = default, int? gameVersionID = null,
        string? historicalSourceID = null, string? historicalSourceUrl = null, int matchmakingSeasonID = 0)
    {
        if (endsAt < startsAt)
            throw new ArgumentException("A season ends no earlier than it starts", nameof(endsAt));
        return new()
        {
            MatchmakingSeasonID = matchmakingSeasonID,
            Name = name,
            StartsAt = startsAt,
            EndsAt = endsAt,
            GameVersionID = gameVersionID,
            HistoricalSourceID = historicalSourceID,
            HistoricalSourceUrl = historicalSourceUrl,
        };
    }

    // The key is internal: the database makes it, and tests may give one
    public int MatchmakingSeasonID { get; internal set; }
    public string Name { get; private set; } = null!;
    public int? GameVersionID { get; private set; }
    public GameVersion? GameVersion { get; private set; }
    /// <summary>The first and last day its games were played (UTC), the start no later than the end (<see cref="DateArchived"/>).</summary>
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    /// <summary>The archive's key for one imported from the historical archive, "{sourceWorkbook}/{sourceSheet}" as a
    /// season's (<see cref="Season.HistoricalSourceID"/>): what a re-run of the import finds it by. Unique where set.</summary>
    public string? HistoricalSourceID { get; private set; }
    /// <summary>The source workbook's link, kept for provenance. Only an imported season has one
    /// (CK_Matchmaking_MatchmakingSeasons_SourceUrlOnlyImported).</summary>
    public string? HistoricalSourceUrl { get; private set; }

    /// <summary>Read-only: a match joins a season from its own side (<see cref="MatchedMatch.RecordArchived"/>).</summary>
    public IReadOnlyCollection<MatchedMatch> MatchedMatches => _matchedMatches;
    private readonly HashSet<MatchedMatch> _matchedMatches = [];
    /// <summary>Read-only: a stats sheet row is its own aggregate, written by the import (<see cref="MatchmakingPlayerStat.FromSheet"/>).</summary>
    public IReadOnlyCollection<MatchmakingPlayerStat> PlayerStats => _playerStats;
    private readonly HashSet<MatchmakingPlayerStat> _playerStats = [];

    /// <summary>
    /// The seasons imported from the historical archive (<see cref="HistoricalSourceID"/> set), for queries: the one rule,
    /// which <see cref="IsImported"/> runs for a loaded season. (Named apart from the <see cref="Imported"/> factory.)
    /// </summary>
    public static readonly Expression<Func<MatchmakingSeason, bool>> ImportedFromArchive = s => s.HistoricalSourceID != null;

    private static readonly Func<MatchmakingSeason, bool> IsImportedRule = ImportedFromArchive.Compile();

    /// <summary>Imported from the historical archive (<see cref="ImportedFromArchive"/>): only the import changes it.</summary>
    public bool IsImported => IsImportedRule(this);

    /// <summary>A re-run of the import: the archive's name, game and link for the season (a corrected name renames it).</summary>
    public void UpdateArchived(string name, int? gameVersionID, string? historicalSourceUrl)
    {
        RequireImported();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        GameVersionID = gameVersionID;
        HistoricalSourceUrl = historicalSourceUrl;
    }

    /// <summary>
    /// The days the season's games were played, as the import finds them: it opened at <paramref name="start"/> and closed
    /// at <paramref name="end"/>. Undated (<paramref name="dated"/> false: only the year is known, spanned whole), a re-run
    /// keeps the close an earlier run gave when it is within the year, as the row is unchanged. A season of the archive has
    /// been played, so it opened and closed by <paramref name="now"/> at the latest (a date past it is the sheet's mistake).
    /// Refused (thrown) for dates that run backwards, or for a season not the archive's.
    /// </summary>
    public void DateArchived(DateTime start, DateTime end, bool dated, DateTime now)
    {
        RequireImported();
        if (end < start)
            throw new ArgumentException($"Matchmaking season {Name}: it ends ({end:o}) before it starts ({start:o})", nameof(end));
        if (!dated && EndsAt > start && EndsAt <= end)
            end = EndsAt;
        StartsAt = start > now ? now : start;
        EndsAt = end > now ? now : end;
    }

    private void RequireImported()
    {
        if (!IsImported)
            throw new InvalidOperationException($"Matchmaking season {Name} is not the archive's: only the import changes a season, and it changes its own");
    }
}
