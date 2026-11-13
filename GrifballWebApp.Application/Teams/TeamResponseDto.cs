using Tapper;

namespace GrifballWebApp.Application.Teams;

[TranspilationSource]
public class TeamResponseDto
{
    public int TeamID { get; set; }
    public required string TeamName { get; set; }
    public required CaptainDto Captain { get; set; }
    public List<PlayerDto> Players { get; set; } = new();
}
