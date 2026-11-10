using GrifballWebApp.Domain.Halo;

namespace GrifballWebApp.Application.MatchDetails;

/// <summary>
/// One Halo Infinite match (its Waypoint match ID) with every stat pulled for it: each team's score and outcome, each
/// player's line and the medals they earned. The season match it was reported for, when it was.
/// </summary>
public record MatchDetailsDto
{
    public required Guid MatchID { get; set; }
    public required DateTime? StartTime { get; set; }
    public required DateTime? EndTime { get; set; }
    public required TimeSpan Duration { get; set; }
    /// <summary>The season match this game was reported as a game of; null for a game not reported (yet).</summary>
    public required MatchDetailsSeasonMatchDto? SeasonMatch { get; set; }
    /// <summary>Infinite's teams, in team ID order.</summary>
    public required MatchDetailsTeamDto[] Teams { get; set; }
    /// <summary>Every medal earned in the match, all players together, most earned first.</summary>
    public required MatchDetailsMedalDto[] Medals { get; set; }
}

public record MatchDetailsSeasonMatchDto
{
    public required int SeasonMatchID { get; set; }
    public required int SeasonID { get; set; }
    public required string SeasonName { get; set; }
    /// <summary>The game's number in the series.</summary>
    public required int GameNumber { get; set; }
    public required int BestOf { get; set; }
}

public record MatchDetailsTeamDto
{
    /// <summary>Infinite's team ID: 0 or 1.</summary>
    public required int TeamID { get; set; }
    /// <summary>The season match's team most of these players are on, when the game was reported for one.</summary>
    public required int? SeasonTeamID { get; set; }
    public required string? SeasonTeamName { get; set; }
    public required int Score { get; set; }
    public required Outcomes Outcome { get; set; }
    public required MatchDetailsPlayerDto[] Players { get; set; }
}

public record MatchDetailsPlayerDto
{
    /// <summary>The site's user for the Xbox account, when there is one (their profile).</summary>
    public required int? UserID { get; set; }
    public required string Gamertag { get; set; }
    /// <summary>Grifball goals as <see cref="GrifballWebApp.Application.Stats.GrifballGoals"/> reads them (a custom mode's decoded, #217);
    /// null when they can't be read yet.</summary>
    public required int? Goals { get; set; }
    /// <summary>Halo's points; null when the mode packs its own counts into it (#217), as the season stats leave it.</summary>
    public required int? PersonalScore { get; set; }
    public required int Rank { get; set; }
    public required int Kills { get; set; }
    public required int Deaths { get; set; }
    public required int Assists { get; set; }
    public required float Kda { get; set; }
    public required int Suicides { get; set; }
    public required int Betrayals { get; set; }
    public required int MeleeKills { get; set; }
    public required int PowerWeaponKills { get; set; }
    public required int MaxKillingSpree { get; set; }
    public required int CalloutAssists { get; set; }
    public required int ShotsFired { get; set; }
    public required int ShotsHit { get; set; }
    public required float Accuracy { get; set; }
    public required int DamageDealt { get; set; }
    public required int DamageTaken { get; set; }
    public required TimeSpan AverageLife { get; set; }
    public required TimeSpan TimePlayed { get; set; }
    public required int Spawns { get; set; }
    public required int ObjectivesCompleted { get; set; }
    public required bool JoinedInProgress { get; set; }
    public required bool LeftInProgress { get; set; }
    public required MatchDetailsMedalDto[] Medals { get; set; }
}

public record MatchDetailsMedalDto
{
    public required long MedalID { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Type { get; set; }
    public required string Difficulty { get; set; }
    public required int Count { get; set; }
}
