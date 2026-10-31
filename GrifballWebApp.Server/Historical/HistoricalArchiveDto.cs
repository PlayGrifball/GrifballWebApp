namespace GrifballWebApp.Server.Historical;

public class HistoricalSeasonDto
{
    public int SeasonID { get; set; }
    public required string SeasonName { get; set; }
    public int? Year { get; set; }
    public int LeagueID { get; set; }
    public required string LeagueName { get; set; }
    public required string LeagueSlug { get; set; }
    public int? GameVersionID { get; set; }
    public string? GameVersionName { get; set; }
    public string? GameVersionSlug { get; set; }
    public string? Division { get; set; }
    public string? HistoricalSourceUrl { get; set; }
    public List<HistoricalSeasonChampionDto> Champions { get; set; } = [];
    public List<HistoricalSeasonTeamDto> Teams { get; set; } = [];
    public List<HistoricalSeasonPlayerStatDto> PlayerStats { get; set; } = [];
}

public class HistoricalSeasonChampionDto
{
    public required string Tier { get; set; }
    public required string TeamName { get; set; }
    public required string TeamSlug { get; set; }
    public List<HistoricalChampionRosterDto> Roster { get; set; } = [];
}

public class HistoricalChampionRosterDto
{
    public int UserID { get; set; }
    public required string Gamertag { get; set; }
    public bool IsCaptain { get; set; }
}

public class HistoricalSeasonTeamDto
{
    public required string TeamName { get; set; }
    public required string TeamSlug { get; set; }
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public decimal? WinPercent { get; set; }
    public int Goals { get; set; }
    public bool WonChampionship { get; set; }
    public List<HistoricalTeamPlayerDto> Roster { get; set; } = [];
}

public class HistoricalTeamPlayerDto
{
    public int UserID { get; set; }
    public required string Gamertag { get; set; }
    public string? TradeNote { get; set; }
    public string? RoleNote { get; set; }
    public int StintIndex { get; set; }
    public int GamesPlayed { get; set; }
    public int Goals { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public decimal? KdRatio { get; set; }
    public int CarrierKills { get; set; }
}

public class HistoricalSeasonPlayerStatDto
{
    public int PlayerSeasonStatID { get; set; }
    public int UserID { get; set; }
    public required string Gamertag { get; set; }
    public string? TeamName { get; set; }
    public string? TeamSlug { get; set; }
    public string? Division { get; set; }
    public string? TradeNote { get; set; }
    public string? RoleNote { get; set; }
    public int StintIndex { get; set; }
    public bool IsPlayoffs { get; set; }
    public bool IsCombined { get; set; }
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public decimal? WinPercent { get; set; }
    public int Goals { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public decimal? KdRatio { get; set; }
    public int? KdSpread { get; set; }
    public int CarrierKills { get; set; }
    public int Sprees { get; set; }
    public int MultiKills { get; set; }
    public int Exterminations { get; set; }
}

public class HistoricalTeamDetailDto
{
    public required string TeamName { get; set; }
    public required string TeamSlug { get; set; }
    public int TotalChampionships { get; set; }
    public List<HistoricalTeamChampionshipDto> Championships { get; set; } = [];
    public List<HistoricalTeamSeasonAppearanceDto> Seasons { get; set; } = [];
    public List<HistoricalTeamAllTimePlayerDto> AllTimePlayers { get; set; } = [];
}

public class HistoricalTeamChampionshipDto
{
    public int SeasonID { get; set; }
    public required string SeasonName { get; set; }
    public int? Year { get; set; }
    public required string LeagueName { get; set; }
    public string? GameVersionName { get; set; }
    public required string Tier { get; set; }
    public List<HistoricalChampionRosterDto> Roster { get; set; } = [];
}

public class HistoricalTeamSeasonAppearanceDto
{
    public int SeasonID { get; set; }
    public required string SeasonName { get; set; }
    public int? Year { get; set; }
    public required string LeagueName { get; set; }
    public required string GameVersionName { get; set; }
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Goals { get; set; }
    public bool WonChampionship { get; set; }
    public List<HistoricalTeamPlayerDto> Roster { get; set; } = [];
}

public class HistoricalTeamAllTimePlayerDto
{
    public int UserID { get; set; }
    public required string Gamertag { get; set; }
    public int SeasonsPlayed { get; set; }
    public int GamesPlayed { get; set; }
    public int Goals { get; set; }
    public int ChampionshipsWon { get; set; }
}

public class HistoricalCatalogItemDto
{
    public int SeasonID { get; set; }
    public required string SeasonName { get; set; }
    public int? Year { get; set; }
    public int LeagueID { get; set; }
    public required string LeagueName { get; set; }
    public required string LeagueSlug { get; set; }
    public int? GameVersionID { get; set; }
    public string? GameVersionName { get; set; }
    public string? GameVersionSlug { get; set; }
    public string? Division { get; set; }
    public string? HistoricalSourceUrl { get; set; }
    public int TeamCount { get; set; }
    public int PlayerCount { get; set; }
    public List<HistoricalSeasonChampionDto> Champions { get; set; } = [];
}

public class HistoricalLeaderboardsDto
{
    public List<HistoricalPlayerRingLeaderDto> RingLeaders { get; set; } = [];
    public List<HistoricalTeamRingLeaderDto> FranchiseDynasties { get; set; } = [];
    public List<HistoricalPlayerMetricLeaderDto> GoalLeaders { get; set; } = [];
    public List<HistoricalPlayerMetricLeaderDto> KillLeaders { get; set; } = [];
    public List<HistoricalPlayerMetricLeaderDto> GamesPlayedLeaders { get; set; } = [];
    public List<HistoricalPlayerMetricLeaderDto> CarrierStopLeaders { get; set; } = [];
}

public class HistoricalPlayerRingLeaderDto
{
    public int UserID { get; set; }
    public required string Gamertag { get; set; }
    public int RingsCount { get; set; }
    public List<string> Titles { get; set; } = [];
}

public class HistoricalTeamRingLeaderDto
{
    public required string TeamName { get; set; }
    public required string TeamSlug { get; set; }
    public int ChampionshipsCount { get; set; }
    public int SeasonsActive { get; set; }
    public List<string> Titles { get; set; } = [];
}

public class HistoricalPlayerMetricLeaderDto
{
    public int UserID { get; set; }
    public required string Gamertag { get; set; }
    public int StatValue { get; set; }
    public int GamesPlayed { get; set; }
    public decimal? PerGame { get; set; }
    public int SeasonsCount { get; set; }
    public string? SecondaryStat { get; set; }
}

