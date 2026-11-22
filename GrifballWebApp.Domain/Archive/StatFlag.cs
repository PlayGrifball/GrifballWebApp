using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using System.Linq.Expressions;

namespace GrifballWebApp.Domain.Archive;

/// <summary>
/// A plain-rule finding about a game's stats (<see cref="StatFlagRule"/>), for staff to check: a question, never a
/// change (it never touches the game it is about). Found whenever a game is saved by hand, by a Halo pull, or by the
/// historical import (the Application's StatFlagService, the one place every save path runs the rules through), it
/// names the game, the player when the rule is about one, and why. Staff dismiss it (<see cref="Dismiss"/>) or fix the
/// game themselves; re-running the rules on the same game replaces an open flag rather than adding another
/// (<see cref="Rewrite"/>).
/// </summary>
[AggregateRoot]
public class StatFlag : AuditableEntity
{
    public const int ReasonMaxLength = 500;

    /// <summary>For EF. A flag begins through <see cref="Found"/>.</summary>
    private StatFlag() { }

    /// <summary>
    /// A finding of <paramref name="rule"/>, against a Halo Infinite match (<paramref name="matchID"/>), a recorded
    /// game (<paramref name="recordedGameID"/>), or a season match's series as a whole (<paramref name="seasonMatchID"/>,
    /// <see cref="StatFlagRule.SeriesDoesNotMatchGames"/>'s only subject) - exactly one of the three
    /// (<see cref="CheckGame"/>) - about <paramref name="playerUserID"/>'s line when the rule is about one player,
    /// else the whole subject (a team, the series). <paramref name="discriminator"/> tells apart two findings of the
    /// same rule about no one player (a side's number, an unlinked or not-yet-linked Halo player's XUID): without it
    /// they'd share a key and a re-run could rewrite one twice while never clearing the other.
    /// </summary>
    public static StatFlag Found(StatFlagRule rule, Guid? matchID, int? recordedGameID, int? seasonMatchID, int? playerUserID, string reason, DateTime foundAt, long? discriminator = null)
    {
        CheckGame(matchID, recordedGameID, seasonMatchID);
        var flag = new StatFlag
        {
            Rule = rule,
            MatchID = matchID,
            RecordedGameID = recordedGameID,
            SeasonMatchID = seasonMatchID,
            PlayerUserID = playerUserID,
            Discriminator = discriminator,
            Reason = CheckReason(reason),
            FoundAt = foundAt,
        };
        flag.Raise(new StatFlagFound(flag));
        return flag;
    }

    /// <summary>A flag is of exactly one subject: a Halo Infinite match, a recorded game, or a season match's series
    /// (CK_Event_StatFlags_OneGame) - never more than one, and never none.</summary>
    private static void CheckGame(Guid? matchID, int? recordedGameID, int? seasonMatchID)
    {
        var count = (matchID is null ? 0 : 1) + (recordedGameID is null ? 0 : 1) + (seasonMatchID is null ? 0 : 1);
        if (count != 1)
            throw new ArgumentException("A flag is of one subject: a Halo Infinite match, a recorded game, or a season match's series, never more than one or none");
    }

    private static string CheckReason(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Length > ReasonMaxLength)
            throw new ArgumentException($"A flag's reason is at most {ReasonMaxLength} characters", nameof(reason));
        return reason;
    }

    // Nothing outside the flag sets its fields: only Found, Rewrite and Dismiss, and EF (which uses the private setters)
    public int StatFlagID { get; internal set; }
    public StatFlagRule Rule { get; private set; }
    public Guid? MatchID { get; private set; }
    public Match? Match { get; private set; }
    public int? RecordedGameID { get; private set; }
    public RecordedGame? RecordedGame { get; private set; }
    /// <summary>The season match whose series this flag is about (<see cref="StatFlagRule.SeriesDoesNotMatchGames"/>
    /// only): never a particular game of it, so a game added, replaced or removed from the series is never this
    /// flag's to clear - StatFlagService.CheckSeriesGamesAsync alone, by this ID, always can. Cascades with the
    /// season match itself (removed, or its team deleted): nothing else needs to clear it.</summary>
    public int? SeasonMatchID { get; private set; }
    public SeasonMatch? SeasonMatch { get; private set; }
    /// <summary>Whose line the rule is about; null when the rule is about the whole game. No foreign key (like
    /// <see cref="GrifballWebApp.Domain.Players.DuplicateDismissal"/>'s user IDs): the player may be merged away later,
    /// and the flag still names who it was found about.</summary>
    public int? PlayerUserID { get; private set; }
    /// <summary>Part of the flag's identity alongside <see cref="Rule"/> and <see cref="PlayerUserID"/>, for findings
    /// that are about no one player and so would otherwise collide (StatFlagService.ReconcileAsync): null for a rule
    /// that's at most one finding a game.</summary>
    public long? Discriminator { get; private set; }
    public string Reason { get; private set; } = null!;
    public DateTime FoundAt { get; private set; }
    public DateTime? DismissedAt { get; private set; }
    /// <summary>Who dismissed it; null when no one was signed in, or they were merged away since. No foreign key, for
    /// the same reason as <see cref="PlayerUserID"/>.</summary>
    public int? DismissedByUserID { get; private set; }

    /// <summary>Not dismissed: still a question for staff (<see cref="IsOpen"/> for a loaded flag).</summary>
    public static readonly Expression<Func<StatFlag, bool>> Open = f => f.DismissedAt == null;

    public bool IsOpen => DismissedAt is null;

    /// <summary>
    /// What a re-run of the rule that found it does, for the same game, rule and player: an open flag's wording is
    /// simply brought up to date (never a second row for the same finding) - but only when the reason actually
    /// changed: the same finding re-confirmed leaves <see cref="FoundAt"/> alone (it was found when it was found, not
    /// every time since) and raises nothing, so an unchanged flag never jumps to the top of the newest-first list or
    /// looks freshly found. A dismissed one stays dismissed - staff already looked at it - unless the reason itself
    /// changed, which reopens it: what staff dismissed isn't what is being found now. Returns whether anything
    /// changed, so a caller reconciling many at once (StatFlagService.ReconcileInMemory) knows whether this one is
    /// worth saving.
    /// </summary>
    public bool Rewrite(string reason, DateTime foundAt)
    {
        reason = CheckReason(reason);
        if (Reason == reason)
            return false; // open: nothing to change; dismissed: stays dismissed, per the rule above
        var reopening = DismissedAt is not null;
        Reason = reason;
        FoundAt = foundAt;
        if (reopening)
        {
            DismissedAt = null;
            DismissedByUserID = null;
            Raise(new StatFlagReopened(this));
        }
        return true;
    }

    /// <summary>Staff looked at it: not wrong, or not worth fixing. <paramref name="dismissedByUserID"/> is null when no
    /// one was signed in. A no-op when it's dismissed already (two staff, or a stale list) rather than refusing: it
    /// says the same thing either way, so there's nothing to tell them went wrong.</summary>
    public void Dismiss(int? dismissedByUserID, DateTime dismissedAt)
    {
        if (DismissedAt is not null)
            return;
        DismissedAt = dismissedAt;
        DismissedByUserID = dismissedByUserID;
        Raise(new StatFlagDismissed(this));
    }

    /// <summary>A re-run no longer finds what this (still open) flag found: it's about to be removed
    /// (StatFlagService.ReconcileAsync), which raises nothing of its own - this says so instead, before it's gone.</summary>
    public void Clear() => Raise(new StatFlagCleared(this));
}
