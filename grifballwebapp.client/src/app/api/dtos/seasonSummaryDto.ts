import type { SeasonInfoDto } from './seasonInfoDto';

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
export interface SeasonSummaryDto extends SeasonInfoDto {
  teamCount: number;
  signupCount: number;
  matchCount: number;
  bracketMatchCount: number;
  playerStatCount: number;
  champions: SeasonChampionDto[];
}

/** The URL of a season's summary (index.html fetches it early for the season page). */
export const seasonSummaryUrl = (seasonID: number) => '/api/Season/getSeasonSummary/' + seasonID;
