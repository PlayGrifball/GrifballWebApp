using System.Globalization;

namespace GrifballWebApp.Application.Live;

/// <summary>
/// What a page shows, by name: a page watches the topics of what it shows, and loads it again when one changes
/// (<see cref="ILiveUpdates"/>). A topic is a kind, then its key after a colon where it has one ("season:12"). The client
/// builds the same names (grifballwebapp.client/src/app/core/live/liveTopics.ts).
/// </summary>
public static class LiveTopics
{
    /// <summary>The seasons: their list, the home page's events, and what a season's matches add to it.</summary>
    public const string Seasons = "seasons";

    /// <summary>A season's overview tabs: its summary, standings, schedule and this week, its players' stats.</summary>
    public static string Season(int seasonID) => Keyed(SeasonKind, seasonID);

    /// <summary>A season's playoff bracket: its matches, their teams and results.</summary>
    public static string Bracket(int seasonID) => Keyed(BracketKind, seasonID);

    /// <summary>A season's teams page: its teams, their rosters and records, and the season's banner.</summary>
    public static string Teams(int seasonID) => Keyed(TeamsKind, seasonID);

    /// <summary>A team's page: its roster, record and titles, its matches, its players' stats.</summary>
    public static string Team(int teamID) => Keyed(TeamKind, teamID);

    /// <summary>A season's draft: its teams and their rosters, its captains and their lock, its player pool.</summary>
    public static string Draft(int seasonID) => Keyed(DraftKind, seasonID);

    /// <summary>A season's signups: who signed up and how (the staff's signups and grades pages, the availability).</summary>
    public static string Signups(int seasonID) => Keyed(SignupsKind, seasonID);

    /// <summary>A season match's page: its teams, time, result, games, reschedules and VODs.</summary>
    public static string SeasonMatch(int seasonMatchID) => Keyed(SeasonMatchKind, seasonMatchID);

    /// <summary>A Halo Infinite match's page.</summary>
    public static string HaloMatch(Guid matchID) => $"{HaloMatchKind}:{matchID:D}";

    /// <summary>A player's profile: their names and accounts, their teams, titles and stats.</summary>
    public static string Player(int userID) => Keyed(PlayerKind, userID);

    /// <summary>An Xbox account's page, and the profile of whoever plays as it: its gamertags and its games.</summary>
    public static string Xbox(long xboxUserID) => $"{XboxKind}:{xboxUserID.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Late League: its seasons, standings, ladders and matches.</summary>
    public const string LateLeague = "lateleague";

    /// <summary>The matchmaking queue: its size.</summary>
    public const string Queue = "queue";

    /// <summary>The viewer's own place in the queue (queued, matched, their match over, kicked): sent to that user alone.</summary>
    public const string MyQueue = "queue:me";

    /// <summary>Staff: the users (user management, claims, past gamertags).</summary>
    public const string StaffUsers = "staff:users";

    /// <summary>Staff: the gamertag claims.</summary>
    public const string StaffClaims = "staff:claims";

    /// <summary>Staff: the user merges and the duplicates.</summary>
    public const string StaffMerges = "staff:merges";

    /// <summary>Staff: the historical import's jobs and sources.</summary>
    public const string StaffImports = "staff:imports";

    /// <summary>Staff: the VODs to review.</summary>
    public const string StaffVods = "staff:vods";

    /// <summary>Staff: the reschedule requests (the commissioner dashboard).</summary>
    public const string StaffReschedules = "staff:reschedules";

    /// <summary>Staff: the open stat flags (#548).</summary>
    public const string StaffStatFlags = "staff:statflags";

    public const string SeasonKind = "season", BracketKind = "bracket", TeamsKind = "teams", TeamKind = "team", DraftKind = "draft",
        SignupsKind = "signups", SeasonMatchKind = "seasonmatch",
        HaloMatchKind = "match", PlayerKind = "player", XboxKind = "xbox", StaffKind = "staff";

    /// <summary>The longest topic a client may name: a kind and a GUID with room to spare.</summary>
    public const int MaxLength = 64;

    private static string Keyed(string kind, int key) => $"{kind}:{key.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>A topic's kind, and its key when it has one: "season:12" is ("season", "12"), "seasons" is ("seasons", null).</summary>
    public static (string Kind, string? Key) Split(string topic)
    {
        var colon = topic.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? (topic, null) : (topic[..colon], topic[(colon + 1)..]);
    }
}

/// <summary>
/// A change to a topic: to everyone watching it, or (<see cref="UserID"/>) to that user's connections alone, whether or not
/// they watch it (<see cref="LiveTopics.MyQueue"/>).
/// </summary>
public sealed record LiveChange(string Topic, int? UserID = null);

/// <summary>
/// Tells the pages watching a topic that it changed, so they load it again: the site's SignalR hub (LiveHub) on a web
/// host, nothing elsewhere. Never waits and never throws: the changes go out shortly after, a burst of them (an import's)
/// as one per topic.
/// </summary>
public interface ILiveUpdates
{
    void Publish(IEnumerable<LiveChange> changes);
}

/// <summary>No pages to tell: a host without the site's hub (the seeder, the converter, tests).</summary>
public sealed class NoLiveUpdates : ILiveUpdates
{
    public void Publish(IEnumerable<LiveChange> changes)
    {
        // Nobody watches from here
    }
}
