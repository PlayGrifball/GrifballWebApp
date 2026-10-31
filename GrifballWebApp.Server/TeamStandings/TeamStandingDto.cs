namespace GrifballWebApp.Server.TeamStandings;

public record TeamStandingDto
{
    public required int TeamID { get; set; }
    public required string TeamName { get; set; }
    public required int Wins { get; set; }
    public required int Losses { get; set; }
    /// <summary>The record is the source spreadsheet's: the season's matches are not in the app.</summary>
    public bool RecordFromSource { get; set; }
}
