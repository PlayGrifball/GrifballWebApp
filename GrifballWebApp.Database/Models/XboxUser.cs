namespace GrifballWebApp.Database.Models;

/// <summary>
/// An Xbox account (XUID), as Xbox gave it: the gamertag it has now, and the ones it had before
/// (<see cref="PastGamertags"/>, its own: recorded only by <see cref="Renamed"/> and <see cref="RecordPastGamertag"/>, and
/// removed by <see cref="RemovePastGamertag"/>). Its user ties to it (<see cref="Models.User.TieToXbox(XboxUser)"/>); its games'
/// players are its match's (<see cref="Match.AddParticipant"/>). A gamertag claim (<see cref="GamertagClaim"/>) is an aggregate of
/// its own that names the account; approving one records the gamertag here.
/// </summary>
[AggregateRoot]
public class XboxUser : AuditableEntity
{
    /// <summary>For EF. An account begins as Xbox gives it: <see cref="FromXbox"/>.</summary>
    private XboxUser() { }

    /// <summary>The account Xbox (the Halo API) gave: its XUID and the gamertag it has now.</summary>
    public static XboxUser FromXbox(long xboxUserID, string gamertag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gamertag);
        return new() { XboxUserID = xboxUserID, Gamertag = gamertag };
    }

    // The XUID is Xbox's (never generated); internal so tests may give one. Nothing outside the account sets its gamertag,
    // its user or its past gamertags: only its methods, and EF (which uses the private setters and fields).
    public long XboxUserID { get; internal set; }
    /// <summary>The gamertag it has now, as Xbox spells it.</summary>
    public string Gamertag { get; private set; } = null!;

    /// <summary>The user tied to it (<see cref="Models.User.TieToXbox(XboxUser)"/>): the user's foreign key, set from their side.</summary>
    public User? User { get; private set; }
    // Its games' players: made only by their match (Match.AddParticipant), so read-only here (EF uses the field)
    public IReadOnlyCollection<MatchParticipant> MatchParticipants => _matchParticipants;
    private readonly HashSet<MatchParticipant> _matchParticipants = [];

    /// <summary>The gamertags it had before: one per gamertag (compared normalised), made and changed only here. Its methods
    /// need them loaded (XboxUserQueries.WithPastGamertags): an account whose history wasn't loaded finds none of them.</summary>
    public IReadOnlyCollection<XboxGamertagHistory> PastGamertags => _pastGamertags;
    private readonly List<XboxGamertagHistory> _pastGamertags = [];

    /// <summary>When it last let go of a gamertag, as far as its history knows (the latest <see cref="XboxGamertagHistory.LastSeen"/>):
    /// before then its current gamertag may have been someone else's. Null when no past gamertag has an end. Needs
    /// <see cref="PastGamertags"/> loaded.</summary>
    public DateTime? LastRenamedAt => _pastGamertags.Where(h => h.LastSeen != null).Max(h => h.LastSeen);

    /// <summary>
    /// Xbox gave a new gamertag, <paramref name="gamertag"/>, for the account at <paramref name="at"/>. The one it had goes into
    /// its history first (<see cref="GamertagHistorySource.RenameDetected"/>), current from when it was stored (the row's last
    /// change) until now, so seasons played under it still find the account; a change of spelling alone (case, spaces) is no
    /// rename and records nothing. Needs <see cref="PastGamertags"/> loaded.
    /// </summary>
    public void Renamed(string gamertag, DateTime at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gamertag);
        if (XboxGamertagHistory.Normalize(Gamertag) != XboxGamertagHistory.Normalize(gamertag))
        {
            // The row's last change is stamped by the wall clock and `at` by the caller's: with skew between them it may come
            // after `at`, which RecordPastGamertag would refuse (DatesOutOfOrder) and lose the old gamertag. Then from `at`.
            DateTime? since = ModifiedAt > DateTime.MinValue ? (ModifiedAt <= at ? ModifiedAt : at) : null;
            // Recorded, or NoGamertag for an account stored blank before FromXbox refused one: nothing to keep then
            _ = RecordPastGamertag(Gamertag, since, at, GamertagHistorySource.RenameDetected);
        }
        Gamertag = gamertag;
    }

    /// <summary>
    /// The account had <paramref name="gamertag"/> between <paramref name="firstSeen"/> and <paramref name="lastSeen"/> (UTC, null
    /// when unknown), as <paramref name="source"/> knows. A gamertag it has in its history already keeps its row, whose dates
    /// widen to cover these (<see cref="XboxGamertagHistory.Widen"/>); a <paramref name="note"/>, if given, replaces its note.
    /// Refuses a blank gamertag and dates out of order. Needs <see cref="PastGamertags"/> loaded.
    /// </summary>
    public RecordPastGamertagResult RecordPastGamertag(string gamertag, DateTime? firstSeen, DateTime? lastSeen, GamertagHistorySource source, string? note = null)
    {
        if (string.IsNullOrWhiteSpace(gamertag))
            return RecordPastGamertagResult.NoGamertag;
        if (firstSeen is DateTime first && lastSeen is DateTime last && first > last)
            return RecordPastGamertagResult.DatesOutOfOrder;
        var normalized = XboxGamertagHistory.Normalize(gamertag);
        var row = _pastGamertags.FirstOrDefault(h => h.NormalizedGamertag == normalized);
        if (row is null)
        {
            row = new XboxGamertagHistory(this, gamertag, firstSeen, lastSeen, source);
            _pastGamertags.Add(row);
        }
        else
            row.Widen(firstSeen, lastSeen);
        row.Annotate(note);
        return RecordPastGamertagResult.Recorded;
    }

    /// <summary>A sysadmin's: takes out a past gamertag one added by hand (<see cref="GamertagHistorySource.Manual"/>); the ones
    /// the app saw stay. Needs <see cref="PastGamertags"/> loaded.</summary>
    public RemovePastGamertagResult RemovePastGamertag(int xboxGamertagHistoryID)
    {
        var row = _pastGamertags.FirstOrDefault(h => h.XboxGamertagHistoryID == xboxGamertagHistoryID);
        if (row is null)
            return RemovePastGamertagResult.NotFound;
        if (!row.IsRemovable)
            return RemovePastGamertagResult.NotManual;
        _pastGamertags.Remove(row);
        return RemovePastGamertagResult.Removed;
    }
}

/// <summary>What <see cref="XboxUser.RecordPastGamertag"/> did.</summary>
public enum RecordPastGamertagResult
{
    Recorded = 1,
    /// <summary>No gamertag was given.</summary>
    NoGamertag = 2,
    /// <summary>First seen after it was last seen.</summary>
    DatesOutOfOrder = 3,
}

/// <summary>What <see cref="XboxUser.RemovePastGamertag"/> did.</summary>
public enum RemovePastGamertagResult
{
    Removed = 1,
    /// <summary>The account has no such past gamertag.</summary>
    NotFound = 2,
    /// <summary>The app saw it (a rename, the import, an approved claim): only one added by hand is removed.</summary>
    NotManual = 3,
}
