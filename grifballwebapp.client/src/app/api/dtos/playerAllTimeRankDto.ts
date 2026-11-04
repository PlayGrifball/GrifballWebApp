export interface MetricRankDetailDto {
  rank: number;
  totalRanked: number;
  statValue: number;
  perGame?: number | null;
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
