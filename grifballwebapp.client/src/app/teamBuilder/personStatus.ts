import type { TeamsPlayerDto } from '@api/models';
import type { TeamResponseDto } from '@api/models';

export enum GrabbedThing {
  AvailablePlayer = "AvailablePlayer",
  Player = "Player",
  Team = "Team",
}

//export class GrabbedPlayer {
//  player!: TeamsPlayerDto;
//  team!: TeamResponseDto;
//}

export interface GrabbedPlayer {
  player: TeamsPlayerDto;
  team: TeamResponseDto;
}

export type GrabbedThingTypes = TeamResponseDto | TeamsPlayerDto;
