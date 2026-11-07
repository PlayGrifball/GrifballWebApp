#nullable disable

namespace GrifballWebApp.Database.Models;
public class MatchBracketInfo : AuditableEntity
{
    public MatchBracketInfo()
    {
        InverseHomeTeamPreviousMatchBracketInfo = new HashSet<MatchBracketInfo>();
        InverseAwayTeamNextMatchBracketInfo = new HashSet<MatchBracketInfo>();
    }
    public int MatchBracketInfoID { get; set; }
    // Its season match's slot: set only through the match (SeasonMatch.InBracket, SeasonMatch.Imported); internal for tests
    public int SeasonMatchID { get; internal set; }
    public int RoundNumber { get; set; }
    public int MatchNumber { get; set; }
    public SeasonMatch SeasonMatch { get; internal set; }
    public int? HomeTeamSeedNumber { get; set; }
    public int? HomeTeamPreviousMatchBracketInfoID { get; set; }
    public MatchBracketInfo HomeTeamPreviousMatchBracketInfo { get; set; }
    public virtual ICollection<MatchBracketInfo> InverseHomeTeamPreviousMatchBracketInfo { get; set; }
    public int? AwayTeamSeedNumber { get; set; }
    public int? AwayTeamPreviousMatchBracketInfoID { get; set; }
    public MatchBracketInfo AwayTeamPreviousMatchBracketInfo { get; set; }
    public virtual ICollection<MatchBracketInfo> InverseAwayTeamNextMatchBracketInfo { get; set; }
    //public bool Bye { get; set; }
    public Bracket Bracket { get; set; }
}

public enum Bracket
{
    Winner,
    Loser,
    GrandFinal,
    GrandFinalSuddenDeath
}