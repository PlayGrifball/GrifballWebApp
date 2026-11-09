using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Domain;

/// <summary>
/// How a <see cref="Team"/> is loaded. Its roster (<see cref="TeamPlayer"/>) is changed only through the team; the
/// readers (the draft board, stats, rosters of a series, the merge's checks) read it untracked, through the context's
/// <c>Rosters()</c> (AggregatePartQueries, in GrifballWebApp.Database). A team whose roster wasn't loaded finds none of it,
/// so every roster method of the team gets a team loaded through <see cref="WithRoster"/>.
/// </summary>
public static class TeamQueries
{
    /// <summary>The teams with their rosters: all their roster methods need (<see cref="Team.Draft"/>, <see cref="Team.SetCaptain"/>, ...).</summary>
    public static IQueryable<Team> WithRoster(this IQueryable<Team> teams) => teams.Include(t => t.TeamPlayers);
}
