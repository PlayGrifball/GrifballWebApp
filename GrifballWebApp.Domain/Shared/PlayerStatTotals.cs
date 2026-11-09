using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
#nullable disable

namespace GrifballWebApp.Domain.Shared;

/// <summary>A player's stored totals over a run of games, as a stats sheet gives them: the columns a season's
/// (<see cref="PlayerSeasonStat"/>) and a matchmaking season's (<see cref="MatchmakingPlayerStat"/>) rows share. A sheet's
/// row is a fact of the archive: it is written whole from its sheet (<see cref="Record"/>), and after that only a merge
/// moves it to another user (<see cref="ReassignUser"/>).</summary>
public abstract class PlayerStatTotals : AuditableEntity
{
    public int UserID { get; private set; }
    public int GameVersionID { get; private set; }

    public string Gamertag { get; private set; }

    // Core Metrics
    public int GamesPlayed { get; private set; }
    public int Wins { get; private set; }
    public int Losses { get; private set; }
    public decimal? WinPercent { get; private set; }
    public int Goals { get; private set; }
    public int Kills { get; private set; }
    public int Deaths { get; private set; }
    public int Assists { get; private set; }
    public decimal? KdRatio { get; private set; }
    public int? KdSpread { get; private set; }
    public int CarrierKills { get; private set; }

    // Durations & Averages
    public decimal? AverageLifeSeconds { get; private set; }
    public int? TotalSecondsPlayed { get; private set; }

    // Universal Medals
    public int Sprees { get; private set; }
    public int MultiKills { get; private set; }
    public int Exterminations { get; private set; }
    public int Betrayals { get; private set; }

    // Weapons
    public int? HammerKills { get; private set; }
    public int? SwordKills { get; private set; }
    public int? BallKills { get; private set; }
    public int? Punches { get; private set; }

    // Infinite
    public int? Score { get; private set; }
    public int? DamageDealt { get; private set; }
    public int? DamageTaken { get; private set; }

    // Game-specific metrics (JSON serialized)
    public string ExtendedMetricsJson { get; private set; }

    public User User { get; private set; }
    public GameVersion GameVersion { get; private set; }

    /// <summary>The row's player, as the sheet names them.</summary>
    protected void Of(int userID, User user = null)
    {
        UserID = user is { Id: not 0 } ? user.Id : userID;
        User = user;
    }

    /// <summary>The sheet's totals for the row, on <paramref name="gameVersionID"/>: all of them, the sheet's row replacing
    /// what was there. Refused (thrown) without a gamertag, which the sheet's row always has.</summary>
    protected void Record(int gameVersionID, StatSheetTotals totals)
    {
        ArgumentNullException.ThrowIfNull(totals);
        ArgumentException.ThrowIfNullOrWhiteSpace(totals.Gamertag, nameof(totals));
        GameVersionID = gameVersionID;
        Gamertag = totals.Gamertag;
        GamesPlayed = totals.GamesPlayed;
        Wins = totals.Wins;
        Losses = totals.Losses;
        WinPercent = totals.WinPercent;
        Goals = totals.Goals;
        Kills = totals.Kills;
        Deaths = totals.Deaths;
        Assists = totals.Assists;
        KdRatio = totals.KdRatio;
        KdSpread = totals.KdSpread;
        CarrierKills = totals.CarrierKills;
        AverageLifeSeconds = totals.AverageLifeSeconds;
        TotalSecondsPlayed = totals.TotalSecondsPlayed;
        Sprees = totals.Sprees;
        MultiKills = totals.MultiKills;
        Exterminations = totals.Exterminations;
        Betrayals = totals.Betrayals;
        HammerKills = totals.HammerKills;
        SwordKills = totals.SwordKills;
        BallKills = totals.BallKills;
        Punches = totals.Punches;
        Score = totals.Score;
        DamageDealt = totals.DamageDealt;
        DamageTaken = totals.DamageTaken;
        ExtendedMetricsJson = totals.ExtendedMetricsJson;
    }

    /// <summary>The row's totals, as its sheet gave them (<see cref="Record"/>). Computed: not mapped.</summary>
    public StatSheetTotals Totals => new()
    {
        Gamertag = Gamertag,
        GamesPlayed = GamesPlayed,
        Wins = Wins,
        Losses = Losses,
        WinPercent = WinPercent,
        Goals = Goals,
        Kills = Kills,
        Deaths = Deaths,
        Assists = Assists,
        KdRatio = KdRatio,
        KdSpread = KdSpread,
        CarrierKills = CarrierKills,
        AverageLifeSeconds = AverageLifeSeconds,
        TotalSecondsPlayed = TotalSecondsPlayed,
        Sprees = Sprees,
        MultiKills = MultiKills,
        Exterminations = Exterminations,
        Betrayals = Betrayals,
        HammerKills = HammerKills,
        SwordKills = SwordKills,
        BallKills = BallKills,
        Punches = Punches,
        Score = Score,
        DamageDealt = DamageDealt,
        DamageTaken = DamageTaken,
        ExtendedMetricsJson = ExtendedMetricsJson,
    };

    /// <summary>
    /// A user merge: the row of <paramref name="fromUserID"/> becomes <paramref name="toUserID"/>'s. False, nothing
    /// changed, for a row of someone else. The merge refuses first where the target has a row of its own for the same
    /// slot (the unique index's).
    /// </summary>
    public bool ReassignUser(int fromUserID, int toUserID)
    {
        if (UserID != fromUserID)
            return false;
        UserID = toUserID;
        return true;
    }
}

/// <summary>A sheet row's totals, as every stored-totals table keeps them (<see cref="PlayerStatTotals"/>).</summary>
public record StatSheetTotals
{
    /// <summary>The gamertag the sheet lists the player under: required.</summary>
    public string Gamertag { get; init; }
    public int GamesPlayed { get; init; }
    public int Wins { get; init; }
    public int Losses { get; init; }
    public decimal? WinPercent { get; init; }
    public int Goals { get; init; }
    public int Kills { get; init; }
    public int Deaths { get; init; }
    public int Assists { get; init; }
    public decimal? KdRatio { get; init; }
    public int? KdSpread { get; init; }
    public int CarrierKills { get; init; }
    public decimal? AverageLifeSeconds { get; init; }
    public int? TotalSecondsPlayed { get; init; }
    public int Sprees { get; init; }
    public int MultiKills { get; init; }
    public int Exterminations { get; init; }
    public int Betrayals { get; init; }
    public int? HammerKills { get; init; }
    public int? SwordKills { get; init; }
    public int? BallKills { get; init; }
    public int? Punches { get; init; }
    public int? Score { get; init; }
    public int? DamageDealt { get; init; }
    public int? DamageTaken { get; init; }
    public string ExtendedMetricsJson { get; init; }
}
