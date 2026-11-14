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

/// <summary>
/// Which seasons a <see cref="SeasonViewer"/> may see, and whether a season, team or season match is one they may: there and
/// visible. A hidden one and one that doesn't exist answer alike, so a caller can't tell them apart by trying IDs.
/// </summary>
public class SeasonVisibility
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _time;

    public SeasonVisibility(IApplicationDbContext context, TimeProvider? time = null)
    {
        _context = context;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>The viewer a caller with <paramref name="roles"/> is: staff for a commissioner or sysadmin (<see cref="SiteRoles.Staff"/>).</summary>
    public static SeasonViewer For(IReadOnlyCollection<string> roles) => SiteRoles.IsInAnyRole(roles, SiteRoles.Staff) ? SeasonViewer.Staff : SeasonViewer.Public;

    /// <summary>The viewer a site request's user is (none signed in: the public).</summary>
    public static SeasonViewer For(System.Security.Claims.ClaimsPrincipal? user)
        => user is not null && SiteRoles.Staff.Any(user.IsInRole) ? SeasonViewer.Staff : SeasonViewer.Public;

    /// <summary>The seasons <paramref name="viewer"/> may see at <paramref name="now"/>.</summary>
    public static Expression<Func<Season, bool>> VisibleTo(SeasonViewer viewer, DateTime now)
        => viewer is SeasonViewer.Staff ? _ => true : Season.PublicAsOf(now);

    /// <summary>Whether the season exists and <paramref name="viewer"/> may see it.</summary>
    public virtual Task<bool> CanSeeSeason(int seasonID, SeasonViewer viewer, CancellationToken ct = default)
        => Visible(viewer).AnyAsync(s => s.SeasonID == seasonID, ct);

    /// <summary>Whether the team exists and is of a season <paramref name="viewer"/> may see.</summary>
    public Task<bool> CanSeeTeam(int teamID, SeasonViewer viewer, CancellationToken ct = default)
    {
        var seasons = Visible(viewer);
        return _context.Teams.AnyAsync(t => t.TeamID == teamID && seasons.Any(s => s.SeasonID == t.SeasonID), ct);
    }

    /// <summary>Whether the season match exists and is of a season <paramref name="viewer"/> may see.</summary>
    public Task<bool> CanSeeSeasonMatch(int seasonMatchID, SeasonViewer viewer, CancellationToken ct = default)
    {
        var seasons = Visible(viewer);
        return _context.SeasonMatches.AnyAsync(m => m.SeasonMatchID == seasonMatchID && seasons.Any(s => s.SeasonID == m.SeasonID), ct);
    }

    /// <summary>The seasons <paramref name="viewer"/> may see now.</summary>
    public IQueryable<Season> Visible(SeasonViewer viewer) => _context.Seasons.Where(VisibleTo(viewer, _time.GetUtcNow().UtcDateTime));
}
