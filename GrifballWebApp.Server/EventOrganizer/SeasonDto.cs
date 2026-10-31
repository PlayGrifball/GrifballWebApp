namespace GrifballWebApp.Server.EventOrganizer;

public class SeasonDto
{
    public int SeasonID { get; set; }
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(100)] // the column's length (historical seasons have long names)
    public string SeasonName { get; set; } = null!;
    /// <summary>When the season is announced (Discord events channel); null keeps it unannounced.</summary>
    public DateTime? PublicAt { get; set; }
    public DateTime SignupsOpen { get; set; }
    public DateTime SignupsClose { get; set; }
    public DateTime DraftStart { get; set; }
    public DateTime SeasonStart { get; set; }
    public DateTime SeasonEnd { get; set; }
    public int SignupsCount { get; set; }
    /// <summary>The game the season is played on; a new season must have one.</summary>
    public int? GameVersionID { get; set; }
    /// <summary>The league the season is in (AGLA unless set otherwise).</summary>
    public int LeagueID { get; set; } = GrifballWebApp.Database.Models.League.DefaultLeagueID;
    /// <summary>The season's division, e.g. Pro: the tier its champions win. None: one open tier.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(50)]
    public string? Division { get; set; }
    /// <summary>The season to copy availability, signups or teams from (by ID: names are only unique per league).</summary>
    public int? CopyFromSeasonID { get; set; }
    public bool CopyAvailability { get; set; }
    public bool CopySignups { get; set; }
    public bool CopyTeams { get; set; }
}
