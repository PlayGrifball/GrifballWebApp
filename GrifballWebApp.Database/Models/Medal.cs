#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class Medal
{
    public long MedalID { get; set; }
    public string MedalName { get; set; }
    public string Description { get; set; }
    public int SpriteIndex { get; set; }
    public int SortingWeight { get; set; }
    public int MedalDifficultyID { get; set; }
    public int MedalTypeID { get; set; }
    public int PersonalScore { get; set; }

    public MedalDifficulty MedalDifficulty { get; set; }
    public MedalType MedalType { get; set; }
}
