import type { SeasonFormat } from './seasonFormat';

/**
 * What a season is, the same on every page that shows one (SeasonInfoDto): the season page and list are one, and a
 * profile's lines and titles and a team page have it as their season.
 */
export interface SeasonInfoDto {
  seasonID: number;
  seasonName: string;
  start: string;
  end: string;
  year: number;
  /** The season is over: a section with nothing to show says it wasn't recorded. */
  hasEnded: boolean;
  leagueID: number;
  leagueName: string;
  leagueSlug: string;
  /** The season's game; null for one played on more than one. */
  gameVersionID?: number | null;
  gameVersionName?: string | null;
  gameVersionSlug?: string | null;
  division?: string | null;
  format?: SeasonFormat;
  /** Imported from the historical archive: historicalSourceUrl is its source spreadsheet. */
  isHistorical: boolean;
  historicalSourceUrl?: string | null;
}
