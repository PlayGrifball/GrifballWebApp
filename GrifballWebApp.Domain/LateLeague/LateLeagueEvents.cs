using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.LateLeague;

// The LateLeague context's domain events (IDomainEvent): the queue's spots, its matches, and the archive's seasons, stats
// and ladders. Each carries the aggregate it happened to, read once the change is saved.

/// <summary>A player took a spot in the queue: joined it, or was put back when their match was canceled.</summary>
public sealed record PlayerQueued(QueuedPlayer Spot) : IDomainEvent;

/// <summary>A player's spot is about to go (<see cref="QueuedPlayer.Leave"/>): they left, were matched, or were dropped.</summary>
public sealed record PlayerLeftQueue(QueuedPlayer Spot) : IDomainEvent;

/// <summary>The queue made a match of these players: it is announced on Discord.</summary>
public sealed record LateLeagueMatchStarted(MatchedMatch Match, IReadOnlyCollection<int> UserIDs) : IDomainEvent;

/// <summary>
/// The match was decided (reported as a Halo match, or by its players' vote), these players still in it: they may queue
/// again, and its result is posted (with what it did to their MMR: <see cref="LateLeagueGameRecorded"/>).
/// </summary>
public sealed record LateLeagueMatchDecided(MatchedMatch Match, IReadOnlyCollection<int> UserIDs) : IDomainEvent;

/// <summary>The match was called off, nobody winning: its players may queue again. <paramref name="Unannounced"/>: it never could be announced.</summary>
public sealed record LateLeagueMatchCanceled(MatchedMatch Match, IReadOnlyCollection<int> UserIDs, bool Unannounced) : IDomainEvent;

/// <summary>The players' vote kicked <paramref name="UserID"/> out of the match.</summary>
public sealed record LateLeaguePlayerKicked(MatchedMatch Match, int UserID) : IDomainEvent;

/// <summary>An archive game's match changed as the import has it now (its season, time or players), or a merge moved a player.</summary>
public sealed record LateLeagueMatchChanged(MatchedMatch Match) : IDomainEvent;

/// <summary>A Late League season of the archive was imported, or changed as the archive has it now.</summary>
public sealed record MatchmakingSeasonChanged(MatchmakingSeason Season) : IDomainEvent;

/// <summary>A Late League game was recorded for the user: a win or a loss, and their MMR from <paramref name="MmrBefore"/> to theirs now.</summary>
public sealed record LateLeagueGameRecorded(User User, bool Won, int MmrBefore) : IDomainEvent;

/// <summary>A player's Late League season stats from a sheet were recorded or restated.</summary>
public sealed record MatchmakingPlayerStatsChanged(MatchmakingPlayerStat Stat) : IDomainEvent;

/// <summary>A player's place on a Late League season's ladder was recorded, restated, or moved by a merge.</summary>
public sealed record LadderRankChanged(MatchmakingLadderRank Rank) : IDomainEvent;
