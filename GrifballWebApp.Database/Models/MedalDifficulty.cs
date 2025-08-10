#nullable disable

namespace GrifballWebApp.Database.Models;
public class MedalDifficulty : AuditableEntity
{
    public MedalDifficulty()
    {
        Medals = new HashSet<Medal>();
    }

    public int MedalDifficultyID { get; set; }
    public string MedalDifficultyName { get; set; }

    public virtual ICollection<Medal> Medals { get; set; }
}
