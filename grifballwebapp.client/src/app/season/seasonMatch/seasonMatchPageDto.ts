import { DateTime } from "luxon";
import { fromApiDate } from "../../shared/apiDates";

export interface SeasonMatchGameParticipantDto {
  userID: number | null;
  gamertag: string;
  kills: number;
  deaths: number;
  assists: number;
  bombScores: number;
  score: number | null;
}

export interface ReportedGameDto {
  matchID: string | null;
  recordedGameID?: number | null;
  matchNumber: number;
  homeScore?: number | null;
  awayScore?: number | null;
  homePlayers?: SeasonMatchGameParticipantDto[];
  awayPlayers?: SeasonMatchGameParticipantDto[];
  /** A recorded game a commissioner entered by hand: they can edit and delete it from the box score. */
  isManual?: boolean;
  /** A recorded game's details (null for a Waypoint game): what it shows, and what its edit starts from. */
  gameVersionID?: number | null;
  gameVersionName?: string | null;
  map?: string | null;
  gametype?: string | null;
  /** When a recorded game was played (ISO), if that was recorded. */
  playedAt?: string | null;
}

export interface SeasonMatchSeriesTotalsDto {
  homePlayers: SeasonMatchGameParticipantDto[];
  awayPlayers: SeasonMatchGameParticipantDto[];
}

export interface SeasonMatchPageDto {
  seasonID: number;
  seasonName: string;
  isPlayoff: boolean;
  homeTeamName: string | null;
  homeTeamID: number | null;
  homeTeamScore: number | null;
  homeTeamResult: 'Won' | 'Loss' | 'Forfeit' | 'Bye' | 'TBD';
  awayTeamName: string | null;
  awayTeamID: number | null;
  awayTeamScore: number | null;
  awayTeamResult: 'Won' | 'Loss' | 'Forfeit' | 'Bye' | 'TBD';
  scheduledTime: DateTime | null;
  bestOf: number;
  reportedGames: ReportedGameDto[];
  seriesTotals?: SeasonMatchSeriesTotalsDto | null;
  /** The season was imported from the historical archive: its games are not entered by hand. */
  isImported?: boolean;
  bracketInfo: BracketInfoDto | null;
  activeRescheduleRequestId: number | null;
  /** Whether the signed-in user may request a reschedule now: an undecided match between two teams, no request
   * active, and the user on either roster or a commissioner (decided on the server). */
  canRequestReschedule: boolean;
}

export interface BracketInfoDto {
  homeTeamSeed: number | null;
  awayTeamSeed: number | null;
  homeTeamPreviousMatchID: number | null;
  awayTeamPreviousMatchID: number | null;
  winnerNextMatchID: number | null;
  loserNextMatchID: number | null;
  /** The grand final or its sudden death: these cannot be forfeited. */
  isGrandFinal: boolean;
}

/** The page as the API sends it (scheduledTime as an ISO string), with a real DateTime; null (no such match) stays null. */
export function parseSeasonMatchPage(raw: any): SeasonMatchPageDto | null {
  if (raw == null)
    return null;
  return { ...raw, scheduledTime: fromApiDate(raw.scheduledTime) };
}

/** A game entered by hand (POST /api/SeasonMatch/AddManualGame, EditManualGame): the side with the higher score won. */
export interface ManualGameRequestDto {
  gameVersionID: number,
  map: string | null,
  gametype: string | null,
  playedAt: string | null,
  homeTeamScore: number,
  awayTeamScore: number,
  players: ManualGamePlayerRequestDto[],
}

export interface ManualGamePlayerRequestDto {
  userID: number,
  isHomeTeam: boolean,
  kills: number,
  deaths: number,
  /** Grifball goals (bomb scores). */
  goals: number | null,
  assists: number,
  score: number | null,
}
