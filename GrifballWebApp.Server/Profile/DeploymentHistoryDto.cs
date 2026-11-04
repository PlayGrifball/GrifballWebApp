using GrifballWebApp.Server.Seasons;

namespace GrifballWebApp.Server.Profile;

/// <summary>How far a team went in its season, best first.</summary>
public enum DeploymentFinish
{
    /// <summary>The team won a title of the season (a <see cref="Database.Models.PlayerChampionship"/> names it).</summary>
    Champion,
    /// <summary>The team played in the season's playoffs: a bracket match, or playoff stats from the archive.</summary>
    Playoffs,
    /// <summary>The season is over and the team played no playoffs.</summary>
    RegularSeason,
    /// <summary>The season is not over yet and nothing above is known.</summary>
    InProgress,
}

/// <summary>One team a player was on in a season (a deployment), newest season first on the profile.</summary>
public class DeploymentHistoryDto
{
    public required SeasonInfoDto Season { get; set; }
    public int TeamID { get; set; }
    public required string TeamName { get; set; }
    /// <summary>The player captained the team.</summary>
    public bool IsCaptain { get; set; }
    /// <summary>The team's record, as the standings and team page count it (TeamRecord).</summary>
    public int Wins { get; set; }
    public int Losses { get; set; }
    /// <summary>The record is the source spreadsheet's: the season's matches are not in the app.</summary>
    public bool RecordFromSource { get; set; }
    public DeploymentFinish Finish { get; set; }
    /// <summary>The tier of the title the team won (Pro, Open, ...); null unless <see cref="Finish"/> is Champion.</summary>
    public string? ChampionshipTier { get; set; }
}
