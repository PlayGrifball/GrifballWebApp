import { DateTime } from "luxon";
import { fromApiDate, fromRequiredApiDate } from "../../shared/apiDates";

export class SeasonDto {
  seasonID: number = 0;
  seasonName: string = "";
  /** When the season is announced (Discord events channel); null: not yet, as a new season is. */
  publicAt: DateTime | null = null;
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

/** A season as the API sends it (dates as ISO strings), with real DateTimes; null (no such season) stays null. */
export function parseSeason(raw: any): SeasonDto | null {
  if (raw == null)
    return null;
  return {
    ...raw,
    publicAt: fromApiDate(raw.publicAt),
    signupsOpen: fromRequiredApiDate(raw.signupsOpen),
    signupsClose: fromRequiredApiDate(raw.signupsClose),
    draftStart: fromRequiredApiDate(raw.draftStart),
    seasonStart: fromRequiredApiDate(raw.seasonStart),
    seasonEnd: fromRequiredApiDate(raw.seasonEnd),
  };
}
