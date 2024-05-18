namespace GrifballWebApp.Server.TeamStandings;

public record TeamStandingDto
{
    public required int TeamID { get; set; }
    public required string TeamName { get; set; }
    public required int Wins { get; set; }
    public required int Losses { get; set; }
}
