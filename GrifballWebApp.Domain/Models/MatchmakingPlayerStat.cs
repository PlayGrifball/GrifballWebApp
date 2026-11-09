#nullable disable
using System.Linq.Expressions;

namespace GrifballWebApp.Domain.Models;

/// <summary>
/// A player's totals for a matchmaking season, as its stats sheet gives them: one row per (MatchmakingSeasonID, UserID),
/// a small aggregate of its own. The historical import writes a season's rows as a batch from its sheet
/// (<see cref="FromSheet"/>, <see cref="Restate"/>) and removes the ones the sheet no longer has; nothing else changes them
/// but a user merge (<see cref="PlayerStatTotals.ReassignUser"/>). A player with games of the season in the app
/// (<see cref="MatchedMatch"/>) has their totals from those instead.
/// </summary>
[AggregateRoot]
public class MatchmakingPlayerStat : PlayerStatTotals
{
    /// <summary>For EF. A row begins from its sheet (<see cref="FromSheet"/>).</summary>
    private MatchmakingPlayerStat() { }

    /// <summary>The sheet's row of <paramref name="userID"/> for <paramref name="matchmakingSeasonID"/>.</summary>
    public static MatchmakingPlayerStat FromSheet(int matchmakingSeasonID, int userID, int gameVersionID, StatSheetTotals totals)
        => Seed(totals, matchmakingSeasonID, userID, gameVersionID);

    /// <summary>A row of saved entities by their keys, or of entities not saved yet (the navigations): for tests.</summary>
    internal static MatchmakingPlayerStat Seed(StatSheetTotals totals, int matchmakingSeasonID = 0, int userID = 0, int gameVersionID = 0,
        MatchmakingSeason matchmakingSeason = null, User user = null)
    {
        var row = new MatchmakingPlayerStat
        {
            MatchmakingSeasonID = matchmakingSeason is { MatchmakingSeasonID: not 0 } ? matchmakingSeason.MatchmakingSeasonID : matchmakingSeasonID,
            MatchmakingSeason = matchmakingSeason,
        };
        row.Of(userID, user);
        row.Record(gameVersionID, totals);
        return row;
    }

    // The key is internal: the database makes it
    public int MatchmakingPlayerStatID { get; internal set; }
    public int MatchmakingSeasonID { get; private set; }

    public MatchmakingSeason MatchmakingSeason { get; private set; }

    /// <summary>The row is the same player's of the same season as <paramref name="other"/> (the unique index's slot).</summary>
    public bool IsSameSlotAs(MatchmakingPlayerStat other) => ClashesWith(other) && UserID == other.UserID;

    /// <summary>The row is for the same season as <paramref name="other"/>, whoever's each is: the unique index's slot but
    /// the user. A merge of one's user onto the other's would put both in one slot (<see cref="ClashesOnMerge"/> for queries).</summary>
    public bool ClashesWith(MatchmakingPlayerStat other) => other is not null && MatchmakingSeasonID == other.MatchmakingSeasonID;

    /// <summary>
    /// The rows of <paramref name="toUserID"/> a row of <paramref name="fromUserID"/> among <paramref name="rows"/> clashes
    /// with (<see cref="ClashesWith"/>): what a merge of the one onto the other refuses first, as
    /// <see cref="PlayerStatTotals.ReassignUser"/> would break the unique index.
    /// </summary>
    public static Expression<Func<MatchmakingPlayerStat, bool>> ClashesOnMerge(IQueryable<MatchmakingPlayerStat> rows, int fromUserID, int toUserID)
        => s => s.UserID == toUserID && rows.Any(o => o.UserID == fromUserID && o.MatchmakingSeasonID == s.MatchmakingSeasonID);

    /// <summary>
    /// A re-run of the import: the sheet's row for the same player (<see cref="IsSameSlotAs"/>) replaces this one's
    /// totals. Only what changed is written: the same sheet changes nothing. Refused (thrown) for another slot's row.
    /// </summary>
    public void Restate(MatchmakingPlayerStat sheet)
    {
        if (!IsSameSlotAs(sheet))
            throw new InvalidOperationException("A sheet's row restates the row of the same player and season only");
        Record(sheet.GameVersionID, sheet.Totals);
    }
}
