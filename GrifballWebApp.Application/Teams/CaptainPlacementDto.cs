using Tapper;

namespace GrifballWebApp.Application.Teams;

[TranspilationSource]
public class CaptainPlacementDto
{
    public int SeasonID { get; set; }
    public int PersonID { get; set; }
    public int OrderNumber { get; set; }
}

[TranspilationSource]
public class RemoveCaptainDto
{
    public int SeasonID { get; set; }
    public int PersonID { get; set; }
}

/// <summary>The answer to AddCaptain: the name the server gave the new team.</summary>
public class CaptainAddedResponseDto
{
    public required string TeamName { get; set; }
    public int TeamID { get; set; }
}
