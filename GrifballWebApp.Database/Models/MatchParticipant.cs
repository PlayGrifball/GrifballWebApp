using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;

/// <summary>A player of a Halo Infinite match, on the team they ended it on. Part of its match: made only by it
/// (<see cref="Match.AddParticipant"/>), and decoded by it (<see cref="Match.Decode"/>).</summary>
[AggregatePart(typeof(Match))]
public class MatchParticipant : AuditableEntity
{
    /// <summary>For EF.</summary>
    private MatchParticipant() { }

    internal MatchParticipant(MatchTeam team, long xboxUserID, MatchParticipantStats stats, IReadOnlyCollection<MedalCount> medals, XboxUser? xboxUser)
    {
        MatchTeam = team;
        MatchID = team.MatchID;
        TeamID = team.TeamID;
        XboxUserID = xboxUserID;
        if (xboxUser is not null)
            XboxUser = xboxUser;

        Score = stats.Score;
        PersonalScore = stats.PersonalScore;
        Kills = stats.Kills;
        Deaths = stats.Deaths;
        Assists = stats.Assists;
        Kda = stats.Kda;
        Suicides = stats.Suicides;
        Betrayals = stats.Betrayals;
        AverageLife = stats.AverageLife;
        MeleeKills = stats.MeleeKills;
        PowerWeaponKills = stats.PowerWeaponKills;
        ShotsFired = stats.ShotsFired;
        ShotsHit = stats.ShotsHit;
        Accuracy = stats.Accuracy;
        DamageDealt = stats.DamageDealt;
        CalloutAssists = stats.CalloutAssists;
        MaxKillingSpree = stats.MaxKillingSpree;
        DamageTaken = stats.DamageTaken;
        FirstJoinedTime = stats.FirstJoinedTime;
        LastLeaveTime = stats.LastLeaveTime;
        JoinedInProgress = stats.JoinedInProgress;
        LeftInProgress = stats.LeftInProgress;
        PresentAtBeginning = stats.PresentAtBeginning;
        PresentAtCompletion = stats.PresentAtCompletion;
        TimePlayed = stats.TimePlayed;
        RoundsWon = stats.RoundsWon;
        RoundsLost = stats.RoundsLost;
        RoundsTied = stats.RoundsTied;
        Rank = stats.Rank;
        Spawns = stats.Spawns;
        ObjectivesCompleted = stats.ObjectivesCompleted;

        foreach (var medal in medals)
            _medalEarned.Add(new MedalEarned(this, medal));
    }

    // The keys are internal (the match gives them); everything else is set here, by the match's methods, and by EF
    public long XboxUserID { get; internal set; }

    public int TeamID { get; internal set; }
    public Guid MatchID { get; internal set; }

    public int Score { get; private set; }
    public int PersonalScore { get; private set; }
    public int Kills { get; private set; }
    public int Deaths { get; private set; }
    public int Assists { get; private set; }
    public float Kda { get; private set; }
    public int Suicides { get; private set; }
    public int Betrayals { get; private set; }
    public TimeSpan AverageLife { get; private set; }
    public int MeleeKills { get; private set; }
    public int PowerWeaponKills { get; private set; }
    public int ShotsFired { get; private set; }
    public int ShotsHit { get; private set; }
    public float Accuracy { get; private set; }
    public int DamageDealt { get; private set; }
    public int CalloutAssists { get; private set; }
    public int MaxKillingSpree { get; private set; }
    public int DamageTaken { get; private set; }

    public DateTime FirstJoinedTime { get; private set; }
    public DateTime? LastLeaveTime { get; private set; }
    public bool JoinedInProgress { get; private set; }
    public bool LeftInProgress { get; private set; }
    public bool PresentAtBeginning { get; private set; }
    public bool PresentAtCompletion { get; private set; }
    public TimeSpan TimePlayed { get; private set; }
    public int RoundsWon { get; private set; }
    public int RoundsLost { get; private set; }
    public int RoundsTied { get; private set; }
    public int Rank { get; private set; }
    public int Spawns { get; private set; }
    public int ObjectivesCompleted { get; private set; }

