export interface UnscheduledMatchDto {
    seasonMatchID: number,
    homeCaptain: string,
    awayCaptain: string,
    /** The team as the schedule shows it: its name, then its captain in parentheses. */
    homeTeam: string,
    awayTeam: string,
    complete: boolean,
}
