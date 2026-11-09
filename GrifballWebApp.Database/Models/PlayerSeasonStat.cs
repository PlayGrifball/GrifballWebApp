#nullable disable
using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A player's totals for one stint of a season (regular season or playoffs), as the season's stats sheet gives them: a
/// small aggregate of its own, a fact of the archive. The historical import writes a season's rows as a batch from its
/// sheet (<see cref="FromSheet"/>, <see cref="Restate"/>) and removes the ones the sheet no longer has; nothing else changes
/// them but a team going (<see cref="RemoveTeam"/>) and a user merge (<see cref="PlayerStatTotals.ReassignUser"/>). Unique
/// per (SeasonID, UserID, StintIndex, IsPlayoffs): a player traded mid-season has a row per team, StintIndex 1, 2, ...
/// </summary>
[AggregateRoot]
public class PlayerSeasonStat : PlayerStatTotals
{
    /// <summary>For EF. A row begins from its sheet (<see cref="FromSheet"/>).</summary>
    private PlayerSeasonStat() { }

    /// <summary>
    /// The sheet's row of <paramref name="userID"/> for <paramref name="seasonID"/>: their <paramref name="stintIndex"/>th
    /// stint (1 is the first; refused, thrown, below it) of the regular season or the playoffs, on
    /// <paramref name="teamID"/> when the sheet names a team. <paramref name="isCombined"/>: the sheet's totals are the
    /// regular season and the playoffs together.
    /// </summary>
    public static PlayerSeasonStat FromSheet(int seasonID, int userID, int gameVersionID, StatSheetTotals totals, int? teamID = null,
        int stintIndex = 1, bool isPlayoffs = false, bool isCombined = false, string tradeNote = null, string roleNote = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stintIndex, 1);
        return Seed(totals, seasonID, userID, gameVersionID, teamID, stintIndex, isPlayoffs, isCombined, tradeNote, roleNote);
    }

    /// <summary>
    /// A row of any kind, for tests, which seed rows of saved entities by their keys or of entities not saved yet (the
    /// navigations), and rows an older model let be (a stint below 1).
    /// </summary>
    internal static PlayerSeasonStat Seed(StatSheetTotals totals, int seasonID = 0, int userID = 0, int gameVersionID = 0, int? teamID = null,
        int stintIndex = 1, bool isPlayoffs = false, bool isCombined = false, string tradeNote = null, string roleNote = null,
        Season season = null, User user = null, Team team = null)
    {
        var row = new PlayerSeasonStat
        {
            SeasonID = season is { SeasonID: not 0 } ? season.SeasonID : seasonID,
            Season = season,
            TeamID = team is { TeamID: not 0 } ? team.TeamID : teamID,
            Team = team,
            StintIndex = stintIndex,
            IsPlayoffs = isPlayoffs,
            IsCombined = isCombined,
            TradeNote = tradeNote,
            RoleNote = roleNote,
        };
        row.Of(userID, user);
        row.Record(gameVersionID, totals);
        return row;
    }

    // The key is internal: the database makes it
    public int PlayerSeasonStatID { get; internal set; }
    public int SeasonID { get; private set; }

    /// <summary>The team the player was on for the stint; null when the sheet names none.</summary>
    public int? TeamID { get; private set; }
    public string TradeNote { get; private set; }
    public string RoleNote { get; private set; }
    /// <summary>Which of the player's stints of the season (1 is the first): never below 1
    /// (CK_Event_PlayerSeasonStats_StintFromOne).</summary>
    public int StintIndex { get; private set; } = 1;

    public bool IsPlayoffs { get; private set; }
    public bool IsCombined { get; private set; }

    public Season Season { get; private set; }
    public Team Team { get; private set; }

    /// <summary>The row is for the same player's same stint of the same season's phase as <paramref name="other"/> (the
    /// unique index's slot).</summary>
    public bool IsSameStintAs(PlayerSeasonStat other) => ClashesWith(other) && UserID == other.UserID;

    /// <summary>The row is for the same stint of the same season's phase as <paramref name="other"/>, whoever's each is: the
    /// unique index's slot but the user. A merge of one's user onto the other's would put both in one slot
    /// (<see cref="ClashesOnMerge"/> for queries).</summary>
    public bool ClashesWith(PlayerSeasonStat other)
        => other is not null && SeasonID == other.SeasonID && StintIndex == other.StintIndex && IsPlayoffs == other.IsPlayoffs;

    /// <summary>
    /// The rows of <paramref name="toUserID"/> a row of <paramref name="fromUserID"/> among <paramref name="rows"/> clashes
    /// with (<see cref="ClashesWith"/>): what a merge of the one onto the other refuses first, as
    /// <see cref="PlayerStatTotals.ReassignUser"/> would break the unique index.
    /// </summary>
    public static Expression<Func<PlayerSeasonStat, bool>> ClashesOnMerge(IQueryable<PlayerSeasonStat> rows, int fromUserID, int toUserID)
        => s => s.UserID == toUserID
            && rows.Any(o => o.UserID == fromUserID && o.SeasonID == s.SeasonID && o.StintIndex == s.StintIndex && o.IsPlayoffs == s.IsPlayoffs);

    /// <summary>
    /// A re-run of the import: the sheet's row for the same stint (<see cref="IsSameStintAs"/>) replaces this one's team,
    /// notes and totals. Only what changed is written (and gets a history row): the same sheet changes nothing. Refused
    /// (thrown) for another stint's row.
    /// </summary>
    public void Restate(PlayerSeasonStat sheet)
    {
        if (!IsSameStintAs(sheet))
            throw new InvalidOperationException("A sheet's row restates the row of the same player, season, stint and phase only");
        // The key alone: EF moves a loaded team navigation to match
        TeamID = sheet.TeamID;
        TradeNote = sheet.TradeNote;
        RoleNote = sheet.RoleNote;
        IsCombined = sheet.IsCombined;
        Record(sheet.GameVersionID, sheet.Totals);
    }

    /// <summary>The stint's team is gone (the archive no longer names it): the row stays, with no team.</summary>
    public void RemoveTeam()
    {
        TeamID = null;
        Team = null;
    }
}
