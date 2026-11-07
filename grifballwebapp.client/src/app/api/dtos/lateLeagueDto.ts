/** A Late League season of the archive: the queue's random, skill-matched sides each game, so no teams, standings or champion. */
export interface MatchmakingSeasonDto {
  matchmakingSeasonID: number;
  name: string;
  gameVersionID: number | null;
  gameVersionName: string | null;
  gameVersionSlug: string | null;
  startsAt: string;
  endsAt: string;
  gameCount: number;
  playerCount: number;
  isHistorical: boolean;
  historicalSourceUrl: string | null;
}

/** A player's line of a matchmaking season; matchmakingSeasonID null for the Discord queue's games (Late League now). */
export interface MatchmakingPlayerStatDto {
  matchmakingSeasonID: number | null;
  matchmakingSeasonName: string | null;
  userID: number;
  gamertag: string;
  gameVersionID: number | null;
  gameVersionName: string | null;
  gameVersionSlug: string | null;
  /** Matches (totalled from its games) or Recorded (a sheet's totals). */
  source: string;
  gamesPlayed: number;
  /** Of gamesPlayed, the games decided by their players' vote: in the record (W-L), with no box score. */
  gamesDecidedByVote: number;
  wins: number;
  losses: number;
  winPercent: number | null;
  goals: number;
  kills: number;
  deaths: number;
  assists: number;
  kdRatio: number | null;
  kdSpread: number | null;
  carrierKills: number;
}

/** The Discord queue: Late League as it is played now, its games (reported, or decided by vote) and each player's totals. */
export interface MatchmakingQueueDto {
  gameCount: number;
  /** Of gameCount, those decided by their players' vote: a result with no box score. */
  voteDecidedGameCount: number;
  players: MatchmakingPlayerStatDto[];
}

export interface MatchmakingSeasonDetailDto {
  season: MatchmakingSeasonDto;
  players: MatchmakingPlayerStatDto[];
}
