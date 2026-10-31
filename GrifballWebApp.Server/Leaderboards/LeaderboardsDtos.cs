namespace GrifballWebApp.Server.Leaderboards;

/// <summary>The all-time leaderboards: titles, and career totals over every season's stats.</summary>
public class LeaderboardsDto
{
    public List<RingLeaderDto> RingLeaders { get; set; } = [];
    public List<DynastyDto> Dynasties { get; set; } = [];
    public List<MetricLeaderDto> GoalLeaders { get; set; } = [];
    public List<MetricLeaderDto> KillLeaders { get; set; } = [];
    public List<MetricLeaderDto> GamesPlayedLeaders { get; set; } = [];
    public List<MetricLeaderDto> CarrierStopLeaders { get; set; } = [];
}

/// <summary>A player's championships.</summary>
public class RingLeaderDto
{
    public int UserID { get; set; }
    public required string Gamertag { get; set; }
    public int RingsCount { get; set; }
    public List<string> Titles { get; set; } = [];
}

/// <summary>
/// A team name's titles across seasons (a team is one season's; the teams that carried a name from season to season
/// are counted together), with its latest title-winning team to link to.
/// </summary>
public class DynastyDto
{
    public required string TeamName { get; set; }
    public int LatestTeamID { get; set; }
    public int LatestSeasonID { get; set; }
    public int ChampionshipsCount { get; set; }
    public int SeasonsActive { get; set; }
    public List<string> Titles { get; set; } = [];
}

public class MetricLeaderDto
{
    public int UserID { get; set; }
    public required string Gamertag { get; set; }
    public int StatValue { get; set; }
    public int GamesPlayed { get; set; }
    public decimal? PerGame { get; set; }
    public int SeasonsCount { get; set; }
    public string? SecondaryStat { get; set; }
}
