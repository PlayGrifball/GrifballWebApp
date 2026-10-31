import type { SeasonChampionDto } from './seasonSummaryDto';

/** A season in the season list (GET /api/Season/GetSeasons). */
export interface SeasonListItemDto {
  seasonID: number;
  name: string;
  start: string;
  end: string;
  year: number;
  isCurrent: boolean;
  leagueID: number;
  leagueName: string;
  leagueSlug: string;
  gameVersionID?: number | null;
  gameVersionName?: string | null;
  gameVersionSlug?: string | null;
  division?: string | null;
  isHistorical: boolean;
  historicalSourceUrl?: string | null;
  teamCount: number;
  playerCount: number;
  champions: SeasonChampionDto[];
}

/** The season list's filters: null for all leagues or all games. */
export interface SeasonListFilter {
  leagueID: number | null;
  gameVersionID: number | null;
}
