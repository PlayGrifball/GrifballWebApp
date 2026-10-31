namespace GrifballWebApp.Server.Profile;

public class ProfileStatsDto
{
    public required int UserID { get; set; }
    public string? Gamertag { get; set; }
    public string? DisplayName { get; set; }
    public bool IsDummyUser { get; set; }
    /// <summary>Gamertags the user's Xbox account had before (its renames), latest first.</summary>
    public List<string> PastGamertags { get; set; } = [];
    public List<PlayerChampionshipDto> Championships { get; set; } = [];
    public List<GameCareerSummaryDto> CareerByGame { get; set; } = [];
    public GameCareerSummaryDto? OverallCareer { get; set; }
    public List<PlayerSeasonStatDetailDto> Seasons { get; set; } = [];
}

public class PlayerChampionshipDto
{
    public int PlayerChampionshipID { get; set; }
    public int SeasonID { get; set; }
    public required string SeasonName { get; set; }
    /// <summary>The season's source workbook (Google Sheets), for provenance; null for the app's own seasons.</summary>
    public string? SeasonSourceUrl { get; set; }
    public int LeagueID { get; set; }
    public required string LeagueName { get; set; }
    public required string LeagueSlug { get; set; }
    public int? GameVersionID { get; set; }
    public string? GameVersionName { get; set; }
    public string? GameVersionSlug { get; set; }
    public required string Tier { get; set; }
    public int TeamID { get; set; }
    public required string TeamName { get; set; }
    public bool IsCaptain { get; set; }
    public required string GamertagUsed { get; set; }
    public int? Year { get; set; }
    public bool IsHistorical { get; set; }
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
    public int Goals { get; set; }
    public decimal? GoalsPerGame { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public decimal? KdRatio { get; set; }
    public int KdSpread { get; set; }
    public int CarrierKills { get; set; }
    public decimal? StopsPerGame { get; set; }
    public int Sprees { get; set; }
    public int MultiKills { get; set; }
    public int Exterminations { get; set; }
    public int Betrayals { get; set; }
    public int? HammerKills { get; set; }
    public int? SwordKills { get; set; }
    public int? BallKills { get; set; }
    public int? Punches { get; set; }
    public long? DamageDealt { get; set; }
    public long? DamageTaken { get; set; }
    public int? Score { get; set; }
}

/// <summary>A profile's season line: the season stats row every page shows (<see cref="GrifballWebApp.Server.Seasons.SeasonPlayerStatDto"/>),
/// with the season it is of.</summary>
public class PlayerSeasonStatDetailDto : GrifballWebApp.Server.Seasons.SeasonPlayerStatDto
{
    public int SeasonID { get; set; }
    public required string SeasonName { get; set; }
    /// <summary>The season's source workbook (Google Sheets), for provenance; null for the app's own seasons.</summary>
    public string? SeasonSourceUrl { get; set; }
    public int LeagueID { get; set; }
    public required string LeagueName { get; set; }
    public required string LeagueSlug { get; set; }
    public int? GameVersionID { get; set; }
    public string? GameVersionName { get; set; }
    public string? GameVersionSlug { get; set; }
    public int? DamageDealt { get; set; }
    public int? DamageTaken { get; set; }
    public int? Score { get; set; }
    public int Year { get; set; }
    public bool IsHistorical { get; set; }
}
