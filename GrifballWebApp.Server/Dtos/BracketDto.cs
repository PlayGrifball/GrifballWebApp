#nullable disable

namespace GrifballWebApp.Server.Dtos;

public class BracketDto
{
    public RoundDto[] WinnerRounds { get; set; }
    public RoundDto[] LoserRounds { get; set; }
    public MatchDto GrandFinal { get; set; }
    public MatchDto GrandFinalSuddenDeath { get; set; }
}

public class RoundDto
{
    public int RoundNumber { get; set; }
    public MatchDto[] Matches { get; set; }
}

public class MatchDto
{
    public string MatchNumber { get; set; }
    public string HomeTeam { get; set; }
    public string AwayTeam { get; set; }
}