namespace GrifballWebApp.Server.Teams;

public class CaptainPlacementDto
{
    public int SeasonID { get; set; }
    public int PersonID { get; set; }
    public int OrderNumber { get; set; }
}

public class RemoveCaptainDto
{
    public int SeasonID { get; set; }
    public int PersonID { get; set; }
}
