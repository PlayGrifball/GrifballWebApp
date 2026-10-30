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