    // Decoded from Score and PersonalScore by the match's ScoreLayout (#217); null where its layout doesn't have the
    // stat, or the match isn't decoded
    public int? Goals { get; private set; }
    public int? PassIns { get; private set; }
    public int? PlateStops { get; private set; }
    public int? SwordKills { get; private set; }
    public int? CarrierKills { get; private set; }
    public int? BallPunches { get; private set; }
    public int? GoalAssists { get; private set; }
    public TimeSpan? BallHeldTime { get; private set; }

    public MatchTeam MatchTeam { get; private set; } = null!;
    public XboxUser XboxUser { get; private set; } = null!;

    // Read-only: made with the player (EF uses the field)
    public IReadOnlyCollection<MedalEarned> MedalEarned => _medalEarned;
    private readonly List<MedalEarned> _medalEarned = [];

    /// <summary>
    /// The player's Grifball goals in the game, the one definition the season stats, the grades and the box scores read
    /// (through GrifballGoals): a decoded match's (<see cref="Goals"/>); an undecoded one's Score, when it's at most
    /// <see cref="Match.PlainMaxScore"/>, and none otherwise. Reads <see cref="MatchTeam"/>'s match.
    /// </summary>
    public static readonly Expression<Func<MatchParticipant, int?>> CountedGoals = p =>
        p.MatchTeam.Match.ScoreLayout == null ? (p.Score <= Match.PlainMaxScore ? (int?)p.Score : null) : p.Goals;

    /// <summary>What its match's layout decodes from its scores (<see cref="Match.Decode"/>); true when it changed.</summary>
    internal bool SetDecoded(DecodedScore d)
    {
        var changed = Goals != d.Goals || PassIns != d.PassIns || PlateStops != d.PlateStops || SwordKills != d.SwordKills
            || CarrierKills != d.CarrierKills || BallPunches != d.BallPunches || GoalAssists != d.GoalAssists || BallHeldTime != d.BallHeldTime;
        Goals = d.Goals;
        PassIns = d.PassIns;
        PlateStops = d.PlateStops;
        SwordKills = d.SwordKills;
        CarrierKills = d.CarrierKills;
        BallPunches = d.BallPunches;
        GoalAssists = d.GoalAssists;
        BallHeldTime = d.BallHeldTime;
        return changed;
    }
}

/// <summary>A player's stats in a Halo Infinite match, as Halo gives them (their core stats and how they took part).</summary>
public sealed record MatchParticipantStats
{
    public int Score { get; init; }
    public int PersonalScore { get; init; }
    public int Kills { get; init; }
    public int Deaths { get; init; }
    public int Assists { get; init; }
    public float Kda { get; init; }
    public int Suicides { get; init; }
    public int Betrayals { get; init; }
    public TimeSpan AverageLife { get; init; }
    public int MeleeKills { get; init; }
    public int PowerWeaponKills { get; init; }
    public int ShotsFired { get; init; }
    public int ShotsHit { get; init; }
    public float Accuracy { get; init; }
    public int DamageDealt { get; init; }
    public int CalloutAssists { get; init; }
    public int MaxKillingSpree { get; init; }
    public int DamageTaken { get; init; }
    public DateTime FirstJoinedTime { get; init; }
    public DateTime? LastLeaveTime { get; init; }
    public bool JoinedInProgress { get; init; }
    public bool LeftInProgress { get; init; }
    public bool PresentAtBeginning { get; init; }
    public bool PresentAtCompletion { get; init; }
    public TimeSpan TimePlayed { get; init; }
    public int RoundsWon { get; init; }
    public int RoundsLost { get; init; }
    public int RoundsTied { get; init; }
    public int Rank { get; init; }
    public int Spawns { get; init; }
    public int ObjectivesCompleted { get; init; }
}

/// <summary>One player's stats decoded from their Score and PersonalScore. A stat the layout doesn't have is null.</summary>
public sealed record DecodedScore(int? Goals, int? PassIns = null, int? PlateStops = null, int? SwordKills = null, int? CarrierKills = null,
    int? BallPunches = null, int? GoalAssists = null, TimeSpan? BallHeldTime = null)
{
    public static readonly DecodedScore None = new((int?)null);
}
