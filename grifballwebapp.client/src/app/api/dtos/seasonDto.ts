import { DateTime } from "luxon";

export class SeasonDto {
  seasonID: number = 0;
  seasonName: string = "";
  signupsOpen!: DateTime;
  signupsClose: string = "";
  draftStart: string = "";
  seasonStart: string = "";
  seasonEnd: string = "";
  signupsCount: number = 0;
}
