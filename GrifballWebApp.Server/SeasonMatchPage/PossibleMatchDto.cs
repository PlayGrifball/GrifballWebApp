using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Server.SeasonMatchPage;

public record PossibleMatchDto
{
    public required Guid MatchID { get; set; }
    public required PossibleTeamDto HomeTeam { get; set; }
    public required PossibleTeamDto AwayTeam { get; set; }
}

public class PossibleTeamDto
{
    public required int TeamID { get; set; }
    public required int Score { get; set; }
    public required Outcomes Outcome { get; set; }
    public required PossiblePlayerDto[] Players { get; set; }
}

public record PossiblePlayerDto
{
    public required long XboxUserID { get; set; }
    public required string Gamertag { get; set; }
    public required int Score { get; set; }
    public required int Kills { get; set; }
    public required int Deaths { get; set; }
    public required bool IsOnTeam { get; set; }
}
