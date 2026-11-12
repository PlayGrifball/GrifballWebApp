/** The quick search's hits (GET /api/search): a few players, teams and seasons, the best first. */
export interface QuickSearchDto {
  players: QuickSearchPlayerDto[];
  teams: QuickSearchTeamDto[];
  seasons: QuickSearchSeasonDto[];
}

export interface QuickSearchPlayerDto {
  userID: number;
  /** The name the site shows them by. */
  name: string;
  /** The past gamertag the search matched, when it was one of those rather than their current name. */
  pastGamertag: string | null;
  /** The Discord or display name the search matched, when that is not the name shown. */
  matchedName?: string | null;
  /** Where the name comes from, so two players of one name can be told apart: 'Xbox' when it is the gamertag their Xbox
   * account holds now, 'Archive' for a player of the imported archive no Xbox account is linked to (the gamertag's
   * holder today may be someone else); null for a signed-up player known by a Discord or display name. */
  kind?: 'Xbox' | 'Archive' | null;
  /** An archive player's seasons' years ("2009" or "2009–2011"). */
  years?: string | null;
}

/** A team of one season: it links to that season's roster. */
export interface QuickSearchTeamDto {
  teamID: number;
  teamName: string;
  seasonID: number;
  seasonName: string;
}

export interface QuickSearchSeasonDto {
  seasonID: number;
  seasonName: string;
  /** ISO dates (UTC). */
  start: string;
  end: string;
  /** The years it ran in: "2019", or "2018–2019" when it crossed a new year (QuickSearchService.YearSpan). */
  years: string;
}
