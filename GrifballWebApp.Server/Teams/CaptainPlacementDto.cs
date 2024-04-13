namespace GrifballWebApp.Server.Teams;

public class CaptainPlacementDto
{
    public int SeasonID { get; set; }
    public int PersonID { get; set; }
    public int OrderNumber { get; set; }
}

public record CaptainAddedDto
{
    public required int SeasonID { get; set; }
    public required int PersonID { get; set; }
    public required string TeamName { get; set; }
    public required string CaptainName { get; set; }
    public required int OrderNumber { get; set; }
}

public class RemoveCaptainDto
{
    public int SeasonID { get; set; }
    public int PersonID { get; set; }
}
