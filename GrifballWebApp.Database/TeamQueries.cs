using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Database;

/// <summary>
/// How a <see cref="Team"/>'s parts are read. Its roster (<see cref="TeamPlayer"/>) is changed only through the team; the
/// readers (the draft board, stats, rosters of a series, the merge's checks) read it here, untracked. A team whose roster
/// wasn't loaded finds none of it, so every roster method of the team gets a team loaded through <see cref="WithRoster"/>.
/// </summary>
public static class TeamQueries
{
    /// <summary>Every team's roster rows, to read.</summary>
    public static IQueryable<TeamPlayer> Rosters(this GrifballContext context) => context.TeamPlayers.AsNoTracking();

    /// <summary>The teams with their rosters: all their roster methods need (<see cref="Team.Draft"/>, <see cref="Team.SetCaptain"/>, ...).</summary>
    public static IQueryable<Team> WithRoster(this IQueryable<Team> teams) => teams.Include(t => t.TeamPlayers);
}
