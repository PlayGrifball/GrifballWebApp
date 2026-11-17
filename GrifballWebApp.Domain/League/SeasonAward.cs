using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
#nullable disable

namespace GrifballWebApp.Domain.League;

/// <summary>
/// An award of a season (its MVP, a Best Runner, a single-game record) and one of its winners: a player, a team, or a
/// player of a team. An award with several winners (a Best Duo, a top 5) is a row per winner. Part of its
/// <see cref="Season"/>, which records them from the historical archive (<see cref="Season.RecordArchivedAwards"/>).
/// Unique per (SeasonID, Name, WinnerName).
/// </summary>
[AggregatePart(typeof(Season))]
public class SeasonAward : AuditableEntity
{
    /// <summary>The longest an award's name can be (its column's length): a longer one is cut.</summary>
    public const int NameMaxLength = 100;
    /// <summary>The longest a winner's name can be: a gamertag or a team's name.</summary>
    public const int WinnerNameMaxLength = 100;
    /// <summary>The longest an award's value can be (a record's "134", a ranked list's "#2").</summary>
    public const int ValueMaxLength = 50;

    /// <summary>For EF. An award is given through its season.</summary>
    private SeasonAward() { }

    /// <summary><paramref name="season"/>'s award <paramref name="name"/> for <paramref name="winnerName"/>; who that is and the
    /// rest come next (<see cref="WonBy"/>).</summary>
    internal static SeasonAward Given(Season season, string name, string winnerName) => new()
    {
        Season = season,
        SeasonID = season.SeasonID,
        Name = Cut(name, NameMaxLength),
        WinnerName = Cut(winnerName, WinnerNameMaxLength),
    };

    // The key is internal: the database makes it
    public int SeasonAwardID { get; internal set; }
    public int SeasonID { get; private set; }
    /// <summary>The award's name, as its source gives it: "MVP", "Finals MVP", "Most Kills (one game)".</summary>
    public string Name { get; private set; }
    /// <summary>The winner as the source names them: the gamertag used, or the team's name for a team's award.</summary>
    public string WinnerName { get; private set; }
    /// <summary>The player who won it; null for a team's award.</summary>
    public int? UserID { get; private set; }
    /// <summary>The team that won it, or the winner's team when the source names it; null when it names none. One of the
    /// season's.</summary>
    public int? TeamID { get; private set; }
    /// <summary>What the award is worth when the source gives it: a record's value, a ranked list's place.</summary>
    public string Value { get; private set; }
    /// <summary>Where the award comes in the season's list of awards (from 1), as its source lists them: the page's order.</summary>
    public int Position { get; private set; }

    public Season Season { get; private set; }
    public User User { get; private set; }
    public Team Team { get; private set; }

    /// <summary>The award is <paramref name="name"/>'s for <paramref name="winnerName"/>, compared as the unique index does
    /// (case-insensitive, as cut to the columns).</summary>
    public bool IsFor(string name, string winnerName)
        => string.Equals(Name, Cut(name, NameMaxLength), StringComparison.OrdinalIgnoreCase)
            && string.Equals(WinnerName, Cut(winnerName, WinnerNameMaxLength), StringComparison.OrdinalIgnoreCase);

    /// <summary>Who won it, what it is worth and where it is listed; only what changed is written.</summary>
    internal bool WonBy(int? userID, int? teamID, string value, int position)
    {
        value = string.IsNullOrWhiteSpace(value) ? null : Cut(value, ValueMaxLength);
        if (UserID == userID && TeamID == teamID && Value == value && Position == position)
            return false;
        UserID = userID;
        TeamID = teamID;
        Value = value;
        Position = position;
        return true;
    }

    /// <summary>The team the player's award names is gone (<see cref="Season.RemoveArchivedAwardsOf"/>).</summary>
    internal void ClearTeam() => TeamID = null;

    /// <summary>A merge's: the award is another user's now (<see cref="Season.MergeAwards"/>).</summary>
    internal void ReassignUser(int userID) => UserID = userID;

    private static string Cut(string text, int length)
    {
        var trimmed = text.Trim();
        return trimmed.Length > length ? trimmed[..length].TrimEnd() : trimmed;
    }
}

/// <summary>A winner of a season's award, as the archive gives it (<see cref="Season.RecordArchivedAwards"/>): the award's
/// name, the winner's name as listed, the player (null for a team's award), the team (one of the season's, or null) and
/// the award's value.</summary>
public record SeasonAwardWinner(string Name, string WinnerName, int? UserID, Team Team, string Value = null);

/// <summary>What recording a season's awards changed (<see cref="Season.RecordArchivedAwards"/>).</summary>
public record AwardChanges(int Inserted, int Updated, int Removed);
