import { DateTime } from "luxon";

export interface WeekDto {
  weekNumber: number,
  games: WeekGameDto[]
}

export interface WeekGameDto {
  seasonMatchID: number,
  homeTeamName: string,
  awayTeamName: string,
  complete: boolean,
  time: DateTime
}
