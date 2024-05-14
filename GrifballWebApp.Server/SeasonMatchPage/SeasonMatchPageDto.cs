namespace GrifballWebApp.Server.SeasonMatchPage;

public record SeasonMatchPageDto
{
    public required int SeasonID { get; set; }
    public required string SeasonName { get; set; }
    public required bool IsPlayoff { get; set; }
    public required string? HomeTeamName { get; set; }
    public required int? HomeTeamID { get; set; }
    public required int? HomeTeamScore { get; set; }
    public required string? AwayTeamName { get; set; }
    public required int? AwayTeamID { get; set; }
    public required int? AwayTeamScore { get; set; }
    public required DateTime? ScheduledTime { get; set; }
    public required int BestOf { get; set; }
    public required ReportedGameDto[] ReportedGames { get; set; }
}

public record ReportedGameDto
{
    public required Guid MatchID { get; set; }
    public required int MatchNumber { get; set; }
}