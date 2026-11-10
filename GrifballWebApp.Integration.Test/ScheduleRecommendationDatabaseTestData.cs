using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using static GrifballWebApp.Domain.Test.ScheduleRecommendationTestData;

namespace GrifballWebApp.Integration.Test;

/// <summary>A season saved for a recommendation to schedule.</summary>
internal static class ScheduleRecommendationDatabaseTestData
{
    internal static async Task<Season> AddSeason(GrifballContext context)
    {
        var season = Season.Seed(seasonName: "S1").WithDates(seasonStart: At, seasonEnd: At.AddDays(28));
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        return season;
    }
}
