import { DateTime } from "luxon";

export interface UserResponseDto {
  userID: number,
  userName: string,
  lockoutEnd: DateTime,
  lockoutEnabled: boolean,
  isDummyUser: boolean,
  accessFailedCount: number,
  region: string,
  displayName: string,
  gamertag: string,
  discord: string,
  externalAuthCount: number,
  roles: RoleDto[]
}

export interface RoleDto {
  roleName: string,
  hasRole: boolean
}
