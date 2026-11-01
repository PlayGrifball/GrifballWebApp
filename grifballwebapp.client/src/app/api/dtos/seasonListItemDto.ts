import type { SeasonChampionDto } from './seasonSummaryDto';
import type { SeasonInfoDto } from './seasonInfoDto';
import type { SeasonFormat } from './seasonFormat';

/** A season in the season list (GET /api/Season/GetSeasons). */
export interface SeasonListItemDto extends SeasonInfoDto {
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
