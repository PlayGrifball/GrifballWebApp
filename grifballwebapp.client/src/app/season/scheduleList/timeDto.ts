import { DateTime } from "luxon";

export interface TimeDto {
  SeasonMatchID: number,
  HomeTeamName: string,
  AwayTeamName: string,
  Complete: boolean,
  Time: DateTime,
  DateTime: DateTime,
  LocalWeekNumber: number,
  LocalWeekYear: number,
}
