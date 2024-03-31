
export class SignupRequestDto {
  seasonID: number = 0;
  personID: number = 0;
  teamName: string | null = null;
  willCaptain: boolean = false;
  requiresAssistanceDrafting: boolean = false;
}
