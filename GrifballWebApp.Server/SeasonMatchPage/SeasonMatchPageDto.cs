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
    /// <summary>What decided the match (its games, a forfeit, a BYE); null while undecided. The model's own answer
    /// (SeasonMatch.DecidedBy), so the page need not work it out from the two results.</summary>
    public SeasonMatchDecisionDto? DecidedBy { get; set; }
    public required DateTime? ScheduledTime { get; set; }
    public required int BestOf { get; set; }
    public required ReportedGameDto[] ReportedGames { get; set; }
    public SeasonMatchSeriesTotalsDto? SeriesTotals { get; set; }
    /// <summary>The season was imported from the historical archive: its games are not entered by hand (the next
    /// import would remove them).</summary>
    public bool IsImported { get; set; }
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
    /// <summary>Halo's points; null where the game doesn't record them, or its mode packs its stats into them (#217).</summary>
    public int? Score { get; set; }
    /// <summary>Kills per death as <see cref="Stats.StatMath.KdRatio"/> counts it everywhere: null without a death.</summary>
    public decimal? KdRatio { get; set; }
}

public record ReportedGameDto
{
    /// <summary>The game in the series: what unreporting it, or replacing it, names.</summary>
    public required int SeriesGameID { get; set; }
    public Guid? MatchID { get; set; }
    public int? RecordedGameID { get; set; }
    public required int MatchNumber { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public List<SeasonMatchGameParticipantDto> HomePlayers { get; set; } = [];
    public List<SeasonMatchGameParticipantDto> AwayPlayers { get; set; } = [];
    /// <summary>A recorded game a commissioner entered by hand: they can edit and delete it from the box score.</summary>
    public bool IsManual { get; set; }
    /// <summary>A recorded game's details (null for a Waypoint game): what it shows and what its edit starts from.</summary>
    public int? GameVersionID { get; set; }
    public string? GameVersionName { get; set; }
    public string? Map { get; set; }
    public string? Gametype { get; set; }
    /// <summary>When a recorded game was played, if it was recorded.</summary>
    public DateTime? PlayedAt { get; set; }
}

public record SeasonMatchSeriesTotalsDto
{
    public List<SeasonMatchGameParticipantDto> HomePlayers { get; set; } = [];
    public List<SeasonMatchGameParticipantDto> AwayPlayers { get; set; } = [];
}

/// <summary>What decided a season match (SeasonMatch.DecidedBy).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SeasonMatchDecisionDto
{
    Games,
    Forfeit,
    Bye,
}
