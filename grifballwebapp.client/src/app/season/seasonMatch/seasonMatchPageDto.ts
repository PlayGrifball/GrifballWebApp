import { DateTime } from "luxon";

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
}

export interface BracketInfoDto {
  homeTeamSeed: number | null,
  awayTeamSeed: number | null,
  homeTeamPreviousMatchID: number | null,
  awayTeamPreviousMatchID: number | null,
  winnerNextMatchID: number | null,
  loserNextMatchID: number | null, 
}

export interface ReportedGameDto {
  matchID: string,
  matchNumber: number,
}
