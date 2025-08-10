#nullable disable

namespace GrifballWebApp.Database.Models;
public class MatchParticipant : AuditableEntity
{
    public MatchParticipant()
    {
        MedalEarned = new HashSet<MedalEarned>();
    }

    
    public long XboxUserID { get; set; }

    public int TeamID { get; set; }
    public Guid MatchID { get; set; }

    public int Score { get; set; }
    public int PersonalScore { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public float Kda { get; set; }
    public int Suicides { get; set; }
    public int Betrayals { get; set; }
    public TimeSpan AverageLife { get; set; }
    public int MeleeKills { get; set; }
    public int PowerWeaponKills { get; set; }
    public int ShotsFired { get; set; }
    public int ShotsHit { get; set; }
    public float Accuracy { get; set; }
    public int DamageDealt { get; set; }
    public int CalloutAssists { get; set; }
    public int MaxKillingSpree { get; set; }
    public int DamageTaken { get; set; }

    public DateTime FirstJoinedTime { get; set; }
    public DateTime? LastLeaveTime { get; set; }
    public bool JoinedInProgress { get; set; }
    public bool LeftInProgress { get; set; }
    public bool PresentAtBeginning { get; set; }
    public bool PresentAtCompletion { get; set; }
    public TimeSpan TimePlayed { get; set; }
    public int RoundsWon { get; set; }
    public int RoundsLost { get; set; }
    public int RoundsTied { get; set; }
    public int Rank { get; set; }
    public int Spawns { get; set; }
    public int ObjectivesCompleted { get; set; }

    public MatchTeam MatchTeam { get; set; }
    public XboxUser XboxUser { get; set; }

    public virtual ICollection<MedalEarned> MedalEarned { get; set; }
}
