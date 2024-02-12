#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class MatchParticipant
{
    public Guid MatchID { get; set; }
    public long XboxUserID { get; set; }

    public int TeamID { get; set; }

    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int Kda { get; set; }
    public int Suicides { get; set; }
    public int Betrayals { get; set; }
    public TimeSpan AverageLife { get; set; }
    public int GrenadeKills { get; set; }
    public int HeadshotKills { get; set; }
    public int MeleeKills { get; set; }
    public int PowerWeaponKills { get; set; }
    public int ShotsFired { get; set; }
    public int ShotsHit { get; set; }
    public int Accuracy { get; set; }
    public int DamageDealt { get; set; }
    public int CalloutAssists { get; set; }
    public int VehicleDestroys { get; set; }
    public int DriverAssists { get; set; }
    public int Hijacks { get; set; }
    public int EmpAssists { get; set; }
    public int MaxKillingSpree { get; set; }
    public int DamageTaken { get; set; }

    public Match Match { get; set; }
    public XboxUser XboxUser { get; set; }
}
