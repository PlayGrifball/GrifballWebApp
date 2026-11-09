using GrifballWebApp.Database;
using GrifballWebApp.Domain.Models;

namespace GrifballWebApp.Test;

/// <summary>A test's season with its dates, set through the season's own methods (its dates have private setters).</summary>
internal static class SeasonTestExtensions
{
    /// <summary>
    /// The season scheduled with these dates (<see cref="Season.Schedule"/>'s order, imported seasons too), each left as it is when not given, and
    /// announced at <paramref name="publicAt"/> when given. Dates in an order that can't happen fail the test.
    /// </summary>
    public static Season WithDates(this Season season, DateTime? signupsOpen = null, DateTime? signupsClose = null, DateTime? draftStart = null,
        DateTime? seasonStart = null, DateTime? seasonEnd = null, DateTime? publicAt = null)
    {
        var result = season.ScheduleSeeded(signupsOpen ?? season.SignupsOpen, signupsClose ?? season.SignupsClose, draftStart ?? season.DraftStart,
            seasonStart ?? season.SeasonStart, seasonEnd ?? season.SeasonEnd);
        if (result is not SeasonScheduleResult.Scheduled)
            throw new InvalidOperationException($"Test season {season.SeasonName}: {result}");
        if (publicAt is not null)
            season.AnnounceAt(publicAt);
        return season;
    }

    /// <summary>
    /// The season with these fields changed, each left as it is when not given, through its own methods: an organizer's
    /// edit (<see cref="Season.Edit"/>), or the import's update of an imported season (<see cref="Season.UpdateArchived"/>).
    /// </summary>
    public static Season Edited(this Season season, string? seasonName = null, int? leagueID = null, int? gameVersionID = null, string? division = null,
        SeasonFormat? format = null)
    {
        if (season.IsImported)
            season.UpdateArchived(seasonName ?? season.SeasonName, leagueID ?? season.LeagueID, gameVersionID ?? season.GameVersionID, division ?? season.Division,
                season.HistoricalSourceUrl, format ?? season.Format);
        else if (season.Edit(seasonName ?? season.SeasonName, leagueID ?? season.LeagueID, gameVersionID ?? season.GameVersionID, division ?? season.Division,
                format ?? season.Format) is not SeasonEditResult.Edited)
            throw new InvalidOperationException($"Test season {season.SeasonName}: not edited");
        return season;
    }

    /// <summary>The season's division set, or cleared (null), as <see cref="Edited"/> does.</summary>
    public static Season WithDivision(this Season season, string? division)
    {
        if (division is not null)
            return season.Edited(division: division);
        season.Edit(season.SeasonName, season.LeagueID, season.GameVersionID, null, season.Format);
        return season;
    }

    /// <summary>
    /// A season already tracked made an imported one, as no method makes an app season one: its key set past its methods,
    /// as a test of a constraint does.
    /// </summary>
    public static Season MarkImported(this GrifballContext context, Season season, string historicalSourceID)
    {
        context.Entry(season).Property(s => s.HistoricalSourceID).CurrentValue = historicalSourceID;
        return season;
    }
}
