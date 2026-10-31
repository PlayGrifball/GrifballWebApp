/** A player's totals for one stint of the season (regular season or playoffs): GET /api/Season/GetSeasonPlayerStats/{id}. */
export interface SeasonPlayerStatDto {
  /** The stored row, for stored season totals; null for stats from the season's games. */
  playerSeasonStatID: number | null;
  userID: number;
  gamertag: string;
  teamID?: number | null;
  teamName?: string | null;
  division?: string | null;
  tradeNote?: string | null;
  roleNote?: string | null;
  stintIndex: number;
  isPlayoffs: boolean;
  isCombined: boolean;
  source: string;
  gamesPlayed: number;
  wins: number;
  losses: number;
  winPercent?: number | null;
  goals: number;
  kills: number;
  deaths: number;
  assists: number;
  kdRatio?: number | null;
  kdSpread?: number | null;
  carrierKills: number;
  sprees: number;
  multiKills: number;
  exterminations: number;
}
