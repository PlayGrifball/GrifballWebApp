#nullable disable

namespace GrifballWebApp.Database.Models;
public partial class MatchBracketInfo
{
    public int MatchBracketInfoID { get; set; }
    public int SeasonMatchID { get; set; }
    public int RoundNumber { get; set; }
    public int MatchNumber { get; set; }
    public SeasonMatch SeasonMatch { get; set; }
    public int? HomeTeamSeedNumber { get; set; }
    public int? HomeTeamPreviousMatchBracketInfoID { get; set; }
    public MatchBracketInfo HomeTeamPreviousMatchBracketInfo { get; set; }
    public MatchBracketInfo HomeTeamNextMatchBracketInfo { get; set; }
    public int? AwayTeamSeedNumber { get; set; }
    public int? AwayTeamPreviousMatchBracketInfoID { get; set; }
    public MatchBracketInfo AwayTeamPreviousMatchBracketInfo { get; set; }
    public MatchBracketInfo AwayTeamNextMatchBracketInfo { get; set; }
    //public bool Bye { get; set; }
    public bool LosersBracket { get; set; }
}
