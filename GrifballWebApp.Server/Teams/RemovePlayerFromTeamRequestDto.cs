namespace GrifballWebApp.Server.Teams;

public record RemovePlayerFromTeamRequestDto
{
    public int SeasonID { get; set; }
    public int CaptainID { get; set; }
    public int PersonID { get; set; }
}
