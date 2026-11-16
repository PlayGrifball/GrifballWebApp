namespace GrifballWebApp.Application.TeamStandings;

public record TeamStandingDto
{
    public required int TeamID { get; set; }
    public required string TeamName { get; set; }
    public required int Wins { get; set; }
    public required int Losses { get; set; }
    /// <summary>The record is the source spreadsheet's: the season's matches are not in the app.</summary>
    public bool RecordFromSource { get; set; }
    /// <summary>The team's final place (1 for the champion) as the source spreadsheet gives it; null when it gives none.</summary>
    public int? Finish { get; set; }
}
