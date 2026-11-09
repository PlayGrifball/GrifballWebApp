#nullable disable

namespace GrifballWebApp.Domain.Models;

/// <summary>A player on a championship-winning roster: one row per (season, tier, player). Part of its
/// <see cref="Models.Season"/>, which records them: when a season's playoff bracket is decided in the app
/// (<see cref="Season.RecordChampions"/>), and by the historical import (<see cref="Season.RecordArchivedChampions"/>).
/// Read through SeasonQueries.Championships.</summary>
[AggregatePart(typeof(Season))]
public class PlayerChampionship : AuditableEntity
{
    /// <summary>The longest a gamertag on a title can be (its column's length): a longer one is cut.</summary>
    public const int GamertagUsedMaxLength = 50;

    /// <summary>For EF. A title is won through its season.</summary>
    private PlayerChampionship() { }

    /// <summary>A title of <paramref name="season"/>'s <paramref name="tier"/> for <paramref name="userID"/>; its team and the
    /// rest come next (<see cref="HeldBy"/>).</summary>
    internal static PlayerChampionship Won(Season season, string tier, int userID) => new()
    {
        Season = season,
        SeasonID = season.SeasonID,
        Tier = tier,
        UserID = userID,
    };

    /// <summary>A title of saved rows by their keys, or of entities not saved yet (the navigations): for tests, which seed
    /// titles as the archive has them.</summary>
    internal static PlayerChampionship Seed(int seasonID = 0, int teamID = 0, int userID = 0, string tier = null, string gamertagUsed = null,
        bool isCaptain = false, int? gameVersionID = null, Season season = null, Team team = null, User user = null) => new()
    {
        SeasonID = seasonID,
        Season = season,
        TeamID = teamID,
        Team = team,
        UserID = userID,
        User = user,
        Tier = tier,
        GamertagUsed = gamertagUsed,
        IsCaptain = isCaptain,
        GameVersionID = gameVersionID,
    };

    // The key is internal: the database makes it
    public int PlayerChampionshipID { get; internal set; }
    public int UserID { get; private set; }
    public int SeasonID { get; private set; }
    /// <summary>The game the title was won on, which can differ from the season's (a season with Halo 3 and Reach
    /// tiers). Null when the record names none the app knows (e.g. "MCC Split").</summary>
    public int? GameVersionID { get; private set; }

    /// <summary>The division the title was won in, e.g. Pro, Amateur, Open: its season's <see cref="Season.ChampionTier"/>
    /// for a season of the app's, the champions record's tier for an imported one. Kept on the title because one archive
    /// season can have titles in more than one tier (its divisions shared a season name and schedule); the league is the
    /// season's.</summary>
    public string Tier { get; private set; }
    /// <summary>The team that won the title.</summary>
    public int TeamID { get; private set; }
    public bool IsCaptain { get; private set; }
    /// <summary>The gamertag the record lists the player under.</summary>
    public string GamertagUsed { get; private set; }

    public User User { get; private set; }
    public Season Season { get; private set; }
    public Team Team { get; private set; }
    public GameVersion GameVersion { get; private set; }

    /// <summary>The title is held by <paramref name="teamID"/>'s roster, the player under <paramref name="gamertag"/> (cut to
    /// <see cref="GamertagUsedMaxLength"/>), its captain or not.</summary>
    internal void HeldBy(int teamID, int? gameVersionID, bool isCaptain, string gamertag)
    {
        TeamID = teamID;
        GameVersionID = gameVersionID;
        IsCaptain = isCaptain;
        GamertagUsed = gamertag.Length > GamertagUsedMaxLength ? gamertag[..GamertagUsedMaxLength] : gamertag;
    }

    /// <summary>A merge's: the title is another user's now (<see cref="Season.MergeTitles"/>).</summary>
    internal void ReassignUser(int userID) => UserID = userID;

    /// <summary>A merge's: the same title of the merged user goes, and its captaincy is kept on this one.</summary>
    internal void KeepCaptaincyOf(PlayerChampionship merged) => IsCaptain |= merged.IsCaptain;
}

/// <summary>A player on a championship-winning roster, as the title records them.</summary>
public record ChampionMember(int UserID, string Gamertag, bool IsCaptain);

/// <summary>What recording a tier's champions changed (<see cref="Season.RecordChampions"/>).</summary>
public record ChampionshipChanges(int Inserted, int Updated, int Removed);
