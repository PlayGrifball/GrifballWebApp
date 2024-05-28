using System.Text.Json.Serialization;

namespace GrifballWebApp.Server.SeasonMatchPage;

public record SeasonMatchPageDto
{
    public required int SeasonID { get; set; }
    public required string SeasonName { get; set; }
    public required bool IsPlayoff { get; set; }
    public required string? HomeTeamName { get; set; }
    public required int? HomeTeamID { get; set; }
    public required int? HomeTeamScore { get; set; }
    public required SeasonMatchResultDto HomeTeamResult { get; set; }
    public required string? AwayTeamName { get; set; }
    public required int? AwayTeamID { get; set; }
    public required int? AwayTeamScore { get; set; }
    public required SeasonMatchResultDto AwayTeamResult { get; set; }
    public required DateTime? ScheduledTime { get; set; }
    public required int BestOf { get; set; }
    public required ReportedGameDto[] ReportedGames { get; set; }
    public required BracketInfoDto? BracketInfo { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SeasonMatchResultDto
{
    Won,
    Loss,
    Forfeit,
    Bye,
    TBD,
}

public record BracketInfoDto
{
    public required int? HomeTeamSeed { get; set; }
    public required int? AwayTeamSeed { get; set; }
    public required int? HomeTeamPreviousMatchID { get; set; }
    public required int? AwayTeamPreviousMatchID { get; set; }
    public required int? WinnerNextMatchID { get; set; }
    public required int? LoserNextMatchID { get; set; }
}

public record ReportedGameDto
{
    public required Guid MatchID { get; set; }
    public required int MatchNumber { get; set; }
}