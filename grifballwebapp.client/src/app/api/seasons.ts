import type { SeasonFormat } from '@api/models';

/** The seasons page's filters (GET /api/seasons): one league, game, format and year when given. */
export interface SeasonListFilter {
  leagueID: number | null;
  gameVersionID: number | null;
  format?: SeasonFormat | null;
  /** Only seasons that started in this year. */
  year?: number | null;
}

/** The URL of a season's summary (index.html fetches it early for the season page). */
export const seasonSummaryUrl = (seasonID: number) => `/api/seasons/${seasonID}/summary`;
