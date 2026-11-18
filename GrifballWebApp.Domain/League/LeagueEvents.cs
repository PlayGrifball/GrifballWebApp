using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.League;

// The League context's domain events (IDomainEvent): what happened to a season, its teams, matches, signups, reschedules,
// VODs and stats. Each carries the aggregate it happened to, read once the change is saved (a new one's key is set then).

/// <summary>A season began: an organizer's (<see cref="Season.Organized"/>) or the archive's (<see cref="Season.Imported"/>).</summary>
public sealed record SeasonCreated(Season Season) : IDomainEvent;

/// <summary>What the season is changed: its name, league, game, division, format, or the archive source it is kept as.</summary>
public sealed record SeasonEdited(Season Season) : IDomainEvent;

/// <summary>The season's dates changed (<see cref="Season.Schedule"/>, <see cref="Season.ScheduleArchived"/>).</summary>
public sealed record SeasonScheduled(Season Season) : IDomainEvent;

/// <summary>When the season is public changed (<see cref="Season.Announce"/>).</summary>
public sealed record SeasonAnnounced(Season Season) : IDomainEvent;

/// <summary>The season's timeslots changed.</summary>
public sealed record SeasonTimeslotsChanged(Season Season) : IDomainEvent;

/// <summary>The season's captains were locked (the draft is on) or unlocked.</summary>
public sealed record CaptainsLockChanged(Season Season) : IDomainEvent;

/// <summary>Another season's signups or teams were copied to this one, in place of its own.</summary>
public sealed record SeasonCopiedFrom(Season Season) : IDomainEvent;

/// <summary>The season's champions' titles changed: recorded, taken away, or moved by a user merge.</summary>
public sealed record SeasonChampionsChanged(Season Season) : IDomainEvent;

/// <summary>The season's awards changed: recorded from the archive, removed with a team, or moved by a user merge.</summary>
public sealed record SeasonAwardsChanged(Season Season) : IDomainEvent;

/// <summary>A team began: a captain's of the draft, the archive's, or a copy of another season's.</summary>
public sealed record TeamFormed(Team Team) : IDomainEvent;

/// <summary>The team is about to go (<see cref="Team.Disband"/>): removed by the draft or the import, with its roster.</summary>
public sealed record TeamDisbanded(Team Team, IReadOnlyCollection<int> UserIDs) : IDomainEvent;

/// <summary>The team was renamed.</summary>
public sealed record TeamRenamed(Team Team) : IDomainEvent;

/// <summary>The team's captain changed, or their place among the season's captains.</summary>
public sealed record TeamCaptainChanged(Team Team) : IDomainEvent;

/// <summary>
/// The team's roster changed for <paramref name="UserIDs"/>: a player added (the draft's pick, the import, a merge), taken
/// off, or a pick moved to another round or traded to another team (each team raises its own).
/// </summary>
public sealed record TeamRosterChanged(Team Team, IReadOnlyCollection<int> UserIDs) : IDomainEvent;

/// <summary>The team's own timeslots changed, or were cleared (its roster's again).</summary>
public sealed record TeamTimeslotsChanged(Team Team) : IDomainEvent;

/// <summary>What the archive records of the team changed: its record, its placing, its draft.</summary>
public sealed record TeamArchiveRecorded(Team Team) : IDomainEvent;

/// <summary>A match of the season began: scheduled between two teams, placed in a bracket, or imported.</summary>
public sealed record SeasonMatchCreated(SeasonMatch Match) : IDomainEvent;

/// <summary>The match is about to go (<see cref="SeasonMatch.Cancel"/>): removed with its bracket, or by the import.</summary>
public sealed record SeasonMatchCanceled(SeasonMatch Match) : IDomainEvent;

/// <summary>The match was moved to another time, or back to unscheduled.</summary>
public sealed record SeasonMatchScheduled(SeasonMatch Match) : IDomainEvent;

/// <summary>Who plays the match changed: a team or a BYE placed on a side, or a side emptied.</summary>
public sealed record SeasonMatchTeamsChanged(SeasonMatch Match) : IDomainEvent;

/// <summary>The match's score or result changed: decided by its games, forfeited, a BYE's win, or taken back.</summary>
public sealed record SeasonMatchResultChanged(SeasonMatch Match) : IDomainEvent;

/// <summary>The games of the series changed: one reported, renumbered or taken out, or all of them cleared.</summary>
public sealed record SeriesGamesChanged(SeasonMatch Match) : IDomainEvent;

/// <summary>A player signed up for a season, or a signup was copied to another season.</summary>
public sealed record PlayerSignedUp(SeasonSignup Signup) : IDomainEvent;

/// <summary>A player changed their signup, or a merge moved it to the account kept.</summary>
public sealed record SignupChanged(SeasonSignup Signup) : IDomainEvent;

/// <summary>The signup is about to go (<see cref="SeasonSignup.Withdraw"/>): the player withdrew, or a merge or a copy replaced it.</summary>
public sealed record SignupWithdrawn(SeasonSignup Signup) : IDomainEvent;

/// <summary>A reschedule of a match was asked for.</summary>
public sealed record RescheduleRequested(MatchReschedule Reschedule) : IDomainEvent;

/// <summary>A reschedule request was approved or denied, closed because the match was decided, or given its thread.</summary>
public sealed record RescheduleChanged(MatchReschedule Reschedule) : IDomainEvent;

/// <summary>A VOD was found, refreshed, or matched to a season and its match (or dismissed).</summary>
public sealed record VodChanged(Vod Vod) : IDomainEvent;

/// <summary>A player's season stats from a stats sheet were recorded or restated, or lost their team.</summary>
public sealed record PlayerSeasonStatsChanged(PlayerSeasonStat Stat) : IDomainEvent;

/// <summary>The season's scheduling rules changed.</summary>
public sealed record SchedulingRulesChanged(SeasonSchedulingRules Rules) : IDomainEvent;

/// <summary>A schedule was proposed for a season's matches, or applied to them.</summary>
public sealed record ScheduleRecommendationChanged(ScheduleRecommendation Recommendation) : IDomainEvent;
