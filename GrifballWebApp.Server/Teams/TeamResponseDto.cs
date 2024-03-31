namespace GrifballWebApp.Server.Teams;

public class TeamResponseDto
{
    public int TeamID { get; set; }
    public string TeamName { get; set; }
    public CaptainDto Captain { get; set; }
    public List<PlayerDto> Players { get; set; } = new();
}
