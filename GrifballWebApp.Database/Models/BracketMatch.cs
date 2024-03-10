#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class BracketMatch
{
    public int BracketMatchID { get; set; }
    public int SeasonMatchID { get; set; }
    public int RoundNumber { get; set; }
    public int MatchNumber { get; set; }
    public SeasonMatch SeasonMatch { get; set; }
    public int? HomeTeamSeedNumber { get; set; }
    public int? HomeTeamPreviousBracketMatchID { get; set; }
    public BracketMatch HomeTeamPreviousBracketMatch { get; set; }
    public BracketMatch HomeTeamNextBracketMatch { get; set; }
    public int? AwayTeamSeedNumber { get; set; }
    public int? AwayTeamPreviousBracketMatchID { get; set; }
    public BracketMatch AwayTeamPreviousBracketMatch { get; set; }
    public BracketMatch AwayTeamNextBracketMatch { get; set; }
    //public bool Bye { get; set; }
    public bool LosersBracket { get; set; }
}
