using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A video of the league's YouTube channel (a match VOD, a cast, a stream), found by the worker's daily refresh
/// (VodService: the channel's snapshot, its RSS feed, the YouTube Data API when a key is set). Its title is matched to a
/// season, its match and teams: a confident match links itself (<see cref="VodMatchStatus.Auto"/>), an unsure one waits
/// on the historical import page for a sysadmin. The row stays when dismissed, so a dismissed video is never suggested again.
/// The root of its team links (<see cref="VodTeam"/>): they are made and removed only through its methods.
/// <list type="bullet">
/// <item>found (<see cref="Found"/>): waiting to be matched (<see cref="VodMatchStatus.NeedsReview"/>);</item>
/// <item>matched (<see cref="Rematch"/>): linked by the matcher, or its suggestion stored for review, while undecided;</item>
/// <item>linked by a sysadmin (<see cref="Confirm"/>), or dismissed (<see cref="Dismiss"/>): decided, never matched again.</item>
/// </list>
/// A linked VOD (Auto or Confirmed) has a season (CK_Event_Vods_LinkedHasSeason), and a VOD of a match is of that match's
/// season (CK_Event_Vods_MatchHasSeason).
/// </summary>
[AggregateRoot]
public class Vod : AuditableEntity
{
    public const int YouTubeVideoIDMaxLength = 20;
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 5000;
    public const int ChannelIDMaxLength = 40;
    public const int MatchNoteMaxLength = 500;

    /// <summary>For EF. A VOD begins through <see cref="Found"/>.</summary>
    private Vod() { }

