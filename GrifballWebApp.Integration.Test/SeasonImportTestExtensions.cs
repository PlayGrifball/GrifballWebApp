using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Integration.Test;

/// <summary>A test season changed past its methods, in the database's context.</summary>
internal static class SeasonImportTestExtensions
{
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
