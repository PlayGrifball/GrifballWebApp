import { PlayerDto } from "../api/dtos/playerDto";
import { TeamResponseDto } from "../api/dtos/teamResponseDto";

export enum GrabbedThing {
  AvailablePlayer = "AvailablePlayer",
  Player = "Player",
  Team = "Team",
}

//export class GrabbedPlayer {
//  player!: PlayerDto;
//  team!: TeamResponseDto;
//}

export interface GrabbedPlayer {
  player: PlayerDto;
  team: TeamResponseDto;
}

export type GrabbedThingTypes = TeamResponseDto | PlayerDto;
