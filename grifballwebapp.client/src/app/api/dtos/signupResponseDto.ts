export class SignupResponseDto {
  seasonID: number = 0;
  userID: number = 0;
  personName: string | null = null;
  timestamp: string = ''; // ISO 8601, UTC
  teamName: string | null = null;
  willCaptain: boolean = false;
  requiresAssistanceDrafting: boolean = false;
  timeslots: TimeslotDto[] = [];
}

/** GrifballWebApp.Server.Signups.TimeslotDto: time is "HH:mm:ss", dayOfWeek the enum name. */
export class TimeslotDto {
  optionID: number = 0;
  dayOfWeek: string = '';
  time: string = '';
  isChecked: boolean = false;
}
