using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Queries;

/// <summary>
/// How a <see cref="Season"/>, its titles (<see cref="PlayerChampionship"/>) and its timeslots (<see cref="SeasonAvailability"/>)
/// are read. The titles and timeslots are changed only through the season; the readers (leaderboards, profiles, signups,
/// the merge's checks) read them untracked through <see cref="Championships"/> and <see cref="Timeslots"/>. A season whose
/// titles weren't loaded finds none of them, so every title method of the season gets a season loaded through
/// <see cref="WithChampionships"/>.
/// </summary>
public static class SeasonQueries
{
    /// <summary>The season with its titles: all its title methods need (<see cref="Season.RecordChampions"/>, <see cref="Season.MergeTitles"/>).</summary>
    public static IQueryable<Season> WithChampionships(this IQueryable<Season> seasons) => seasons.Include(s => s.PlayerChampionships);

    /// <summary>Every season's titles, to read: <c>context.Seasons.Championships()</c>.</summary>
    public static IQueryable<PlayerChampionship> Championships(this IQueryable<Season> seasons) => seasons.SelectMany(s => s.PlayerChampionships).AsNoTracking();

    /// <summary>Every season's timeslots, to read: <c>context.Seasons.Timeslots()</c>.</summary>
    public static IQueryable<SeasonAvailability> Timeslots(this IQueryable<Season> seasons) => seasons.SelectMany(s => s.SeasonAvailability).AsNoTracking();
}