    /// <summary>
    /// A video the refresh found, not matched yet (<see cref="VodMatchStatus.NeedsReview"/> until <see cref="Rematch"/>):
    /// its title and description cut to what a row holds.
    /// </summary>
    public static Vod Found(string youTubeVideoID, string title, string? description, DateTime publishedAt, bool publishedAtIsApproximate, int? durationSeconds, string channelID)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(youTubeVideoID);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(channelID);
        return new()
        {
            YouTubeVideoID = youTubeVideoID,
            Title = Truncate(title.Trim(), TitleMaxLength),
            Description = description is null ? null : Truncate(description, DescriptionMaxLength),
            PublishedAt = publishedAt,
            PublishedAtIsApproximate = publishedAtIsApproximate,
            DurationSeconds = durationSeconds,
            ChannelID = channelID,
            MatchStatus = VodMatchStatus.NeedsReview,
        };
    }

    /// <summary>
    /// The same in any state, past the rules: for tests, which seed VODs as each status leaves them (and old rows for the
    /// migration tests).
    /// </summary>
    internal static Vod Seed(string youTubeVideoID, string title, DateTime publishedAt, VodMatchStatus status, int? seasonID = null, int? seasonMatchID = null,
        IEnumerable<int>? teamIDs = null, string channelID = "UC", int? durationSeconds = null)
    {
        var vod = new Vod
        {
            YouTubeVideoID = youTubeVideoID,
            Title = title,
            PublishedAt = publishedAt,
            DurationSeconds = durationSeconds,
            ChannelID = channelID,
            MatchStatus = status,
            SeasonID = seasonID,
            SeasonMatchID = seasonMatchID,
        };
        foreach (var teamID in (teamIDs ?? []).Distinct())
            vod._teams.Add(new VodTeam(teamID));
        return vod;
    }

    // Nothing outside the VOD sets its fields, its foreign keys or its navigations: only its own methods below, and EF
    // (which uses the private setters). The key is internal: the database makes it, and tests may give one.
    public int VodID { get; internal set; }
    /// <summary>The 11-character video ID (youtube.com/watch?v=...): what a video is found by, whichever way it came.</summary>
    public string YouTubeVideoID { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTime PublishedAt { get; private set; }
    /// <summary>Read off the channel page's "2w ago": replaced by the exact date when the feed or the API gives it.</summary>
    public bool PublishedAtIsApproximate { get; private set; }
    /// <summary>In seconds (a stream can run past the 24 hours a SQL Server time holds).</summary>
    public int? DurationSeconds { get; private set; }
    public string ChannelID { get; private set; } = null!;
    /// <summary>Its season: always set once linked (Auto or Confirmed), and whenever it has a match.</summary>
    public int? SeasonID { get; private set; }
    public Season? Season { get; private set; }
    public int? SeasonMatchID { get; private set; }
    public SeasonMatch? SeasonMatch { get; private set; }
    public VodMatchStatus MatchStatus { get; private set; }
    /// <summary>Why the match is what it is (what the title gave, what was missing), for the review list.</summary>
    public string? MatchNote { get; private set; }
    // Read-only: only the VOD's own methods add or remove a team link (EF uses the field)
    public IReadOnlyCollection<VodTeam> Teams => _teams;
    private readonly HashSet<VodTeam> _teams = [];

    /// <summary>Shown on the site's pages: linked by the matcher or a sysadmin (<see cref="IsVisible"/> for a loaded VOD).</summary>
    public static readonly Expression<Func<Vod, bool>> Visible = v => v.MatchStatus == VodMatchStatus.Auto || v.MatchStatus == VodMatchStatus.Confirmed;

    /// <summary>Whether it shows on the site's pages (<see cref="Visible"/> for queries).</summary>
    public bool IsVisible => MatchStatus is VodMatchStatus.Auto or VodMatchStatus.Confirmed;

    /// <summary>Not decided by a sysadmin yet (Auto or NeedsReview): the matcher matches it again on each refresh
    /// (<see cref="IsUndecided"/> for a loaded VOD).</summary>
    public static readonly Expression<Func<Vod, bool>> Undecided = v => v.MatchStatus == VodMatchStatus.Auto || v.MatchStatus == VodMatchStatus.NeedsReview;

    /// <summary>Whether the matcher may still change it (<see cref="Undecided"/> for queries).</summary>
    public bool IsUndecided => MatchStatus is VodMatchStatus.Auto or VodMatchStatus.NeedsReview;

    /// <summary>
    /// What a later read of the video gives a known VOD: a corrected title, a description, the exact date in place of the
    /// channel page's approximate one, and a length it lacked. Nothing it has is lost. True when anything changed.
    /// </summary>
    public bool Refresh(string? title, string? description, DateTime? publishedAt, bool publishedAtIsApproximate, int? durationSeconds)
    {
        var changed = false;
        if (!string.IsNullOrWhiteSpace(title) && Title != Truncate(title.Trim(), TitleMaxLength))
        {
            Title = Truncate(title.Trim(), TitleMaxLength);
            changed = true;
        }
        if (description is not null && Description != Truncate(description, DescriptionMaxLength))
        {
            Description = Truncate(description, DescriptionMaxLength);
            changed = true;
        }
        if (PublishedAtIsApproximate && publishedAt is { } exact && !publishedAtIsApproximate)
        {
            PublishedAt = exact;
            PublishedAtIsApproximate = false;
            changed = true;
        }
        if (DurationSeconds is null && durationSeconds is not null)
        {
            DurationSeconds = durationSeconds;
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// The matcher's answer for an undecided VOD: linked (<see cref="VodMatchStatus.Auto"/>, which needs a season) or its
    /// suggestion stored for review (<see cref="VodMatchStatus.NeedsReview"/>). Its team links become <paramref name="teamIDs"/>.
    /// A VOD a sysadmin decided is never matched again.
    /// </summary>
    public void Rematch(VodMatchStatus status, int? seasonID, int? seasonMatchID, IReadOnlyCollection<int> teamIDs, string note)
    {
        if (!IsUndecided)
            throw new InvalidOperationException($"VOD {VodID} was decided by a sysadmin ({MatchStatus}): the matcher leaves it");
        if (status is not (VodMatchStatus.Auto or VodMatchStatus.NeedsReview))
            throw new ArgumentException($"The matcher links a VOD or suggests a link, never {status}", nameof(status));
        if (status is VodMatchStatus.Auto && seasonID is null)
            throw new ArgumentException("A linked VOD has a season", nameof(seasonID));
        if (seasonMatchID is not null && seasonID is null)
            throw new ArgumentException("A VOD of a match is of its season", nameof(seasonID));

        MatchStatus = status;
        SeasonID = seasonID;
        SeasonMatchID = seasonMatchID;
        MatchNote = Truncate(note, MatchNoteMaxLength);
        SetTeams(teamIDs);
    }

    /// <summary>
    /// Linked as a sysadmin says (<see cref="VodMatchStatus.Confirmed"/>): to a season, one of its matches if any, and its
    /// teams. Whatever the VOD was before, even dismissed; no refresh changes it after. The caller has checked that the
    /// match and the teams are the season's.
    /// </summary>
    public void Confirm(int seasonID, int? seasonMatchID, IReadOnlyCollection<int> teamIDs)
    {
        SeasonID = seasonID;
        SeasonMatchID = seasonMatchID;
        MatchStatus = VodMatchStatus.Confirmed;
        SetTeams(teamIDs);
    }

    /// <summary>Hidden for good: not a VOD of anything (or not wanted), and never suggested again. Its links stay as they were.</summary>
    public void Dismiss() => MatchStatus = VodMatchStatus.Dismissed;

    /// <summary>Its team links become <paramref name="teamIDs"/>: a link gone is removed (EF deletes the orphan), a new one added.</summary>
    private void SetTeams(IReadOnlyCollection<int> teamIDs)
    {
        _teams.RemoveWhere(t => !teamIDs.Contains(t.TeamID));
        foreach (var teamID in teamIDs.Where(id => _teams.All(t => t.TeamID != id)).Distinct())
            _teams.Add(new VodTeam(teamID));
    }

    private static string Truncate(string value, int length) => value.Length > length ? value[..length] : value;
}

public enum VodMatchStatus
{
    /// <summary>Linked by the matcher; matched again on each refresh while the season's data changes.</summary>
    Auto = 0,
    /// <summary>Linked (or confirmed) by a sysadmin: never changed by a refresh.</summary>
    Confirmed = 1,
    /// <summary>The matcher wasn't sure: its suggestion (if any) is stored, nothing shows on the site until a sysadmin decides.</summary>
    NeedsReview = 2,
    /// <summary>Not a VOD of anything (or not wanted): hidden, and never suggested again.</summary>
    Dismissed = 3,
}
