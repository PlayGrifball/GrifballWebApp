using System.Linq.Expressions;
using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Server.TeamStandings;

/// <summary>A team's record: the one rule the standings and the team page both count by.</summary>
public class TeamRecord
{
    public int TeamID { get; set; }
    public required string TeamName { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    /// <summary>The record is the source spreadsheet's (Team.RecordedWins/RecordedLosses): the season's matches are not in the app.</summary>
    public bool FromSource { get; set; }

    /// <summary>
    /// Wins are matches won; losses are matches lost or forfeited (a BYE is neither, an undecided match not yet either).
    /// A team whose matches are not in the app has the record its source spreadsheet gives instead.
    /// </summary>
    public static readonly Expression<Func<Team, TeamRecord>> Of = t => new TeamRecord
    {
        TeamID = t.TeamID,
        TeamName = t.TeamName,
        Wins = t.RecordedWins ?? t.HomeMatches.Count(m => m.HomeTeamResult == SeasonMatchResult.Won)
            + t.AwayMatches.Count(m => m.AwayTeamResult == SeasonMatchResult.Won),
        Losses = t.RecordedLosses ?? t.HomeMatches.Count(m => m.HomeTeamResult == SeasonMatchResult.Loss || m.HomeTeamResult == SeasonMatchResult.Forfeit)
            + t.AwayMatches.Count(m => m.AwayTeamResult == SeasonMatchResult.Loss || m.AwayTeamResult == SeasonMatchResult.Forfeit),
        FromSource = t.RecordedWins != null || t.RecordedLosses != null,
    };
}
