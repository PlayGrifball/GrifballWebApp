using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Database.Queries;

/// <summary>
/// How a <see cref="Team"/> and its roster (<see cref="TeamPlayer"/>) are read. The roster is changed only through the
/// team; the readers (the draft board, stats, rosters of a series, the merge's checks) read it untracked through
/// <see cref="Rosters"/>. A team whose roster wasn't loaded finds none of it, so every roster method of the team gets a
/// team loaded through <see cref="WithRoster"/>.
/// </summary>
public static class TeamQueries
{
    /// <summary>The teams with their rosters: all their roster methods need (<see cref="Team.Draft"/>, <see cref="Team.SetCaptain"/>, ...).</summary>
    public static IQueryable<Team> WithRoster(this IQueryable<Team> teams) => teams.Include(t => t.TeamPlayers);

    /// <summary>Every team's roster rows, to read: <c>context.Teams.Rosters()</c>.</summary>
    public static IQueryable<TeamPlayer> Rosters(this IQueryable<Team> teams) => teams.SelectMany(t => t.TeamPlayers).AsNoTracking();
}
