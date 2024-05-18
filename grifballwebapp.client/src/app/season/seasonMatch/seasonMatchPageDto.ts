import { DateTime } from "luxon";

export interface SeasonMatchPageDto {
  seasonID: number,
  seasonName: string,
  isPlayoff: boolean,
  homeTeamName: string | null,
  homeTeamID: number | null,
  homeTeamScore: number | null,
  awayTeamName: string | null,
  awayTeamID: number | null,
  awayTeamScore: number | null,
  scheduledTime: DateTime | null,
  bestOf: number,
  reportedGame: ReportedGameDto[],
  bracketInfo: BracketInfoDto | null,
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
