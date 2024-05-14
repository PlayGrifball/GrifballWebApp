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
}

export interface ReportedGameDto {
  matchID: string,
  matchNumber: number,
}
