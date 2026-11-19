using System.Globalization;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Seasons;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Live;

/// <summary>
/// Who may watch a topic (<see cref="LiveTopics"/>): what they may see. A season's topics, and its matches' and teams', only for one
/// they may see (one not announced yet: staff alone, #435), so a change never tells the public a hidden season exists; the
/// staff's topics only for staff. A topic not spelt as the app spells it is no topic. The rest carry no data that isn't
/// public (a notice, or the queue's size; the home page's events are built per viewer), so they are anyone's.
/// </summary>
public class LiveTopicAccess(SeasonVisibility visibility, IApplicationDbContext context)
{
    private static readonly HashSet<string> Public = [LiveTopics.Seasons, LiveTopics.LateLeague, LiveTopics.Queue];

    private static readonly HashSet<string> Staff = [LiveTopics.StaffUsers, LiveTopics.StaffClaims, LiveTopics.StaffMerges,
        LiveTopics.StaffImports, LiveTopics.StaffVods, LiveTopics.StaffReschedules];

    /// <summary>Whether a user with <paramref name="roles"/> (none: signed out) may watch <paramref name="topic"/>.</summary>
    public virtual async Task<bool> MayWatch(string topic, IReadOnlyCollection<string> roles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(topic) || topic.Length > LiveTopics.MaxLength)
            return false;
        if (Public.Contains(topic))
            return true;
        if (Staff.Contains(topic))
            return SiteRoles.IsInAnyRole(roles, SiteRoles.Staff);

        var viewer = SeasonVisibility.For(roles);
        var (kind, key) = LiveTopics.Split(topic);
        switch (kind)
        {
            case LiveTopics.SeasonKind or LiveTopics.BracketKind or LiveTopics.TeamsKind or LiveTopics.DraftKind or LiveTopics.SignupsKind:
                return IntKey(key) is int seasonID && topic == Keyed(kind, seasonID) && await visibility.CanSeeSeason(seasonID, viewer, ct);
            case LiveTopics.SeasonMatchKind:
                if (IntKey(key) is not int seasonMatchID || topic != LiveTopics.SeasonMatch(seasonMatchID))
                    return false;
                var seasonOf = await context.SeasonMatches.AsNoTracking()
                    .Where(m => m.SeasonMatchID == seasonMatchID)
                    .Select(m => (int?)m.SeasonID)
                    .FirstOrDefaultAsync(ct);
                return seasonOf is int matchSeasonID && await visibility.CanSeeSeason(matchSeasonID, viewer, ct);
            case LiveTopics.TeamKind:
                if (IntKey(key) is not int teamID || topic != LiveTopics.Team(teamID))
                    return false;
                var teamSeason = await context.Teams.AsNoTracking()
                    .Where(t => t.TeamID == teamID)
                    .Select(t => (int?)t.SeasonID)
                    .FirstOrDefaultAsync(ct);
                return teamSeason is int teamSeasonID && await visibility.CanSeeSeason(teamSeasonID, viewer, ct);
            case LiveTopics.PlayerKind:
                return IntKey(key) is int userID && topic == LiveTopics.Player(userID);
            case LiveTopics.XboxKind:
                return long.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var xboxUserID) && topic == LiveTopics.Xbox(xboxUserID);
            case LiveTopics.HaloMatchKind:
                return Guid.TryParseExact(key, "D", out var matchID) && topic == LiveTopics.HaloMatch(matchID);
            default:
                return false;
        }
    }

    private static int? IntKey(string? key)
        => int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    private static string Keyed(string kind, int seasonID) => kind switch
    {
        LiveTopics.SeasonKind => LiveTopics.Season(seasonID),
        LiveTopics.BracketKind => LiveTopics.Bracket(seasonID),
        LiveTopics.TeamsKind => LiveTopics.Teams(seasonID),
        LiveTopics.DraftKind => LiveTopics.Draft(seasonID),
        _ => LiveTopics.Signups(seasonID),
    };
}
