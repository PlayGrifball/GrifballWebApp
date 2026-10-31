using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Historical;

public static class GamertagHistoryExtensions
{
    /// <summary>
    /// Records that the Xbox account had the gamertag between <paramref name="firstSeen"/> and <paramref name="lastSeen"/>
    /// (UTC, null when unknown), widening the dates of a row it already has for that gamertag. Not saved.
    /// </summary>
    public static async Task<XboxGamertagHistory> UpsertGamertagHistoryAsync(this GrifballContext context, long xboxUserId, string gamertag,
        DateTime? firstSeen, DateTime? lastSeen, GamertagHistorySource source, CancellationToken ct = default)
    {
        var clean = HistoricalKeys.CleanGamertag(gamertag);
        var normalized = HistoricalKeys.Normalize(clean);
        var row = context.XboxGamertagHistory.Local.FirstOrDefault(h => h.XboxUserID == xboxUserId && h.NormalizedGamertag == normalized)
            ?? await context.XboxGamertagHistory.FirstOrDefaultAsync(h => h.XboxUserID == xboxUserId && h.NormalizedGamertag == normalized, ct);
        if (row is null)
        {
            row = new XboxGamertagHistory { XboxUserID = xboxUserId, Gamertag = clean, NormalizedGamertag = normalized, FirstSeen = firstSeen, LastSeen = lastSeen, Source = source };
            context.XboxGamertagHistory.Add(row);
            return row;
        }
        row.FirstSeen = Earliest(row.FirstSeen, firstSeen);
        row.LastSeen = Latest(row.LastSeen, lastSeen);
        return row;
    }

    private static DateTime? Earliest(DateTime? a, DateTime? b) => a is null ? b : b is null ? a : (a < b ? a : b);
    private static DateTime? Latest(DateTime? a, DateTime? b) => a is null ? b : b is null ? a : (a > b ? a : b);
}
