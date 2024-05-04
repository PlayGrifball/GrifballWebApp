import { DateTime } from "luxon";

export interface WeekDto {
  weekNumber: number,
  games: WeekGameDto[]
}

export interface WeekGameDto {
  seasonMatchID: number,
  homeCaptainName: string,
  awayCaptainName: string,
  time: DateTime
}
