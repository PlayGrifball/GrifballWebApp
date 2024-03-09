#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class BracketMatch
{
    public int BracketMatchID { get; set; }
    public int SeasonMatchID { get; set; }
    public int RoundNumber { get; set; }
    public int MatchNumber { get; set; }
    public SeasonMatch SeasonMatch { get; set; }
    //public bool Bye { get; set; }
    //public bool LosersBracket { get; set; }
}
