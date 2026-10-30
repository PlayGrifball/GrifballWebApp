using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Reschedule;

/// <summary>
/// Which matches can be rescheduled, and who may ask: used by <see cref="RescheduleService"/> to refuse a request and
/// by the match page to show the Reschedule button only where a request would be accepted.
/// </summary>
public static class RescheduleRules
{
    /// <summary>The roles that may request a reschedule of any match (commissioners, and sysadmins as everywhere else).</summary>
    public static readonly string[] OrganizerRoles = ["Commissioner", "Sysadmin"];

    /// <summary>
    /// Why this match cannot be rescheduled, or null when it can: it needs both teams (a bracket match waiting on an
    /// earlier result has none yet) and no result (a decided match, a forfeit or a BYE).
    /// </summary>
    public static string? WhyNotReschedulable(SeasonMatch seasonMatch)
    {
        if (seasonMatch.HomeTeamID is null || seasonMatch.AwayTeamID is null)
            return "This match does not have both teams yet.";
        if (seasonMatch.HomeTeamResult is not null || seasonMatch.AwayTeamResult is not null)
            return "This match already has a result.";
        return null;
    }

    /// <summary>Whether the user is on either team's roster (captains included) or is a commissioner.</summary>
    public static async Task<bool> MayRequestAsync(GrifballContext context, SeasonMatch seasonMatch, int userID, CancellationToken ct = default)
    {
        var onRoster = await context.TeamPlayers.AnyAsync(tp => tp.UserID == userID
            && (tp.TeamID == seasonMatch.HomeTeamID || tp.TeamID == seasonMatch.AwayTeamID), ct);
        if (onRoster)
            return true;

        return await context.UserRoles.AnyAsync(ur => ur.UserId == userID && OrganizerRoles.Contains(ur.Role.Name!), ct);
    }

    /// <summary>Whether this user could request a reschedule of this match now (no request already active).</summary>
    public static async Task<bool> CanRequestAsync(GrifballContext context, SeasonMatch seasonMatch, int? userID, CancellationToken ct = default)
    {
        if (userID is null || seasonMatch.ActiveRescheduleRequestId is not null || WhyNotReschedulable(seasonMatch) is not null)
            return false;
        return await MayRequestAsync(context, seasonMatch, userID.Value, ct);
    }
}
