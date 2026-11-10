namespace GrifballWebApp.Application.Leaderboards;

/// <summary>A row of a board, ranked in standard competition ranking (ties share a rank).</summary>
public interface IRanked
{
    int Rank { get; set; }
}

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
public class RingLeaderDto : IRanked
{
    public int Rank { get; set; }
    public int UserID { get; set; }
    public required string Gamertag { get; set; }
    public int RingsCount { get; set; }
    public List<TitleDto> Titles { get; set; } = [];
}

/// <summary>
/// A team name's titles across seasons (a team is one season's; the teams that carried a name from season to season
/// are counted together), with its latest title-winning team to link to.
/// </summary>
public class DynastyDto : IRanked
{
    public int Rank { get; set; }
    public required string TeamName { get; set; }
    public int LatestTeamID { get; set; }
    public int LatestSeasonID { get; set; }
    public int ChampionshipsCount { get; set; }
    public int SeasonsActive { get; set; }
    public List<TitleDto> Titles { get; set; } = [];
}

public class MetricLeaderDto : IRanked
{
    public int Rank { get; set; }
    public int UserID { get; set; }
    public required string Gamertag { get; set; }
    /// <summary>The game the total is of (goals and carrier stops are counted per game); null for a career total across games.</summary>
    public string? GameVersionName { get; set; }
    public long StatValue { get; set; }
    public int GamesPlayed { get; set; }
    /// <summary>Of <see cref="GamesPlayed"/>, the Late League games decided by their players' vote: a win or a loss with no box
    /// score, so left out of <see cref="PerGame"/>.</summary>
    public int GamesDecidedByVote { get; set; }
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

/// <summary>Universal all-time ranking summary for a specific player across every tracked metric.</summary>
public class PlayerAllTimeRankDto
{
    public int UserID { get; set; }
    public required string Gamertag { get; set; }
    public string? MatchedAlias { get; set; }
    public List<string> PastGamertags { get; set; } = [];
    public bool IsDummyUser { get; set; }

    public MetricRankDetail Rings { get; set; } = null!;
    public MetricRankDetail Goals { get; set; } = null!;
    public MetricRankDetail Kills { get; set; } = null!;
    public MetricRankDetail GamesPlayed { get; set; } = null!;
    public MetricRankDetail CarrierStops { get; set; } = null!;
}

/// <summary>A player's rank and totals on one metric leaderboard.</summary>
public class MetricRankDetail
{
    /// <summary>Null when the player has none of the stat, so isn't on the board.</summary>
    public int? Rank { get; set; }
    /// <summary>How many are on the board: those with any of the stat.</summary>
    public int TotalRanked { get; set; }
    public long StatValue { get; set; }
    public string? SecondaryText { get; set; }
}
