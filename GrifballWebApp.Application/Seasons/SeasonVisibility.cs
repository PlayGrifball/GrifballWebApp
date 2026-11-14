using System.Linq.Expressions;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Seasons;

/// <summary>
/// Who is looking at seasons. Everyone sees the public ones (<see cref="Season.PublicAsOf"/>: started, or announced);
/// commissioners and sysadmins see the ones not announced yet too, to set them up. A reply others see (a Discord channel's)
/// is <see cref="Public"/> whoever asked.
/// </summary>
public enum SeasonViewer
{
    Public,
    Staff,
}

/// <summary>What a season left unnamed is for, which decides the season it means (<see cref="SeasonService.GetDefaultSeasonID"/>).</summary>
public enum SeasonPurpose
{
    /// <summary>Standings, schedule, stats, grades, the bracket: the running season, else the one that ended last, else the next to start.</summary>
    Current,
    /// <summary>Signups, availability, the draft: the next season to start, else the running one, else the one that ended last.</summary>
    Upcoming,
    /// <summary>Staff's scheduling and seeding: the running season, else the next to start, else the one that ended last.</summary>
    Active,
}

/// <summary>Which seasons a <see cref="SeasonViewer"/> may see, and checks that a season, team or season match isn't hidden from them.</summary>
public class SeasonVisibility
{
    /// <summary>The roles that see seasons not announced yet.</summary>
    public static readonly string[] StaffRoles = [SiteRoles.Commissioner, SiteRoles.Sysadmin];

    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _time;

    public SeasonVisibility(IApplicationDbContext context, TimeProvider? time = null)
    {
        _context = context;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>The viewer a caller with <paramref name="roles"/> is: staff for a commissioner or sysadmin.</summary>
    public static SeasonViewer For(IReadOnlyCollection<string> roles) => roles.Any(StaffRoles.Contains) ? SeasonViewer.Staff : SeasonViewer.Public;

    /// <summary>The viewer a site request's user is (none signed in: the public).</summary>
    public static SeasonViewer For(System.Security.Claims.ClaimsPrincipal? user)
        => user is not null && StaffRoles.Any(user.IsInRole) ? SeasonViewer.Staff : SeasonViewer.Public;

    /// <summary>The seasons <paramref name="viewer"/> may see at <paramref name="now"/>.</summary>
    public static Expression<Func<Season, bool>> VisibleTo(SeasonViewer viewer, DateTime now)
        => viewer is SeasonViewer.Staff ? _ => true : Season.PublicAsOf(now);

    /// <summary>
    /// Whether the season is one <paramref name="viewer"/> may not see. False for one that doesn't exist: the caller's own
    /// lookup says so, as it did.
    /// </summary>
    public Task<bool> IsSeasonHidden(int seasonID, SeasonViewer viewer, CancellationToken ct = default)
        => viewer is SeasonViewer.Staff
            ? Task.FromResult(false)
            : _context.Seasons.Where(s => s.SeasonID == seasonID).AnyAsync(Hidden(), ct);

    /// <summary>Whether the team is of a season <paramref name="viewer"/> may not see (false for no such team).</summary>
    public Task<bool> IsTeamHidden(int teamID, SeasonViewer viewer, CancellationToken ct = default)
        => viewer is SeasonViewer.Staff
            ? Task.FromResult(false)
            : _context.Teams.Where(t => t.TeamID == teamID).Select(t => t.Season).AnyAsync(Hidden(), ct);

    /// <summary>Whether the season match is of a season <paramref name="viewer"/> may not see (false for no such match).</summary>
    public Task<bool> IsSeasonMatchHidden(int seasonMatchID, SeasonViewer viewer, CancellationToken ct = default)
        => viewer is SeasonViewer.Staff
            ? Task.FromResult(false)
            : _context.SeasonMatches.Where(m => m.SeasonMatchID == seasonMatchID).Select(m => m.Season).AnyAsync(Hidden(), ct);

    /// <summary>The seasons anyone may not see now: <see cref="Season.PublicAsOf"/> negated.</summary>
    private Expression<Func<Season, bool>> Hidden()
    {
        var visible = Season.PublicAsOf(_time.GetUtcNow().UtcDateTime);
        return Expression.Lambda<Func<Season, bool>>(Expression.Not(visible.Body), visible.Parameters);
    }
}
