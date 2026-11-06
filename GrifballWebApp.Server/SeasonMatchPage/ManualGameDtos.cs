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
    /// <summary>When it was played, which places a new game among the series' games; optional (without it, the game
    /// goes after the others and keeps its place).</summary>
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
    /// <summary>Grifball goals (bomb scores): counted in the stats and the box score like an imported game's.</summary>
    public int? Goals { get; set; }
    public int? Score { get; set; }
}
