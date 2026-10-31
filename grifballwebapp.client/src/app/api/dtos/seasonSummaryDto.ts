/** A player who holds a tier's title. */
export interface ChampionPlayerDto {
  userID: number;
  gamertag: string;
  isCaptain: boolean;
}

/** A tier's champions: the team and the players who hold the title. */
export interface SeasonChampionDto {
  tier: string;
  teamID: number;
  teamName: string;
  roster: ChampionPlayerDto[];
}

/**
 * A season's header (GET /api/Season/getSeasonSummary/{id}): what it is, its champions, and how much data of each kind
 * it has, so each section can say what is missing instead of showing an empty table.
 */
export interface SeasonSummaryDto {
  seasonID: number;
  seasonName: string;
  year: number;
  leagueID: number;
  leagueName: string;
  gameVersionName?: string | null;
  gameVersionSlug?: string | null;
  division?: string | null;
  /** Backfilled from the historical archive; historicalSourceUrl is its spreadsheet. */
  isHistorical: boolean;
  /** The season is over: a section with nothing in it now never will have. */
  hasEnded: boolean;
  historicalSourceUrl?: string | null;
  teamCount: number;
  signupCount: number;
  matchCount: number;
  bracketMatchCount: number;
  playerStatCount: number;
  champions: SeasonChampionDto[];
}

/** The URL of a season's summary (index.html fetches it early for the season page). */
export const seasonSummaryUrl = (seasonID: number) => '/api/Season/getSeasonSummary/' + seasonID;
