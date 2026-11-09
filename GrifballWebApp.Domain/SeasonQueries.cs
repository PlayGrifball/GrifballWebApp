using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Domain;

/// <summary>
/// How a <see cref="Season"/> is loaded. Its titles (<see cref="PlayerChampionship"/>) and timeslots
/// (<see cref="SeasonAvailability"/>) are changed only through the season; the readers (leaderboards, profiles, signups,
/// the merge's checks) read them untracked, through the context's <c>Championships()</c> and <c>SeasonTimeslots()</c>
/// (AggregatePartQueries, in GrifballWebApp.Database). A season whose titles weren't loaded finds none of them, so every
/// title method of the season gets a season loaded through <see cref="WithChampionships"/>.
/// </summary>
public static class SeasonQueries
{
    /// <summary>The season with its titles: all its title methods need (<see cref="Season.RecordChampions"/>, <see cref="Season.MergeTitles"/>).</summary>
    public static IQueryable<Season> WithChampionships(this IQueryable<Season> seasons) => seasons.Include(s => s.PlayerChampionships);
}
