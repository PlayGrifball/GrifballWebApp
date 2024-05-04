import { DateTime } from "luxon";

export interface TimeDto {
  SeasonMatchID: number,
  HomeCaptainName: string,
  AwayCaptainName: string,
  Time: DateTime,
  DateTime: DateTime,
  LocalWeekNumber: number,
  LocalWeekYear: number,

}
