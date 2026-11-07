/** A player's titles across every season. */
export interface RingLeaderDto {
  rank: number;
  userID: number;
  gamertag: string;
  ringsCount: number;
  titles: TitleDto[];
}

/** A franchise's titles across seasons (its teams of one name in one league; a placeholder name like "Team 1" is one season's team alone), linked to its franchise history by its latest title-winning team. */
export interface DynastyDto {
  rank: number;
  teamName: string;
  latestTeamID: number;
  latestSeasonID: number;
  championshipsCount: number;
  seasonsActive: number;
  titles: TitleDto[];
}

/** A player's career total of one stat. */
export interface MetricLeaderDto {
  rank: number;
  userID: number;
  gamertag: string;
  /** The game of a per-game total (goals, carrier stops); none for a career total across games. */
  gameVersionName?: string | null;
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

/** A title: the season, its tier and the team that won it. */
export interface TitleDto {
  seasonID: number;
  seasonName: string;
  tier: string;
  teamID: number;
}
