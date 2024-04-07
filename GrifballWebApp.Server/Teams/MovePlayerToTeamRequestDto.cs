namespace GrifballWebApp.Server.Teams;

public record MovePlayerToTeamRequestDto
{
    public int SeasonID { get; set; }
    public int PreviousCaptainID { get; set; }
    public int NewCaptainID { get; set; }
    public int PersonID { get; set; }
    public int RoundNumber { get; set; }
}
