import { DateTime } from "luxon";

export class SignupResponseDto {
  seasonID: number = 0;
  userID: number = 0;
  personName: string | null = null;
  timeStamp!: DateTime;
  teamName: string | null = null;
  willCaptain: boolean = false;
  requiresAssistanceDrafting: boolean = false;
  timeslots: TimeslotDto[] = [];
}

export class TimeslotDto {
  id: number = 0;
  dayOfWeek: number = 0;
  time: string = '';
  isChecked: boolean = false;
  isDisabled: boolean = false;
  isHeader: boolean = false;
}
