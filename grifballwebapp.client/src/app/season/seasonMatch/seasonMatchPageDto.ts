import { DateTime } from "luxon";
import { fromApiDate } from "../../shared/apiDates";

export interface SeasonMatchPageDto {
  seasonID: number,
  seasonName: string,
  isPlayoff: boolean,
  homeTeamName: string | null,
  homeTeamID: number | null,
  homeTeamScore: number | null,
  homeTeamResult: 'Won' | 'Loss' | 'Forfeit' | 'Bye' | 'TBD'
  awayTeamName: string | null,
  awayTeamID: number | null,
  awayTeamScore: number | null,
  awayTeamResult: 'Won' | 'Loss' | 'Forfeit' | 'Bye' | 'TBD'
  scheduledTime: DateTime | null,
  bestOf: number,
  reportedGames: ReportedGameDto[],
  /** The series' games that are not Waypoint matches: entered by hand, or recorded by the historical import. */
  recordedGames?: RecordedSeriesGameDto[],
  /** The season was imported from the historical archive: its games are not entered by hand. */
  isImported?: boolean,
  bracketInfo: BracketInfoDto | null,
  activeRescheduleRequestId: number | null,
  /** Whether the signed-in user may request a reschedule now: an undecided match between two teams, no request
   * active, and the user on either roster or a commissioner (decided on the server). */
  canRequestReschedule: boolean,
}

export interface BracketInfoDto {
  homeTeamSeed: number | null,
  awayTeamSeed: number | null,
  homeTeamPreviousMatchID: number | null,
  awayTeamPreviousMatchID: number | null,
  winnerNextMatchID: number | null,
  loserNextMatchID: number | null,
  /** The grand final or its sudden death: these cannot be forfeited. */
  isGrandFinal: boolean,
}

export interface ReportedGameDto {
  matchID: string,
  matchNumber: number,
}

/** The page as the API sends it (scheduledTime as an ISO string), with a real DateTime; null (no such match) stays null. */
export function parseSeasonMatchPage(raw: any): SeasonMatchPageDto | null {
  if (raw == null)
    return null;
  return { ...raw, scheduledTime: fromApiDate(raw.scheduledTime) };
}

/** A game of the series that is not a Waypoint match: entered by hand (isManual: a commissioner can edit and delete
 * it) or recorded by the historical import. */
export interface RecordedSeriesGameDto {
  recordedGameID: number,
  gameNumber: number,
  gameVersionID: number,
  gameVersionName: string,
  map: string | null,
  gametype: string | null,
  /** ISO time, or null when it was entered without one. */
  playedAt: string | null,
  homeTeamScore: number | null,
  awayTeamScore: number | null,
  homeTeamWon: boolean,
  awayTeamWon: boolean,
  isManual: boolean,
  players: RecordedSeriesGamePlayerDto[],
}

export interface RecordedSeriesGamePlayerDto {
  userID: number,
  gamertag: string,
  isHomeTeam: boolean,
  kills: number,
  deaths: number,
  assists: number,
  score: number | null,
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
  assists: number,
  score: number | null,
}
