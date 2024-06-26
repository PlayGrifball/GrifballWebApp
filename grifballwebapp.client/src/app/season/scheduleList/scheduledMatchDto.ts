import { DateTime } from "luxon";

export interface ScheduledMatchDto {
    seasonMatchID: number,
    homeCaptain: string,
    awayCaptain: string,
    complete: boolean,
    time: DateTime
}
