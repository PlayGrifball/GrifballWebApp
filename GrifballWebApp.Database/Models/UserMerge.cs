using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// One user merged into another, and everything the merge changed (<see cref="Manifest"/>), so a sysadmin can see why
/// an account is gone and undo the merge. It is itself the log: no row history, and no foreign keys to the users (the
/// source is deleted by the merge, and the record outlives both).
/// </summary>
/// <remarks>
/// Begins as a merge is made (<see cref="Record"/>), and changes once at most: a sysadmin undoes it (<see cref="Undo"/>).
/// What it records never changes after that.
/// </remarks>
[AggregateRoot]
public class UserMerge
{
    /// <summary>The longest reason kept; a longer one is cut.</summary>
    public const int ReasonMaxLength = 200;
    /// <summary>The longest user name kept (Identity's).</summary>
    public const int UserNameMaxLength = 256;
    /// <summary>The longest undo note kept; a longer one is cut.</summary>
    public const int UndoNoteMaxLength = 500;

    /// <summary>For EF. A merge record begins with <see cref="Record"/>.</summary>
    private UserMerge() { }

    /// <summary>
    /// The record of <paramref name="sourceUserID"/> merged into <paramref name="targetUserID"/> at <paramref name="mergedAt"/>,
    /// standing (not undone). The <paramref name="manifest"/> is what the merge changed, as JSON, with its counts. A
    /// <paramref name="reason"/> longer than <see cref="ReasonMaxLength"/> is cut. Refuses a user merged into itself, a count
    /// below zero and a trigger the model doesn't know: no merge makes those.
    /// </summary>
    public static UserMerge Record(DateTime mergedAt, MergeTrigger trigger, int? triggeredByUserID, string? reason,
        int sourceUserID, string? sourceUserName, int targetUserID, string? targetUserName,
        string manifest, int rowsMoved, int rowsRemoved, int rowsAdded)
    {
        if (sourceUserID == targetUserID)
            throw new ArgumentException("A user is never merged into itself", nameof(targetUserID));
        if (!Enum.IsDefined(trigger))
            throw new ArgumentOutOfRangeException(nameof(trigger), trigger, "Not a known trigger");
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentOutOfRangeException.ThrowIfNegative(rowsMoved);
        ArgumentOutOfRangeException.ThrowIfNegative(rowsRemoved);
        ArgumentOutOfRangeException.ThrowIfNegative(rowsAdded);
        return new()
        {
            MergedAt = mergedAt,
            Trigger = trigger,
            TriggeredByUserID = triggeredByUserID,
            Reason = Cut(reason, ReasonMaxLength),
            SourceUserID = sourceUserID,
            SourceUserName = Cut(sourceUserName, UserNameMaxLength),
            TargetUserID = targetUserID,
            TargetUserName = Cut(targetUserName, UserNameMaxLength),
            Manifest = manifest,
            RowsMoved = rowsMoved,
            RowsRemoved = rowsRemoved,
            RowsAdded = rowsAdded,
        };
    }

    // Nothing outside the record sets what it records: only Record, Undo and EF (which uses the private setters). The key
    // is internal: the database makes it, and tests may give one.
    public int UserMergeID { get; internal set; }
    public DateTime MergedAt { get; private set; }
    /// <summary>The signed-in user who caused it (a sysadmin merging or approving a claim, a user setting their gamertag); null for the import or a background job.</summary>
    public int? TriggeredByUserID { get; private set; }
    public MergeTrigger Trigger { get; private set; }
    /// <summary>Why, e.g. "claim 12" or "Discord ID match".</summary>
    public string? Reason { get; private set; }
    /// <summary>The user merged (never the target: CK_Auth_UserMerges_DifferentUsers).</summary>
    public int SourceUserID { get; private set; }
    public string? SourceUserName { get; private set; }
    public int TargetUserID { get; private set; }
    public string? TargetUserName { get; private set; }
    /// <summary>The rows the merge moved, removed and added, as JSON (see GrifballWebApp.Server.Profile.MergeManifest).</summary>
    public string Manifest { get; private set; } = "";
    /// <summary>Rows changed (moved), removed and added: the manifest's counts, for the list. Never below zero
    /// (CK_Auth_UserMerges_RowCountsNotNegative).</summary>
    public int RowsMoved { get; private set; }
    public int RowsRemoved { get; private set; }
    public int RowsAdded { get; private set; }
    /// <summary>When a sysadmin undid it; null while it stands. Only an undone merge has an undoer or a note
    /// (CK_Auth_UserMerges_UndoOnlyWhenUndone).</summary>
    public DateTime? UndoneAt { get; private set; }
    public int? UndoneByUserID { get; private set; }
    public string? UndoNote { get; private set; }

