namespace GrifballWebApp.Application.MatchPlanner;

public record UpdateMatchTimeDto
{
    public int SeasonMatchID { get; set; }
    public DateTime? Time { get; set; }
}
