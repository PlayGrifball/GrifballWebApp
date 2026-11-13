using Tapper;

namespace GrifballWebApp.Application.Teams;

[TranspilationSource]
public class PlayerDto
{
    public int PersonID { get; set; }
    public required string Name { get; set; }
    public int? Round { get; set; }
    public int? Pick { get; set; }
    /// <summary>Where the player plays from, for building teams.</summary>
    public string? Region { get; set; }
}
