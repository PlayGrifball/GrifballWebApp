import { DateTime } from "luxon";
import { fromRequiredApiDate } from "../../shared/apiDates";

export class SeasonDto {
  seasonID: number = 0;
  seasonName: string = "";
  signupsOpen!: DateTime;
  signupsClose!: DateTime;
  draftStart!: DateTime;
  seasonStart!: DateTime;
  seasonEnd!: DateTime;
  signupsCount: number = 0;
  copyFrom: string | null = null;
  copyAvailability: boolean = false;
  copySignups: boolean = false;
  copyTeams: boolean = false;
}

/** A season as the API sends it (dates as ISO strings), with real DateTimes. */
export function parseSeason(raw: any): SeasonDto {
  return {
    ...raw,
    signupsOpen: fromRequiredApiDate(raw.signupsOpen),
    signupsClose: fromRequiredApiDate(raw.signupsClose),
    draftStart: fromRequiredApiDate(raw.draftStart),
    seasonStart: fromRequiredApiDate(raw.seasonStart),
    seasonEnd: fromRequiredApiDate(raw.seasonEnd),
  };
}
