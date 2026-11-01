#nullable disable

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A video of the league's YouTube channel (a match VOD, a cast, a stream), found by the worker's daily refresh
/// (VodService: the channel's snapshot, its RSS feed, the YouTube Data API when a key is set). Its title is matched to a
/// season, its match and teams: a confident match links itself (<see cref="VodMatchStatus.Auto"/>), an unsure one waits
/// on the historical import page for a sysadmin. The row stays when dismissed, so a dismissed video is never suggested again.
/// </summary>
public class Vod : AuditableEntity
{
    public int VodID { get; set; }
    /// <summary>The 11-character video ID (youtube.com/watch?v=...): what a video is found by, whichever way it came.</summary>
    public string YouTubeVideoID { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public DateTime PublishedAt { get; set; }
    /// <summary>Read off the channel page's "2w ago": replaced by the exact date when the feed or the API gives it.</summary>
    public bool PublishedAtIsApproximate { get; set; }
    /// <summary>In seconds (a stream can run past the 24 hours a SQL Server time holds).</summary>
    public int? DurationSeconds { get; set; }
    public string ChannelID { get; set; }
    public int? SeasonID { get; set; }
    public Season Season { get; set; }
    public int? SeasonMatchID { get; set; }
    public SeasonMatch SeasonMatch { get; set; }
    public VodMatchStatus MatchStatus { get; set; }
    /// <summary>Why the match is what it is (what the title gave, what was missing), for the review list.</summary>
    public string MatchNote { get; set; }
    public virtual ICollection<VodTeam> Teams { get; set; } = new List<VodTeam>();
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
