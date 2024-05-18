import { DateTime } from "luxon";

export interface TimeDto {
  SeasonMatchID: number,
  HomeCaptainName: string,
  AwayCaptainName: string,
  Complete: boolean,
  Time: DateTime,
  DateTime: DateTime,
  LocalWeekNumber: number,
  LocalWeekYear: number,
}
