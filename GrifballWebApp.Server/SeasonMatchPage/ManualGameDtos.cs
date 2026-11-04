namespace GrifballWebApp.Server.SeasonMatchPage;

/// <summary>
/// A game of a season match's series entered by hand by a commissioner (<see cref="SeasonMatchService.AddManualGame"/>):
/// when the stat bot or Halo Infinite's stats have no record of it, or the game is not Halo Infinite at all (MCC, a newer
/// Halo). The winner is the side with the higher score.
/// </summary>
public record ManualGameRequestDto
{
    /// <summary>The game it was played in (Other.GameVersions).</summary>
    public required int GameVersionID { get; set; }
    public string? Map { get; set; }
    public string? Gametype { get; set; }
    /// <summary>When it was played, which orders it among the series' games; optional.</summary>
    public DateTime? PlayedAt { get; set; }
    public required int HomeTeamScore { get; set; }
    public required int AwayTeamScore { get; set; }
    /// <summary>Each player's line, optional: a game can be entered with its score alone.</summary>
    public List<ManualGamePlayerRequestDto> Players { get; set; } = [];
}

/// <summary>One player's line of a <see cref="ManualGameRequestDto"/>.</summary>
public record ManualGamePlayerRequestDto
{
    public required int UserID { get; set; }
    /// <summary>Played for the home team (true) or the away team (false).</summary>
    public required bool IsHomeTeam { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int? Score { get; set; }
}

/// <summary>
/// A game of the series that is not a Waypoint match: entered by hand (<see cref="IsManual"/>, which a commissioner can
/// edit and delete) or recorded by the historical import.
/// </summary>
public record RecordedSeriesGameDto
{
    public required int RecordedGameID { get; set; }
    public required int GameNumber { get; set; }
    public required int GameVersionID { get; set; }
    public required string GameVersionName { get; set; }
    public required string? Map { get; set; }
    public required string? Gametype { get; set; }
    public required DateTime? PlayedAt { get; set; }
    public required int? HomeTeamScore { get; set; }
    public required int? AwayTeamScore { get; set; }
    /// <summary>The home team won the game (false: the away team did, or nobody is recorded as winning).</summary>
    public required bool HomeTeamWon { get; set; }
    public required bool AwayTeamWon { get; set; }
    /// <summary>Entered by hand: a commissioner can edit and delete it.</summary>
    public required bool IsManual { get; set; }
    public required RecordedSeriesGamePlayerDto[] Players { get; set; }
}

public record RecordedSeriesGamePlayerDto
{
    public required int UserID { get; set; }
    public required string Gamertag { get; set; }
    public required bool IsHomeTeam { get; set; }
    public required int Kills { get; set; }
    public required int Deaths { get; set; }
    public required int Assists { get; set; }
    public required int? Score { get; set; }
}
