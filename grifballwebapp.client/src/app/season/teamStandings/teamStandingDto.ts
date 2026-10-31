export interface TeamStandingDto {
  teamID: number,
  teamName: string,
  wins: number,
  losses: number,
  /** The record is the source spreadsheet's: the season's matches are not in the app. */
  recordFromSource?: boolean,
}
