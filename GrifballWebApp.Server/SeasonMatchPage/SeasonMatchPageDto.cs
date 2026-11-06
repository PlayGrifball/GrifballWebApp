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
    public SeasonMatchSeriesTotalsDto? SeriesTotals { get; set; }
    public required BracketInfoDto? BracketInfo { get; set; }
    public int? ActiveRescheduleRequestId { get; set; }
    /// <summary>Whether the user asking may request a reschedule of this match now (see RescheduleRules); false for a visitor.</summary>
    public bool CanRequestReschedule { get; set; }
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
    /// <summary>The grand final or its sudden death: these cannot be forfeited.</summary>
    public required bool IsGrandFinal { get; set; }
}

public record SeasonMatchGameParticipantDto
{
    public int? UserID { get; set; }
    public required string Gamertag { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int BombScores { get; set; }
    public int? Score { get; set; }
}

public record ReportedGameDto
{
    public Guid? MatchID { get; set; }
    public int? RecordedGameID { get; set; }
    public required int MatchNumber { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public List<SeasonMatchGameParticipantDto> HomePlayers { get; set; } = [];
    public List<SeasonMatchGameParticipantDto> AwayPlayers { get; set; } = [];
}

public record SeasonMatchSeriesTotalsDto
{
    public List<SeasonMatchGameParticipantDto> HomePlayers { get; set; } = [];
    public List<SeasonMatchGameParticipantDto> AwayPlayers { get; set; } = [];
}
