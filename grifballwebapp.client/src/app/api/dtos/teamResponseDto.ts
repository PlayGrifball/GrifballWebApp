import { CaptainDto } from "./captainDto";
import { PlayerDto } from "./playerDto";

export class TeamResponseDto {
    teamID: number = 0;
    teamName: string = "";
    captain: CaptainDto = {} as CaptainDto;
    players!: PlayerDto[];
  }
