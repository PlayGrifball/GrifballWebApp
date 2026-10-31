/** A game a season is played in (GET /api/EventOrganizer/GetGameVersions). */
export interface GameVersionDto { gameVersionID: number; name: string; }

/** A league seasons belong to (GET /api/EventOrganizer/GetLeagues). */
export interface LeagueDto { leagueID: number; name: string; }
