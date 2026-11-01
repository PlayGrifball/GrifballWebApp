import type { SeasonChampionDto } from './seasonSummaryDto';
import type { SeasonFormat } from './seasonFormat';

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
  /** Missing: a league season. */
  format?: SeasonFormat;
  isHistorical: boolean;
  historicalSourceUrl?: string | null;
  teamCount: number;
  playerCount: number;
  champions: SeasonChampionDto[];
}

/** The season list's filters: null (or no format) for all leagues, games or formats. */
export interface SeasonListFilter {
  leagueID: number | null;
  gameVersionID: number | null;
  format?: SeasonFormat | null;
}
