namespace GrifballWebApp.Database.Models;

/// <summary>
/// One user merged into another, and everything the merge changed (<see cref="Manifest"/>), so a sysadmin can see why
/// an account is gone and undo the merge. It is itself the log: no row history, and no foreign keys to the users (the
/// source is deleted by the merge, and the record outlives both).
/// </summary>
public class UserMerge
{
    public int UserMergeID { get; set; }
    public DateTime MergedAt { get; set; }
    /// <summary>The signed-in user who caused it (a sysadmin merging or approving a claim, a user setting their gamertag); null for the import or a background job.</summary>
    public int? TriggeredByUserID { get; set; }
    public MergeTrigger Trigger { get; set; }
    /// <summary>Why, e.g. "claim 12" or "Discord ID match".</summary>
    public string? Reason { get; set; }
    public int SourceUserID { get; set; }
    public string? SourceUserName { get; set; }
    public int TargetUserID { get; set; }
    public string? TargetUserName { get; set; }
    /// <summary>The rows the merge moved, removed and added, as JSON (see GrifballWebApp.Server.Profile.MergeManifest).</summary>
    public string Manifest { get; set; } = "";
    /// <summary>Rows changed (moved), removed and added: the manifest's counts, for the list.</summary>
    public int RowsMoved { get; set; }
    public int RowsRemoved { get; set; }
    public int RowsAdded { get; set; }
    public DateTime? UndoneAt { get; set; }
    public int? UndoneByUserID { get; set; }
    public string? UndoNote { get; set; }
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
