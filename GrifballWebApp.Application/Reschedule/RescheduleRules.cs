using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Queries;

namespace GrifballWebApp.Application.Reschedule;

/// <summary>
/// Which matches can be rescheduled, and who may ask: used by <see cref="RescheduleService"/> to refuse a request and
/// by the match page to show the Reschedule button only where a request would be accepted. Which matches can be
/// rescheduled at all is the request's own rule (<see cref="MatchReschedule.WhyNotReschedulable"/>).
/// </summary>
public static class RescheduleRules
{
    /// <summary>The roles that may request a reschedule of any match (commissioners, and sysadmins as everywhere else).</summary>
    public static readonly string[] OrganizerRoles = ["Commissioner", "Sysadmin"];

    /// <summary>Whether the user is on either team's roster (captains included) or is a commissioner.</summary>
    public static async Task<bool> MayRequestAsync(IApplicationDbContext context, SeasonMatch seasonMatch, int userID, CancellationToken ct = default)
    {
        var onRoster = await context.Teams.Rosters().AnyAsync(tp => tp.UserID == userID
            && (tp.TeamID == seasonMatch.HomeTeamID || tp.TeamID == seasonMatch.AwayTeamID), ct);
        if (onRoster)
            return true;

        return await context.UserRoles.AnyAsync(ur => ur.UserId == userID && OrganizerRoles.Contains(ur.Role.Name!), ct);
    }

    /// <summary>Whether this user could request a reschedule of this match now (no request already active).</summary>
    public static async Task<bool> CanRequestAsync(IApplicationDbContext context, SeasonMatch seasonMatch, int? userID, CancellationToken ct = default)
    {
        if (userID is null || MatchReschedule.WhyNotReschedulable(seasonMatch) is not null)
            return false;
        if (await context.MatchReschedules.Where(MatchReschedule.IsPending).AnyAsync(r => r.SeasonMatchID == seasonMatch.SeasonMatchID, ct))
            return false;
        return await MayRequestAsync(context, seasonMatch, userID.Value, ct);
    }
}
