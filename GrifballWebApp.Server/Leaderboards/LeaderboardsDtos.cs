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
    public List<TitleDto> Titles { get; set; } = [];
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
    public List<TitleDto> Titles { get; set; } = [];
}

public class MetricLeaderDto
{
    public int UserID { get; set; }
    public required string Gamertag { get; set; }
    /// <summary>The game the total is of (goals and carrier stops are counted per game); null for a career total across games.</summary>
    public string? GameVersionName { get; set; }
    public int StatValue { get; set; }
    public int GamesPlayed { get; set; }
    public decimal? PerGame { get; set; }
    public int SeasonsCount { get; set; }
    public string? SecondaryStat { get; set; }
}

/// <summary>A title: the season, its tier and the team that won it, each linked on the page.</summary>
public class TitleDto
{
    public int SeasonID { get; set; }
    public required string SeasonName { get; set; }
    public required string Tier { get; set; }
    public int TeamID { get; set; }
}