    /// <summary>A merge not undone: what it changed is still so. For queries.</summary>
    public static readonly Expression<Func<UserMerge, bool>> Standing = m => m.UndoneAt == null;

    private static readonly Func<UserMerge, bool> _isStanding = Standing.Compile();

    /// <summary><see cref="Standing"/>, for a loaded record.</summary>
    public bool IsStanding => _isStanding(this);

    /// <summary>
    /// The merges that may depend on this one: made after it, standing, and into or out of either of its users. One of
    /// them that changed the same rows has to be undone before this one can be. For queries.
    /// </summary>
    public Expression<Func<UserMerge, bool>> LaterStandingMergesOfItsUsers()
    {
        var (id, source, target) = (UserMergeID, SourceUserID, TargetUserID);
        return m => m.UserMergeID > id && m.UndoneAt == null
            && (m.SourceUserID == source || m.SourceUserID == target || m.TargetUserID == source || m.TargetUserID == target);
    }

    /// <summary>
    /// Marks a standing merge undone by <paramref name="undoneByUserID"/> at <paramref name="undoneAt"/>, with the
    /// <paramref name="note"/> (trimmed, none when blank, cut to <see cref="UndoNoteMaxLength"/>). An undone merge stays
    /// as it was. The rows themselves are put back by the caller, in the same transaction.
    /// </summary>
    public UserMergeUndo Undo(DateTime undoneAt, int? undoneByUserID, string? note)
    {
        if (!IsStanding)
            return UserMergeUndo.AlreadyUndone;
        UndoneAt = undoneAt;
        UndoneByUserID = undoneByUserID;
        UndoNote = Cut(string.IsNullOrWhiteSpace(note) ? null : note.Trim(), UndoNoteMaxLength);
        return UserMergeUndo.Undone;
    }

    private static string? Cut(string? text, int max) => text is { Length: var length } && length > max ? text[..max] : text;
}

/// <summary>What <see cref="UserMerge.Undo"/> did.</summary>
public enum UserMergeUndo
{
    /// <summary>It is undone now.</summary>
    Undone,
    /// <summary>It was undone already, and stays as it was.</summary>
    AlreadyUndone,
}

/// <summary>What caused a merge.</summary>
public enum MergeTrigger
{
    /// <summary>A sysadmin merged two users on the merge page.</summary>
    Manual = 1,
    /// <summary>A sysadmin approved a roster-match or set-gamertag claim.</summary>
    ClaimApproval = 2,
    /// <summary>A user set their gamertag, and its placeholder (or one of a past gamertag of theirs) followed.</summary>
    SetGamertag = 3,
    /// <summary>A sysadmin approved a claim an XUID lookup made.</summary>
    XuidLookup = 4,
    /// <summary>A sysadmin approved a user's claim of a past gamertag.</summary>
    UserClaim = 5,
    /// <summary>The placeholder of an Xbox account nobody had signed up with.</summary>
    XboxPlaceholder = 6,
    /// <summary>The same Discord account: the archive's Discord ID, or signing in with Discord.</summary>
    DiscordMatch = 7,
    /// <summary>The historical import.</summary>
    Import = 8,
    /// <summary>A sysadmin merged a possible duplicate the claims page suggested.</summary>
    DuplicateSuggestion = 9,
}
