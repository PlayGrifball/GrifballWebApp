namespace GrifballWebApp.Server.Profile;

public class ProfileStatsDto
{
    public required int UserID { get; set; }
    public string? Gamertag { get; set; }
    public string? DisplayName { get; set; }
    public bool IsDummyUser { get; set; }
    public int? RegionID { get; set; }
    public string? RegionName { get; set; }
    /// <summary>Gamertags the user's Xbox account had before (its renames), latest first.</summary>
    public List<string> PastGamertags { get; set; } = [];
    public List<PlayerChampionshipDto> Championships { get; set; } = [];
    public List<GameCareerSummaryDto> CareerByGame { get; set; } = [];
    public GameCareerSummaryDto? OverallCareer { get; set; }
    public List<PlayerSeasonStatDetailDto> Seasons { get; set; } = [];
    /// <summary>Late League (matchmaking): a line per matchmaking season, and one for the queue's games outside any.</summary>
    public List<GrifballWebApp.Server.LateLeague.MatchmakingPlayerStatDto> LateLeague { get; set; } = [];
    /// <summary>The stat strip at the top of the profile: MMR, K/D and bomb scores.</summary>
    public ServiceRecordDto? ServiceRecord { get; set; }
}

public class PlayerChampionshipDto
{
    public int PlayerChampionshipID { get; set; }
    public required GrifballWebApp.Server.Seasons.SeasonInfoDto Season { get; set; }
    /// <summary>The game the title was won on, which can differ from the season's; null when the record names none.</summary>
    public int? GameVersionID { get; set; }
    public string? GameVersionName { get; set; }
    public string? GameVersionSlug { get; set; }
    public required string Tier { get; set; }
    public int TeamID { get; set; }
    public required string TeamName { get; set; }
    public bool IsCaptain { get; set; }
    public required string GamertagUsed { get; set; }
}

public class GameCareerSummaryDto
{
    public int? GameVersionID { get; set; }
    public string? GameVersionName { get; set; }
    public string? GameVersionSlug { get; set; }
    public int SeasonsCount { get; set; }
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public decimal? WinPercent { get; set; }
    /// <summary>Null on the all-games row: goals, carrier stops and medals mean something different in each game, so they
    /// are only totalled per game.</summary>
    public long? Goals { get; set; }
    public decimal? GoalsPerGame { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public decimal? KdRatio { get; set; }
    public int KdSpread { get; set; }
    public int? CarrierKills { get; set; }
    public decimal? StopsPerGame { get; set; }
    public int? Sprees { get; set; }
    public int? MultiKills { get; set; }
    public int? Exterminations { get; set; }
    public int? Betrayals { get; set; }
    public int? HammerKills { get; set; }
    public int? SwordKills { get; set; }
    public int? BallKills { get; set; }
    public int? Punches { get; set; }
    public long? DamageDealt { get; set; }
    public long? DamageTaken { get; set; }
    public long? Score { get; set; }
}

/// <summary>A profile's season line: the season stats row every page shows (<see cref="GrifballWebApp.Server.Seasons.SeasonPlayerStatDto"/>),
/// with the season it is of.</summary>
public class PlayerSeasonStatDetailDto : GrifballWebApp.Server.Seasons.SeasonPlayerStatDto
{
    public GrifballWebApp.Server.Seasons.SeasonInfoDto Season { get; set; } = null!;
    /// <summary>The line's game, which can differ from the season's.</summary>
    public int? GameVersionID { get; set; }
    public string? GameVersionName { get; set; }
    public string? GameVersionSlug { get; set; }
    public int? DamageDealt { get; set; }
    public int? DamageTaken { get; set; }
    public long? Score { get; set; }

    public static PlayerSeasonStatDetailDto From(GrifballWebApp.Server.Stats.SeasonStatLine line)
    {
        var dto = From<PlayerSeasonStatDetailDto>(line);
        dto.Season = line.Season;
        dto.GameVersionID = line.GameVersionID;
        dto.GameVersionName = line.GameVersionName;
        dto.GameVersionSlug = line.GameVersionSlug;
        dto.DamageDealt = line.DamageDealt;
        dto.DamageTaken = line.DamageTaken;
        dto.Score = line.Score;
        return dto;
    }
}
