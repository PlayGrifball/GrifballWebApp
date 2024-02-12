#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class MedalDifficulty
{
    public MedalDifficulty()
    {
        Medals = new HashSet<Medal>();
    }

    public int MedalDifficultyID { get; set; }
    public string MedalDifficultyName { get; set; }

    public virtual ICollection<Medal> Medals { get; set; }
}
