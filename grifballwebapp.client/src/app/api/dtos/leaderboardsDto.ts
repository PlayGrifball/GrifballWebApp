/** A player's titles across every season. */
export interface RingLeaderDto { userID: number; gamertag: string; ringsCount: number; titles: string[]; }

/** A team name's titles across seasons, linked to its latest title-winning team. */
export interface DynastyDto {
  teamName: string;
  latestTeamID: number;
  latestSeasonID: number;
  championshipsCount: number;
  seasonsActive: number;
  titles: string[];
}

/** A player's career total of one stat. */
export interface MetricLeaderDto {
  userID: number;
  gamertag: string;
  statValue: number;
  gamesPlayed: number;
  perGame?: number | null;
  seasonsCount: number;
  secondaryStat?: string | null;
}

/** The all-time leaderboards (GET /api/Leaderboards/GetLeaderboards). */
export interface LeaderboardsDto {
  ringLeaders: RingLeaderDto[];
  dynasties: DynastyDto[];
  goalLeaders: MetricLeaderDto[];
  killLeaders: MetricLeaderDto[];
  gamesPlayedLeaders: MetricLeaderDto[];
  carrierStopLeaders: MetricLeaderDto[];
}
