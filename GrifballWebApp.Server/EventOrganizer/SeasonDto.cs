namespace GrifballWebApp.Server.EventOrganizer;

#nullable disable

public class SeasonDto
{
    public int SeasonID { get; set; }
    public string SeasonName { get; set; }
    public DateTime SignupsOpen { get; set; }
    public DateTime SignupsClose { get; set; }
    public DateTime DraftStart { get; set; }
    public DateTime SeasonStart { get; set; }
    public DateTime SeasonEnd { get; set; }
    public int SignupsCount { get; set; }
}
