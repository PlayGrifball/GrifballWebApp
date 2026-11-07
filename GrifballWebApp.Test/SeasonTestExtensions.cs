using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Test;

/// <summary>A test's season with its dates, set through the season's own methods (its dates have private setters).</summary>
internal static class SeasonTestExtensions
{
    /// <summary>
    /// The season scheduled with these dates (<see cref="Season.Schedule"/>), each left as it is when not given, and
    /// announced at <paramref name="publicAt"/> when given. Dates in an order that can't happen fail the test.
    /// </summary>
    public static Season WithDates(this Season season, DateTime? signupsOpen = null, DateTime? signupsClose = null, DateTime? draftStart = null,
        DateTime? seasonStart = null, DateTime? seasonEnd = null, DateTime? publicAt = null)
    {
        var result = season.Schedule(signupsOpen ?? season.SignupsOpen, signupsClose ?? season.SignupsClose, draftStart ?? season.DraftStart,
            seasonStart ?? season.SeasonStart, seasonEnd ?? season.SeasonEnd);
        if (result is not SeasonScheduleResult.Scheduled)
            throw new InvalidOperationException($"Test season {season.SeasonName}: {result}");
        if (publicAt is not null)
            season.AnnounceAt(publicAt);
        return season;
    }
}
