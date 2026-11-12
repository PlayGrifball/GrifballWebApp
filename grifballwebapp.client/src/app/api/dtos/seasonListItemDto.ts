import type { SeasonChampionDto } from './seasonSummaryDto';
import type { SeasonInfoDto } from './seasonInfoDto';
import type { SeasonFormat } from './seasonFormat';

/** A season in the season list (GET /api/seasons). */
export interface SeasonListItemDto extends SeasonInfoDto {
  teamCount: number;
  playerCount: number;
  champions: SeasonChampionDto[];
  /** The names of the teams that played the season: the list's search matches them. */
  teamNames: string[];
}

/** The season list's filters: null (or none given) for all leagues, games, formats or years. */
export interface SeasonListFilter {
  leagueID: number | null;
  gameVersionID: number | null;
  format?: SeasonFormat | null;
  /** Only seasons that started in this year. */
  year?: number | null;
}
