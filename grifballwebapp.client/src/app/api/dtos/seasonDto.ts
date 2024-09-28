import { DateTime } from "luxon";

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
