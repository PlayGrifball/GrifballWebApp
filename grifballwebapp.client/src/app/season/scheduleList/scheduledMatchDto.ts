import { DateTime } from "luxon";
import { fromRequiredApiDate } from "../../shared/apiDates";

export interface ScheduledMatchDto {
    seasonMatchID: number,
    homeCaptain: string,
    awayCaptain: string,
    /** The team as the schedule shows it: its name, then its captain in parentheses. */
    homeTeam: string,
    awayTeam: string,
    complete: boolean,
    time: DateTime
}

/** A scheduled match as the API sends it (time as an ISO string), with a real DateTime. */
export function parseScheduledMatch(raw: any): ScheduledMatchDto {
  return { ...raw, time: fromRequiredApiDate(raw.time) };
}
