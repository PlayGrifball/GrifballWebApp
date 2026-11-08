using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A gamertag ONE Xbox account (XUID) had before: its renames. Another Xbox account of the same person is not modelled
/// (a user has one Xbox account). <see cref="FirstSeen"/> and <see cref="LastSeen"/> bound when the account is known to
/// have used it (UTC; null when unknown), which is what lets a historical season's gamertag be matched to the account
/// that had it then rather than to whoever holds it now. Part of its account: made and changed only through it
/// (<see cref="XboxUser.Renamed"/>, <see cref="XboxUser.RecordPastGamertag"/>, <see cref="XboxUser.RemovePastGamertag"/>),
/// read through <see cref="XboxUser.PastGamertags"/>.
/// </summary>
[AggregatePart(typeof(XboxUser))]
public partial class XboxGamertagHistory : AuditableEntity
{
    /// <summary>The longest note kept; a longer one is cut.</summary>
    public const int MaxNoteLength = 200;

    /// <summary>For EF.</summary>
    private XboxGamertagHistory() { }

    /// <summary>The account had the gamertag (cleaned: <see cref="Clean"/>) between the dates given.</summary>
    internal XboxGamertagHistory(XboxUser xboxUser, string gamertag, DateTime? firstSeen, DateTime? lastSeen, GamertagHistorySource source)
        : this(xboxUser.XboxUserID, gamertag, firstSeen, lastSeen, source)
    {
        XboxUser = xboxUser;
    }

    /// <summary>For tests: a past gamertag of the account with the XUID, seeded without loading the account.</summary>
    internal XboxGamertagHistory(long xboxUserID, string gamertag, DateTime? firstSeen = null, DateTime? lastSeen = null,
        GamertagHistorySource source = GamertagHistorySource.Manual, string? note = null)
    {
        XboxUserID = xboxUserID;
        Gamertag = Clean(gamertag);
        NormalizedGamertag = Normalize(gamertag);
        FirstSeen = firstSeen;
        LastSeen = lastSeen;
        Source = source;
        Annotate(note);
    }

    public int XboxGamertagHistoryID { get; internal set; }
    public long XboxUserID { get; private set; }
    public string Gamertag { get; private set; } = null!;
    /// <summary>The gamertag trimmed, inner whitespace collapsed and upper-cased: what lookups compare (<see cref="Normalize"/>).</summary>
    public string NormalizedGamertag { get; private set; } = null!;
    /// <summary>No later than <see cref="LastSeen"/> when both are known (CK_Xbox_PastGamertags_SeenInOrder).</summary>
    public DateTime? FirstSeen { get; private set; }
    public DateTime? LastSeen { get; private set; }
    public GamertagHistorySource Source { get; private set; }
    /// <summary>Why a sysadmin added it, e.g. "known as this in Winter League 2019".</summary>
    public string? Note { get; private set; }

    public virtual XboxUser XboxUser { get; private set; } = null!;

    /// <summary>Trimmed, inner whitespace collapsed to one space: how gamertags are stored on historical rows.</summary>
    public static string Clean(string gamertag) => Whitespace().Replace(gamertag.Trim(), " ");

    /// <summary><see cref="Clean"/>, upper-cased: what gamertags are compared by.</summary>
    public static string Normalize(string gamertag) => Clean(gamertag).ToUpperInvariant();

    /// <summary>
    /// The account may have had the gamertag at <paramref name="from"/>: it hadn't let go of it before then (or either is
    /// unknown). Only LastSeen bounds the period: FirstSeen is when the app first saw the account with it (a rename
    /// noticed, an approved claim), not when the account took it, and archive seasons are usually older. For queries.
    /// </summary>
    public static Expression<Func<XboxGamertagHistory, bool>> HeldAt(DateTime? from)
        => h => h.LastSeen == null || from == null || h.LastSeen >= from;

    /// <summary><see cref="HeldAt"/>, for a loaded row.</summary>
    public bool WasHeldAt(DateTime? from) => LastSeen is null || from is null || LastSeen >= from;

    /// <summary>
    /// Known to have been used between these dates too: <see cref="FirstSeen"/> moves back and <see cref="LastSeen"/> forward
    /// to cover them. Seen at a time after the last one known (only a start given), it was still held then: the first sighting
    /// never ends up after the last (<see cref="LastSeen"/>, the bound matching reads, stays as it is).
    /// </summary>
    internal void Widen(DateTime? firstSeen, DateTime? lastSeen)
    {
        FirstSeen = Earliest(FirstSeen, firstSeen);
        LastSeen = Latest(LastSeen, lastSeen);
        if (FirstSeen > LastSeen)
            FirstSeen = LastSeen;
    }

    /// <summary>A sysadmin's note, trimmed and cut to <see cref="MaxNoteLength"/>; a blank one leaves the note as it is.</summary>
    internal void Annotate(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
            return;
        var trimmed = note.Trim();
        Note = trimmed.Length > MaxNoteLength ? trimmed[..MaxNoteLength] : trimmed;
    }

    private static DateTime? Earliest(DateTime? a, DateTime? b) => a is null ? b : b is null ? a : (a < b ? a : b);
    private static DateTime? Latest(DateTime? a, DateTime? b) => a is null ? b : b is null ? a : (a > b ? a : b);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

/// <summary>How a past gamertag became known.</summary>
public enum GamertagHistorySource
{
    /// <summary>The Halo API returned a new gamertag for a known XUID (or the XboxUsers row history shows one).</summary>
    RenameDetected = 1,
    /// <summary>The historical import tied a season's gamertag to the XUID that played its Waypoint matches.</summary>
    HistoricalImport = 2,
    /// <summary>A commissioner approved a roster match suggested by the historical import.</summary>
    RosterMatch = 3,
    /// <summary>Added or approved by a commissioner or sysadmin.</summary>
    Manual = 4,
}
