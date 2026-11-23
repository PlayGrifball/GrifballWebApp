namespace GrifballWebApp.Domain.Archive;

/// <summary>
/// A plain rule a <see cref="StatFlag"/> is found by: conservative thresholds, each one in one place - fixed ones in
/// StatFlagThresholds, the team size and accuracy ones configurable (StatFlagOptions, fourth review), both in the
/// Application's StatFlagService - not spread through the checks. Left for later: how a game compares to a player's
/// own history, duplicate-game detection, and roster-membership checks (the issue's other candidate rules).
/// </summary>
public enum StatFlagRule
{
    /// <summary>Hand-entered: the team scores don't match the players' goals added up.</summary>
    ScoreDoesNotMatchGoals = 1,
    /// <summary>Hand-entered: the winner doesn't match the scores.</summary>
    WinnerDoesNotMatchScores = 2,
    // 3 was PlayerOnBothTeamsOrTwice: removed (#562 review) - RecordedGameParticipant's unique index on
    // (RecordedGameID, UserID) already makes it impossible, so the rule could never fire. Not reused.
    /// <summary>Hand-entered: a team's size is outside the season's expected range (fewer or - fourth review - more).</summary>
    TeamSmallerThanFormat = 4,
    /// <summary>Hand-entered: the game's date is before the season started, or in the future.</summary>
    DateOutsideSeason = 5,
    /// <summary>Any game: a stat that can't be negative is (kills, deaths, assists, goals).</summary>
    NegativeStat = 6,
    /// <summary>Any game: accuracy over 100%, or shots hit above shots fired.</summary>
    ImpossibleAccuracy = 7,
    /// <summary>Any game: a custom game mode whose score is still the large encoded number (#217), not decoded to goals.</summary>
    UndecodedCustomScore = 8,
    /// <summary>Any game: a season match's games don't add up to its series result.</summary>
    SeriesDoesNotMatchGames = 9,
    /// <summary>Hand-entered: a side's kills are well above the other side's deaths (a kill is somebody's death).</summary>
    KillsDoNotMatchDeaths = 10,
    /// <summary>Hand-entered: the game's date is well away from when its season match was scheduled.</summary>
    DateOutsideMatchWeek = 11,
    /// <summary>Halo match: a player's kills, deaths or goals are more than the match's length allows.</summary>
    StatsExceedGameLength = 12,
}
