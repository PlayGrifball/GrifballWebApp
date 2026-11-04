/** The quick search's hits (Search/QuickSearch): a few players, teams and seasons, the best first. */
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
}
