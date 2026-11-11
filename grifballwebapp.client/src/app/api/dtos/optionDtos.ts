/** A game a season is played in (GET /api/game-versions). */
export interface GameVersionDto { gameVersionID: number; name: string; }

/** A league seasons belong to (GET /api/leagues). */
export interface LeagueDto { leagueID: number; name: string; }
