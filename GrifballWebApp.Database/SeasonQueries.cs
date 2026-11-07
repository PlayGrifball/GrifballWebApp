using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Database;

/// <summary>
/// How a <see cref="Season"/>'s parts are read. Its titles (<see cref="PlayerChampionship"/>) and timeslots
/// (<see cref="SeasonAvailability"/>) are changed only through the season; the readers (leaderboards, profiles, signups,
/// the merge's checks) read them here, untracked. A season whose titles weren't loaded finds none of them, so every title
/// method of the season gets a season loaded through <see cref="WithChampionships"/>.
/// </summary>
public static class SeasonQueries
{
    /// <summary>Every season's titles, to read.</summary>
    public static IQueryable<PlayerChampionship> Championships(this GrifballContext context) => context.PlayerChampionships.AsNoTracking();

    /// <summary>Every season's timeslots, to read.</summary>
    public static IQueryable<SeasonAvailability> SeasonTimeslots(this GrifballContext context) => context.SeasonAvailability.AsNoTracking();

    /// <summary>The season with its titles: all its title methods need (<see cref="Season.RecordChampions"/>, <see cref="Season.MergeTitles"/>).</summary>
    public static IQueryable<Season> WithChampionships(this IQueryable<Season> seasons) => seasons.Include(s => s.PlayerChampionships);
}
