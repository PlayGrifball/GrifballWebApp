import type { SeasonFormat } from '@api/models';

/** The seasons page's filters (GET /api/seasons): one league, game, format and year when given. */
export interface SeasonListFilter {
  leagueID: number | null;
  gameVersionID: number | null;
  format?: SeasonFormat | null;
  /** Only seasons that started in this year. */
  year?: number | null;
}
