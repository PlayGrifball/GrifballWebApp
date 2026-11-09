using GrifballWebApp.Server.Stats;

namespace GrifballWebApp.Server.Seasons;

/// <summary>
/// A season's page header: what it is, its champions, and how much of each kind of data it has, so the page can say
/// what is missing (a season backfilled from the historical archive has teams and stats but no signups or schedule;
/// one with Waypoint matches has those too).
/// </summary>
public class SeasonSummaryDto : SeasonInfoDto
{
    public int TeamCount { get; set; }
    public int SignupCount { get; set; }
    /// <summary>Regular season matches (not in the bracket).</summary>
    public int MatchCount { get; set; }
    public int BracketMatchCount { get; set; }
    public int PlayerStatCount { get; set; }
    public List<SeasonChampionDto> Champions { get; set; } = [];
}

/// <summary>A tier's champions: the team and the players who hold the title.</summary>
public class SeasonChampionDto
{
    public required string Tier { get; set; }
    public int TeamID { get; set; }
    public required string TeamName { get; set; }
    public List<ChampionPlayerDto> Roster { get; set; } = [];
}

public class ChampionPlayerDto
{
    public int UserID { get; set; }
    public required string Gamertag { get; set; }
    public bool IsCaptain { get; set; }
}

/// <summary>A player's totals for one stint of the season (regular season or playoffs), with their team.</summary>
public class SeasonPlayerStatDto
{
    /// <summary>The stored row, for stored season totals; null for stats from the season's games.</summary>
    public int? PlayerSeasonStatID { get; set; }
    public int UserID { get; set; }
    public string Gamertag { get; set; } = "";
    public int? TeamID { get; set; }
    public string? TeamName { get; set; }
    public string? TradeNote { get; set; }
    public string? RoleNote { get; set; }
    public int StintIndex { get; set; }
    public bool IsPlayoffs { get; set; }
    public bool IsCombined { get; set; }
    /// <summary>Matches (totalled from the season's games) or Recorded (season totals stored, e.g. a historical sheet's).</summary>
    public string Source { get; set; } = "";
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public decimal? WinPercent { get; set; }
    public long Goals { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public decimal? KdRatio { get; set; }
    public int? KdSpread { get; set; }
    public int CarrierKills { get; set; }
    public int Sprees { get; set; }
    public int MultiKills { get; set; }
    public int Exterminations { get; set; }
    /// <summary>Ball punches and sword kills: null when none of the games records them (an Infinite mode decoded from its
    /// score does, #217).</summary>
    public int? Punches { get; set; }
    public int? SwordKills { get; set; }

    /// <summary>The row of a stat line, for the season page and (with its season) a profile: the one mapping of a
    /// <see cref="SeasonStatLine"/>.</summary>
    public static T From<T>(SeasonStatLine line) where T : SeasonPlayerStatDto, new() => new T
    {
        PlayerSeasonStatID = line.PlayerSeasonStatID,
        UserID = line.UserID,
        Gamertag = line.Gamertag,
        TeamID = line.TeamID,
        TeamName = line.TeamName,
        TradeNote = line.TradeNote,
        RoleNote = line.RoleNote,
        StintIndex = line.StintIndex,
        IsPlayoffs = line.IsPlayoffs,
        IsCombined = line.IsCombined,
        Source = line.FromMatches ? "Matches" : "Recorded",
        GamesPlayed = line.GamesPlayed,
        Wins = line.Wins,
        Losses = line.Losses,
        WinPercent = line.WinPercent,
        Goals = line.Goals,
        Kills = line.Kills,
        Deaths = line.Deaths,
        Assists = line.Assists,
        KdRatio = line.KdRatio,
        KdSpread = line.KdSpread,
        CarrierKills = line.CarrierKills,
        Sprees = line.Sprees,
        MultiKills = line.MultiKills,
        Exterminations = line.Exterminations,
        Punches = line.Punches,
        SwordKills = line.SwordKills,
    };
}

/// <summary>The filters of the season list (GET Season/GetSeasons), with its paging and sorting.</summary>
public class SeasonListFilter : GrifballWebApp.Server.Dtos.PaginationFilter
{
    public int? LeagueID { get; set; }
    public int? GameVersionID { get; set; }
    /// <summary>True: only seasons that have ended; false: only the current ones; null: both.</summary>
    public bool? Ended { get; set; }
    /// <summary>Only seasons of this format; null: every format.</summary>
    public GrifballWebApp.Domain.League.SeasonFormat? Format { get; set; }
    /// <summary>Only seasons that started in this year; null: every year.</summary>
    public int? Year { get; set; }
}

/// <summary>A season in the season list: every season that has started, imported or played in the app alike.</summary>
public class SeasonListItemDto : SeasonInfoDto
{
    public int TeamCount { get; set; }
    public int PlayerCount { get; set; }
    public List<SeasonChampionDto> Champions { get; set; } = [];
    /// <summary>The names of the teams that played the season, alphabetical: the list's search matches them.</summary>
    public List<string> TeamNames { get; set; } = [];
}
