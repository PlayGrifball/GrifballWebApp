namespace GrifballWebApp.Server.EventOrganizer;

public class SeasonDto
{
    public int SeasonID { get; set; }
    public string SeasonName { get; set; } = null!;
    public DateTime SignupsOpen { get; set; }
    public DateTime SignupsClose { get; set; }
    public DateTime DraftStart { get; set; }
    public DateTime SeasonStart { get; set; }
    public DateTime SeasonEnd { get; set; }
    public int SignupsCount { get; set; }
    public string? CopyFrom { get; set; }
    public bool CopyAvailability { get; set; }
    public bool CopySignups { get; set; }
    public bool CopyTeams { get; set; }
}
