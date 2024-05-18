import { DateTime } from "luxon";

export interface UnscheduledMatchDto {
    seasonMatchID: number,
    homeCaptain: string,
    awayCaptain: string,
    complete: boolean,
    time: DateTime
}
