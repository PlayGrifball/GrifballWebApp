using Tapper;

namespace GrifballWebApp.Application.Teams;

[TranspilationSource]
public class CaptainDto
{
    public int PersonID { get; set; }
    public required string Name { get; set; }
    public int? Order { get; set; }
    /// <summary>Where the captain plays from.</summary>
    public string? Region { get; set; }
}
