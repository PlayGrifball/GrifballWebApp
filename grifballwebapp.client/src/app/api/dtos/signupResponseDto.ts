import { DateTime } from "luxon";

export class SignupResponseDto {
    seasonID: number = 0;
    personID: number = 0;
    personName: string | null = null;
    timeStamp!: DateTime;
    teamName: string | null = null;
    willCaptain: boolean = false;
    requiresAssistanceDrafting: boolean = false;
  }
  