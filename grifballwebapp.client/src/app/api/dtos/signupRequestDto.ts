import { TimeslotDto } from "./signupResponseDto";

export class SignupRequestDto {
  seasonID: number = 0;
  userID: number = 0;
  teamName: string | null = null;
  willCaptain: boolean = false;
  requiresAssistanceDrafting: boolean = false;
  timeslots: TimeslotDto[] = [];
}
