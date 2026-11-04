/** A team member in a season's roster (GET /api/Season/GetSeasonTeams/{id}). */
export interface SeasonTeamMemberDto {
  teamPlayerID: number;
  userID: number;
  gamertag: string;
  isCaptain: boolean;
  draftCaptainOrder?: number | null;
  draftRound?: number | null;
  draftPick?: number | null;
  region?: string | null;
}

/** A team and its roster in a season (GET /api/Season/GetSeasonTeams/{id}). */
export interface SeasonTeamDto {
  teamID: number;
  teamName: string;
  wins: number;
  losses: number;
  tbd: number;
  recordFromSource: boolean;
  titles: string[];
  draftCaptainOrder?: number | null;
  players: SeasonTeamMemberDto[];
}
