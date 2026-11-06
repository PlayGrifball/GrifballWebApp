/** A player's standing on one board. */
export interface MetricRankDetailDto {
  /** Null when the player has none of the stat, so isn't on the board. */
  rank: number | null;
  /** How many are on the board. */
  totalRanked: number;
  statValue: number;
  secondaryText?: string | null;
}

export interface PlayerAllTimeRankDto {
  userID: number;
  gamertag: string;
  matchedAlias?: string | null;
  pastGamertags: string[];
  isDummyUser: boolean;
  rings: MetricRankDetailDto;
  goals: MetricRankDetailDto;
  kills: MetricRankDetailDto;
  gamesPlayed: MetricRankDetailDto;
  carrierStops: MetricRankDetailDto;
}

export interface PlayerSearchResultDto {
  userID: number;
  gamertag: string;
  matchedAlias?: string | null;
}
